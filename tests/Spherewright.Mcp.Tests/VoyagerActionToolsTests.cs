using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Protocol;
using Spherewright.Mcp.BridgeClient;
using Spherewright.Mcp.Resources;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed class VoyagerActionToolsTests
{
    [Fact]
    public async Task SupplyPreparationMapsExactFreshCountWithoutCommitOrChangingSettings()
    {
        var bridge = NewBridge(); var proxy = Proxy(bridge);
        using var cancellation = new CancellationTokenSource();
        var result = await SpherewrightTools.PrepareSupplyMechaWarpersAsync(bridge, "owned", 101, "complete-player",
            count: 7, cancellationToken: cancellation.Token);
        Assert.False(result.IsError);
        Assert.Equal(new[] { nameof(IBridgeClient.PrepareSupplyMechaWarpersAsync) }, proxy.Calls);
        Assert.Equal("owned", proxy.LastSessionId);
        Assert.Equal(cancellation.Token, proxy.LastCancellationToken);
        var request = Assert.IsType<PrepareSupplyMechaWarpersRequest>(proxy.LastRequest);
        Assert.Equal(101, request.PlanetId); Assert.Equal(7, request.Count);
        Assert.Equal("complete-player", request.ExpectedPlayerStateHash); Assert.Equal(1, request.StateHashVersion);
        Assert.Equal(20, typeof(SpherewrightTools).GetMethod(nameof(SpherewrightTools.PrepareSupplyMechaWarpersAsync))!
            .GetParameters().Single(p => p.Name == "count").DefaultValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("original-journey")]
    public async Task VoyagerPreparationBindsInspectedRoutePlayerAndOriginalReturnJourney(string? journey)
    {
        var bridge = NewBridge(); var proxy = Proxy(bridge);
        var result = await SpherewrightTools.PrepareVoyagerFlightAsync(bridge, "owned", 101,
            "inspected-route", "complete-player", journeyId: journey);
        Assert.False(result.IsError);
        Assert.Equal(new[] { nameof(IBridgeClient.PrepareVoyagerFlightAsync) }, proxy.Calls);
        var request = Assert.IsType<PrepareVoyagerFlightRequest>(proxy.LastRequest);
        Assert.Equal(101, request.PlanetId); Assert.Equal("inspected-route", request.ExpectedRouteStateHash);
        Assert.Equal("complete-player", request.ExpectedPlayerStateHash); Assert.Equal(journey, request.JourneyId);
        Assert.Equal(1, request.StateHashVersion);
    }

    [Fact]
    public async Task CancelPreparationUsesOriginalFlightAndOriginWithoutAnotherMovementCall()
    {
        var bridge = NewBridge(); var proxy = Proxy(bridge);
        var result = await SpherewrightTools.PrepareCancelVoyagerFlightAsync(bridge, "owned", 101, "original-flight");
        Assert.False(result.IsError);
        Assert.Equal(new[] { nameof(IBridgeClient.PrepareCancelVoyagerFlightAsync) }, proxy.Calls);
        var request = Assert.IsType<PrepareCancelVoyagerFlightRequest>(proxy.LastRequest);
        Assert.Equal(101, request.PlanetId); Assert.Equal("original-flight", request.ActionId);
        Assert.Equal(1, request.StateHashVersion);
    }

    [Theory]
    [InlineData("supply", nameof(IBridgeClient.CommitSupplyMechaWarpersAsync))]
    [InlineData("flight", nameof(IBridgeClient.CommitVoyagerFlightAsync))]
    [InlineData("cancel", nameof(IBridgeClient.CommitCancelVoyagerFlightAsync))]
    public async Task CommitsMapOneOriginalNormalActionRequestWithoutClaimingCompletion(string operation, string expectedMethod)
    {
        var bridge = NewBridge(); var proxy = Proxy(bridge);
        const string key = "b5fc00ce-ad14-4a52-a8c1-6c3d388b0aef";
        using var cancellation = new CancellationTokenSource();
        var result = operation switch
        {
            "supply" => await SpherewrightTools.CommitSupplyMechaWarpersAsync(bridge, "owned", 101, "exact-plan", key, cancellation.Token),
            "flight" => await SpherewrightTools.CommitVoyagerFlightAsync(bridge, "owned", 101, "exact-plan", key, cancellation.Token),
            _ => await SpherewrightTools.CommitCancelVoyagerFlightAsync(bridge, "owned", 101, "exact-plan", key, cancellation.Token),
        };
        Assert.False(result.IsError); Assert.Equal(new[] { expectedMethod }, proxy.Calls);
        Assert.Equal("owned", proxy.LastSessionId); Assert.Equal(cancellation.Token, proxy.LastCancellationToken);
        var request = Assert.IsType<CommitNormalActionRequest>(proxy.LastRequest);
        Assert.Equal("owned", request.SessionId); Assert.Equal(101, request.PlanetId);
        Assert.Equal("exact-plan", request.PlanToken); Assert.Equal(key, request.IdempotencyKey);
        var accepted = result.StructuredContent!.Value.GetProperty("result");
        Assert.True(accepted.GetProperty("accepted").GetBoolean());
        Assert.Equal("queued", accepted.GetProperty("state").GetString());
        Assert.Equal("original-action", accepted.GetProperty("actionId").GetString());
    }

    [Theory]
    [InlineData(BridgeErrorCodes.InvalidRequest)]
    [InlineData(BridgeErrorCodes.StaleState)]
    [InlineData(BridgeErrorCodes.ActionOutcomeUnknown)]
    public async Task NativeSupplyRefusalOrUnknownIsReturnedWithoutRetryOrFallback(string code)
    {
        var bridge = NewBridge(); var proxy = Proxy(bridge);
        proxy.Error = BridgeError.Create(code, "Native proof unavailable.", false, "Reconcile the original action.");
        var result = await SpherewrightTools.CommitSupplyMechaWarpersAsync(bridge, "owned", 101, "exact-plan", Guid.NewGuid().ToString());
        Assert.True(result.IsError);
        Assert.Equal(new[] { nameof(IBridgeClient.CommitSupplyMechaWarpersAsync) }, proxy.Calls);
        Assert.Equal(code, result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task SupplyReadbackPreservesUnknownAfterFieldsAndNativeReturn()
    {
        var bridge = NewBridge(); var proxy = Proxy(bridge);
        proxy.Action = new ActionResultSnapshot
        {
            ActionId = "original-action", ActionKind = NormalActionKinds.SupplyMechaWarpers,
            State = "outcome_unknown", Terminal = true, Succeeded = false,
            MechaWarperSupplyReadback = new MechaWarperSupplyReadback { RequestedCount = 20, PackageCountBefore = 40 },
        };
        var result = await SpherewrightTools.GetActionResultAsync(bridge, "original-action");
        var action = result.StructuredContent!.Value.GetProperty("result");
        Assert.Equal("outcome_unknown", action.GetProperty("state").GetString());
        var proof = action.GetProperty("mechaWarperSupplyReadback");
        foreach (var field in new[] { "actualStored", "packageCountAfter", "packageIncAfter", "chamberCountAfter",
                     "chamberIncAfter", "autoReplenishWarperAfter", "nativeReturnedSuccess" })
            Assert.Equal(JsonValueKind.Null, proof.GetProperty(field).ValueKind);
        Assert.False(proof.GetProperty("readbackComplete").GetBoolean());
        Assert.False(proof.GetProperty("conservationVerified").GetBoolean());
        Assert.Equal(new[] { nameof(IBridgeClient.GetActionResultAsync) }, proxy.Calls);
    }

    [Fact]
    public async Task SupplyReadbackKeepsImmediateCountIncConservationAndUnchangedAutomaticSetting()
    {
        var bridge = NewBridge(); var proxy = Proxy(bridge);
        proxy.Action = new ActionResultSnapshot
        {
            ActionId = "original-action", State = "completed", Terminal = true, Succeeded = true,
            MechaWarperSupplyReadback = new MechaWarperSupplyReadback
            {
                RequestedCount = 20, ActualStored = 20, PackageCountBefore = 40, PackageCountAfter = 20,
                PackageIncBefore = 70, PackageIncAfter = 50, ChamberCountBefore = 0, ChamberCountAfter = 20,
                ChamberIncBefore = 0, ChamberIncAfter = 20, AutoReplenishWarperBefore = false,
                AutoReplenishWarperAfter = false, NativeReturnedSuccess = true, ReadbackComplete = true, ConservationVerified = true,
            },
        };
        var result = await SpherewrightTools.GetActionResultAsync(bridge, "original-action");
        var proof = result.StructuredContent!.Value.GetProperty("result").GetProperty("mechaWarperSupplyReadback");
        Assert.Equal(20, proof.GetProperty("actualStored").GetInt32());
        Assert.Equal(50, proof.GetProperty("packageIncAfter").GetInt32());
        Assert.Equal(20, proof.GetProperty("chamberIncAfter").GetInt32());
        Assert.True(proof.GetProperty("nativeReturnedSuccess").GetBoolean());
        Assert.True(proof.GetProperty("readbackComplete").GetBoolean());
        Assert.True(proof.GetProperty("conservationVerified").GetBoolean());
        Assert.False(proof.GetProperty("autoReplenishWarperAfter").GetBoolean());
    }

    [Fact]
    public async Task VoyagerReadbackKeepsOriginalOriginAndNullableActualSpaceIdentity()
    {
        var bridge = NewBridge(); var proxy = Proxy(bridge);
        proxy.Action = new ActionResultSnapshot
        {
            ActionId = "original-flight", PlanetId = 101, State = "running",
            VoyagerFlightReadback = new VoyagerFlightReadback
            {
                JourneyId = "original-journey", Phase = "exiting_warp", OriginStarId = 1, DestinationStarId = 2,
                StopRequested = true, NativeExitObserved = false, ReturnEnergyFloorJoules = 5000,
            },
        };
        var result = await SpherewrightTools.GetActionResultAsync(bridge, "original-flight");
        var action = result.StructuredContent!.Value.GetProperty("result");
        Assert.Equal(101, action.GetProperty("planetId").GetInt32());
        var proof = action.GetProperty("voyagerFlightReadback");
        foreach (var field in new[] { "selectedDestinationPlanetId", "actualStarId", "actualPlanetId", "remainingDistanceMeters" })
            Assert.Equal(JsonValueKind.Null, proof.GetProperty(field).ValueKind);
        Assert.True(proof.GetProperty("stopRequested").GetBoolean());
        Assert.False(proof.GetProperty("nativeExitObserved").GetBoolean());
        Assert.Equal(5000, proof.GetProperty("returnEnergyFloorJoules").GetDouble());
        Assert.Equal(new[] { nameof(IBridgeClient.GetActionResultAsync) }, proxy.Calls);
    }

    [Fact]
    public void SixExecutionMethodsHaveBoundSchemasAndNativeSafetyDescriptions()
    {
        var services = new ServiceCollection(); services.AddSingleton(NewBridge());
        services.AddMcpServer().WithToolsFromAssembly(typeof(SpherewrightTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tools = provider.GetServices<McpServerTool>().Select(t => t.ProtocolTool).ToArray();
        foreach (var suffix in new[] { "supply_mecha_warpers", "voyager_flight", "cancel_voyager_flight" })
        foreach (var verb in new[] { "prepare", "commit" })
        {
            var tool = Assert.Single(tools, t => t.Name == $"spherewright_{verb}_{suffix}");
            Assert.False(tool.Annotations!.ReadOnlyHint); Assert.False(tool.Annotations.OpenWorldHint);
            Assert.Equal(verb == "commit", tool.Annotations.DestructiveHint);
            Assert.Equal(verb == "commit", tool.Annotations.IdempotentHint);
            var required = tool.InputSchema.GetProperty("required").EnumerateArray().Select(p => p.GetString()).ToArray();
            Assert.Contains("sessionId", required); Assert.Contains("planetId", required);
            var properties = tool.InputSchema.GetProperty("properties");
            foreach (var forbidden in new[] { "autoReplenishWarper", "itemId", "userConfirmedInConversation", "recoveryMode" })
                Assert.False(properties.TryGetProperty(forbidden, out _));
            if (verb == "commit")
            {
                Assert.Contains("planToken", required); Assert.Contains("idempotencyKey", required);
            }
            else if (suffix == "cancel_voyager_flight") Assert.Contains("actionId", required);
            else
            {
                Assert.Contains("expectedPlayerStateHash", required);
                if (suffix == "voyager_flight") Assert.Contains("expectedRouteStateHash", required);
                else
                {
                    Assert.Equal("integer", properties.GetProperty("count").GetProperty("type").GetString());
                    Assert.DoesNotContain("count", required);
                }
            }
        }
        Assert.Equal("prepare_supply_mecha_warpers", BridgeMethods.PrepareSupplyMechaWarpers);
        Assert.Equal("commit_supply_mecha_warpers", BridgeMethods.CommitSupplyMechaWarpers);
        Assert.Equal("prepare_voyager_flight", BridgeMethods.PrepareVoyagerFlight);
        Assert.Equal("commit_voyager_flight", BridgeMethods.CommitVoyagerFlight);
        Assert.Equal("prepare_cancel_voyager_flight", BridgeMethods.PrepareCancelVoyagerFlight);
        Assert.Equal("commit_cancel_voyager_flight", BridgeMethods.CommitCancelVoyagerFlight);
    }

    [Fact]
    public void EmbeddedGuideRetainsSupplyOriginalActionAndReturnReserveBoundaries()
    {
        var guide = AgentPlaybookResources.GetOpeningMovementPlaybook().Text;
        foreach (var term in new[] { "spherewright_prepare_supply_mecha_warpers", "spherewright_commit_supply_mecha_warpers",
                     "mechaWarperSupplyReadback", "nativeReturnedSuccess", "readbackComplete", "AutoReplenishWarper",
                     "spherewright_prepare_voyager_flight", "spherewright_commit_voyager_flight", "expectedRouteStateHash",
                     "spherewright_prepare_cancel_voyager_flight", "spherewright_commit_cancel_voyager_flight",
                     "voyagerFlightReadback", "journeyId", "nativeExitObserved", "maximumWarpGameTicksPerLeg" })
            Assert.Contains(term, guide);
        Assert.Equal("spherewright://agent/playbooks/opening-movement-v1", AgentPlaybookResources.OpeningMovementUri);
    }

    private static IBridgeClient NewBridge() => DispatchProxy.Create<IBridgeClient, NativeActionBridgeProxy>();
    private static NativeActionBridgeProxy Proxy(IBridgeClient bridge) => (NativeActionBridgeProxy)(object)bridge;

    public class NativeActionBridgeProxy : DispatchProxy
    {
        public List<string> Calls { get; } = new();
        public object? LastRequest { get; private set; }
        public string? LastSessionId { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public BridgeError? Error { get; set; }
        public ActionResultSnapshot Action { get; set; } = new();
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.NotNull(targetMethod); var name = targetMethod!.Name; Calls.Add(name);
            if (name == nameof(IBridgeClient.GetActionResultAsync))
            {
                LastRequest = args![0]; LastCancellationToken = Assert.IsType<CancellationToken>(args[1]);
                return Task.FromResult(BridgeCallResult<ActionResultSnapshot>.Succeeded(Action));
            }
            LastSessionId = Assert.IsType<string>(args![0]); LastRequest = args[1];
            LastCancellationToken = Assert.IsType<CancellationToken>(args[2]);
            if (name is nameof(IBridgeClient.PrepareSupplyMechaWarpersAsync) or nameof(IBridgeClient.PrepareVoyagerFlightAsync)
                or nameof(IBridgeClient.PrepareCancelVoyagerFlightAsync))
                return Task.FromResult(Error is null
                    ? BridgeCallResult<PreparedNormalAction>.Succeeded(new PreparedNormalAction { Prepared = true, PlanToken = "native-plan" })
                    : BridgeCallResult<PreparedNormalAction>.Failed(Error));
            if (name is nameof(IBridgeClient.CommitSupplyMechaWarpersAsync) or nameof(IBridgeClient.CommitVoyagerFlightAsync)
                or nameof(IBridgeClient.CommitCancelVoyagerFlightAsync))
                return Task.FromResult(Error is null
                    ? BridgeCallResult<NormalActionCommitResult>.Succeeded(new NormalActionCommitResult { ActionId = "original-action", Accepted = true, State = "queued" })
                    : BridgeCallResult<NormalActionCommitResult>.Failed(Error));
            throw new InvalidOperationException($"Unexpected action call: {name}");
        }
    }
}
