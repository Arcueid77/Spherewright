using System.Reflection;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Spherewright.Mcp.BridgeClient;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed class NativeStackingToolsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewDistributorRequiresItsExactNativeStorageJoint(bool wrongJoint)
    {
        var bridge = DispatchProxy.Create<IBridgeClient, StackBridgeProxy>();
        var proxy = (StackBridgeProxy)(object)bridge;
        proxy.Response = new PreparedNormalAction { Prepared = true, PlannedModelIndex = 371,
            PlanToken = "synthetic-attachment-token", PlannedStackConstruction = new()
            {
                SelectedObjectId = 1, RootObjectId = 1, AnchorObjectId = 1,
                BeforeStackStateHash = "sha256:stack", BuildingItemId = 2107, ModelIndex = 371,
                ExistingLayerCount = 1, ExistingObjectIds = new() { 1 },
                BuildToolKind = "addon", HeightLimitKind = "native_reach", MaximumLayers = null,
                InputFromSlot = wrongJoint ? 15 : 13, InputToSlot = wrongJoint ? 14 : 0,
            } };
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "synthetic-session", 104, 2107,
            "player-state", stackOnObjectId: 1, expectedStackStateHash: "sha256:stack", preferredModelIndex: 371);
        Assert.Equal(wrongJoint, result.IsError);
        if (wrongJoint) Assert.DoesNotContain("synthetic-attachment-token", result.StructuredContent!.Value.GetRawText());
    }

    [Fact]
    public async Task AmbiguousPlacementStopsBeforeBridgeDispatch()
    {
        var bridge = DispatchProxy.Create<IBridgeClient, StackBridgeProxy>();
        var proxy = (StackBridgeProxy)(object)bridge;
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "synthetic-session", 104, 2101,
            "player-state", stackOnObjectId: 1, expectedStackStateHash: "sha256:stack", sourceObjectId: 1);
        Assert.True(result.IsError);
        Assert.Equal(0, proxy.Calls);
    }

    [Fact]
    public async Task LegacyPluginCannotSubstituteAnOrdinaryGroundBuildToken()
    {
        var bridge = DispatchProxy.Create<IBridgeClient, StackBridgeProxy>();
        var proxy = (StackBridgeProxy)(object)bridge;
        proxy.Response = new PreparedNormalAction { Prepared = true, PlanToken = "synthetic-legacy-token" };
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "synthetic-session", 104, 2101,
            "player-state", stackOnObjectId: 1, expectedStackStateHash: "sha256:stack", preferredModelIndex: 51);
        Assert.True(result.IsError);
        Assert.DoesNotContain("synthetic-legacy-token", result.StructuredContent!.Value.GetRawText());
        Assert.Equal(1, proxy.Calls);
    }

    [Fact]
    public async Task ExactModelAndWholeStackEchoPreservesTheNativePlan()
    {
        var bridge = DispatchProxy.Create<IBridgeClient, StackBridgeProxy>();
        var proxy = (StackBridgeProxy)(object)bridge;
        proxy.Response = new PreparedNormalAction
        {
            Prepared = true,
            PlannedModelIndex = 51,
            PlanToken = "synthetic-confirmed-token",
            PlannedStackConstruction = new NativeStackConstructionPlan
            {
                SelectedObjectId = 1, RootObjectId = 1, AnchorObjectId = 1,
                BeforeStackStateHash = "sha256:stack", BuildingItemId = 2101, ModelIndex = 51,
                ExistingLayerCount = 1, ExistingObjectIds = new() { 1 }, MaximumLayers = 4,
            },
        };
        var result = await SpherewrightTools.PrepareBuildAsync(bridge, "synthetic-session", 104, 2101,
            "player-state", stackOnObjectId: 1, expectedStackStateHash: "sha256:stack", preferredModelIndex: 51);
        Assert.False(result.IsError);
        Assert.Contains("synthetic-confirmed-token", result.StructuredContent!.Value.GetRawText());
        Assert.Equal(51, proxy.Request!.PreferredModelIndex);
        Assert.Equal(1, proxy.Request.StackOnObjectId);
    }

    public class StackBridgeProxy : DispatchProxy
    {
        public int Calls { get; private set; }
        public PrepareBuildRequest? Request { get; private set; }
        public PreparedNormalAction Response { get; set; } = new();

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Assert.Equal(nameof(IBridgeClient.PrepareBuildAsync), method!.Name);
            Calls++;
            Request = (PrepareBuildRequest)args![1]!;
            return Task.FromResult(BridgeCallResult<PreparedNormalAction>.Succeeded(Response));
        }
    }
}
