using System.Reflection;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Mcp.BridgeClient;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed class WalkingRouteToolsTests
{
    [Fact]
    public async Task ThreeBoundReadPassesProduceAnObservedTokenlessRoute()
    {
        var bridge = DispatchProxy.Create<IBridgeClient, WalkingBridgeProxy>();
        var proxy = (WalkingBridgeProxy)(object)bridge;
        var result = await SpherewrightTools.PrepareMoveAsync(bridge, "synthetic-session", 104,
            0, 200, 8, "player-state", previewWalkingRoute: true);
        Assert.False(result.IsError);
        Assert.Equal(3, proxy.Calls.Count);
        Assert.Null(proxy.Calls[0].ExpectedWalkingGeometryHash);
        Assert.All(proxy.Calls.Skip(1), request => Assert.Equal("synthetic-geometry", request.ExpectedWalkingGeometryHash));
        var json = result.StructuredContent!.Value.GetRawText();
        Assert.Contains("route_observed", json);
        Assert.DoesNotContain("synthetic-commit-capability", json);
    }

    [Fact]
    public async Task LegacyMoveTokenIsWithheldWhenPluginIgnoresPreviewFlag()
    {
        var bridge = DispatchProxy.Create<IBridgeClient, WalkingBridgeProxy>();
        var proxy = (WalkingBridgeProxy)(object)bridge;
        proxy.Legacy = true;
        var result = await SpherewrightTools.PrepareMoveAsync(bridge, "synthetic-session", 104,
            0, 200, 8, "player-state", previewWalkingRoute: true);
        Assert.True(result.IsError);
        Assert.Single(proxy.Calls);
        Assert.DoesNotContain("synthetic-commit-capability", result.StructuredContent!.Value.GetRawText());
    }

    [Fact]
    public async Task ChangedGeometryStopsBeforeAnyFurtherPassOrCommit()
    {
        var bridge = DispatchProxy.Create<IBridgeClient, WalkingBridgeProxy>();
        var proxy = (WalkingBridgeProxy)(object)bridge;
        proxy.ChangeAtPass = 2;
        var result = await SpherewrightTools.PrepareMoveAsync(bridge, "synthetic-session", 104,
            0, 200, 8, "player-state", previewWalkingRoute: true);
        Assert.True(result.IsError);
        Assert.Equal(2, proxy.Calls.Count);
    }

    [Fact]
    public async Task FinalIncompleteSceneCannotBorrowEarlierCompleteGeometry()
    {
        var bridge = DispatchProxy.Create<IBridgeClient, WalkingBridgeProxy>();
        var proxy = (WalkingBridgeProxy)(object)bridge;
        proxy.IncompleteAtPass = 3;
        var result = await SpherewrightTools.PrepareMoveAsync(bridge, "synthetic-session", 104,
            0, 200, 8, "player-state", previewWalkingRoute: true);
        Assert.False(result.IsError);
        var json = result.StructuredContent!.Value.GetRawText();
        Assert.Contains("partial", json);
        Assert.DoesNotContain("route_observed", json);
    }

    [Fact]
    public async Task OrdinaryMoveKeepsItsSinglePrepareAndOriginalToken()
    {
        var bridge = DispatchProxy.Create<IBridgeClient, WalkingBridgeProxy>();
        var proxy = (WalkingBridgeProxy)(object)bridge;
        proxy.Legacy = true;
        var result = await SpherewrightTools.PrepareMoveAsync(bridge, "synthetic-session", 104,
            0, 200, 8, "player-state");
        Assert.False(result.IsError);
        Assert.Single(proxy.Calls);
        Assert.False(proxy.Calls[0].PreviewWalkingRoute);
        Assert.Contains("synthetic-commit-capability", result.StructuredContent!.Value.GetRawText());
    }

    public class WalkingBridgeProxy : DispatchProxy
    {
        public List<PrepareMoveRequest> Calls { get; } = new();
        public bool Legacy { get; set; }
        public int ChangeAtPass { get; set; }
        public int IncompleteAtPass { get; set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Assert.Equal(nameof(IBridgeClient.PrepareMoveAsync), method!.Name);
            var request = (PrepareMoveRequest)args![1]!;
            Calls.Add(request);
            if (Legacy)
                return Task.FromResult(BridgeCallResult<PreparedNormalAction>.Succeeded(new()
                {
                    Prepared = true, PlanToken = "synthetic-commit-capability", CommitAllowedNow = true,
                }));
            Assert.True(request.PreviewWalkingRoute);
            if (Calls.Count == ChangeAtPass)
                return Task.FromResult(BridgeCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                    BridgeErrorCodes.StaleState, "synthetic-scene-changed", true, "Fresh-read.")));
            var length = Math.Sqrt(200d * 200 + 64);
            var geometry = new MovementGeometrySnapshot
            {
                State = "observed", Complete = Calls.Count != IncompleteAtPass,
                SessionId = "synthetic-session", PlanetId = 104, RadiusMetres = 16,
                GeometryHash = "synthetic-geometry", Origin = new() { Y = 200 },
                EffectiveTarget = new() { Y = (float)(40000 / length), Z = (float)(1600 / length) },
                Body = new() { new() { LowerCentreHeight = .45, UpperCentreHeight = 2.55, Radius = .45 } },
            };
            var preview = new WalkingRoutePreview { Geometry = geometry };
            if (request.WalkingPreviewPoints is not null)
                preview.TerrainPoints = request.WalkingPreviewPoints.Select((point, index) => new WalkingTerrainPoint
                {
                    Index = index, RequestedPosition = point, Position = point,
                    Observed = true, Dry = true, GroundNormalDotUp = 1, GroundRadiusMetres = 200,
                }).ToList();
            return Task.FromResult(BridgeCallResult<PreparedNormalAction>.Succeeded(new()
            {
                Prepared = true, WalkingRoutePreview = preview,
            }));
        }
    }
}
