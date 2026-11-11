using Spherewright.Bridge.Core.Celestial;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task DysonStructureForwardsAnIndependentExactRequest(bool previewOnly)
    {
        var structure = new DysonStructureRequest
        {
            Kind = DysonStructureKinds.Shell, LayerId = 2, BoundaryNodeIds = new() { 4, 7, 9 }, PreviewOnly = previewOnly,
        };
        var plan = new DysonStructurePlan
        {
            EvidenceVersion = 1, SessionId = "synthetic-dyson", PlanetId = 104, StarId = 1,
            Structure = structure.Copy(), BeforeDesignStateHash = "synthetic-design", NativeChecksComplete = true,
            UnlockedNodeLatitudeDegrees = 15, RequiredCellPoints = 144, PlannedObjectId = 4,
            NativeOperation = DysonStructurePolicy.NativeOperation(structure.Kind),
        };
        plan.BindingHash = DysonStructurePolicy.BindingHash(plan);
        var bridge = new FakeBridgeClient(SuccessResult()) { ConfigurePlan = new()
        {
            Prepared = !previewOnly, CommitAllowedNow = !previewOnly,
            PlanToken = previewOnly ? string.Empty : "synthetic-dyson-token", ActionKind = NormalActionKinds.ConfigureBuilding,
            ExpectedStateHash = plan.BindingHash, PlannedDysonStructure = plan,
        }};
        var result = await SpherewrightTools.PrepareConfigureBuildingAsync(bridge, "synthetic-dyson", 104, 0, 0,
            "synthetic-design", mode: BuildingConfigurationModes.DysonStructure, dysonStructure: structure);
        Assert.False(result.IsError);
        Assert.NotSame(structure, bridge.LastConfigureRequest!.DysonStructure);
        Assert.Equal(structure.BoundaryNodeIds, bridge.LastConfigureRequest.DysonStructure!.BoundaryNodeIds);
        structure.BoundaryNodeIds.Reverse();
        Assert.Equal(new[] { 4, 7, 9 }, bridge.LastConfigureRequest.DysonStructure.BoundaryNodeIds);
    }

    [Fact]
    public async Task LegacyPluginCannotExposeADysonStructureToken()
    {
        var bridge = new FakeBridgeClient(SuccessResult()) { ConfigurePlan = new()
        {
            Prepared = true, CommitAllowedNow = true, PlanToken = "synthetic-unbound-token",
            ActionKind = NormalActionKinds.ConfigureBuilding,
        }};
        var result = await SpherewrightTools.PrepareConfigureBuildingAsync(bridge, "synthetic", 104, 0, 0,
            "synthetic-design", mode: BuildingConfigurationModes.DysonStructure,
            dysonStructure: new() { Kind = DysonStructureKinds.Node, LayerId = 1, LocalPosition = new() { X = 11000 } });
        Assert.True(result.IsError);
        Assert.DoesNotContain("synthetic-unbound-token", result.StructuredContent!.Value.GetRawText());
    }

    [Fact]
    public async Task DysonPayloadOnAnotherModeStopsBeforeCallingThePlugin()
    {
        var bridge = new FakeBridgeClient(SuccessResult());
        var result = await SpherewrightTools.PrepareConfigureBuildingAsync(bridge, "synthetic", 104, 1, 1,
            "synthetic-design", dysonStructure: new());
        Assert.True(result.IsError);
        Assert.Null(bridge.LastConfigureRequest);
    }
}
