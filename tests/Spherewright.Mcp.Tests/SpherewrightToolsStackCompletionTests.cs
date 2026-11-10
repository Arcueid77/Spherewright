using Spherewright.Contracts.Actions;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("selection")]
    [InlineData("duplicate")]
    [InlineData("cargo")]
    [InlineData("operation")]
    [InlineData("piler")]
    public async Task ReversalRequiresTheCompleteNativePathEcho(string fault)
    {
        var plan = new PreparedNormalAction { Prepared = true, ActionKind = NormalActionKinds.ConfigureBuilding,
            PlanToken = "synthetic-reverse-token", TargetObjectId = 7, BeltPathReversal = new()
            { SelectedObjectId = 7, PathId = 1, PathLengthCells = 50, BeltObjectIds = [7, 8],
                PilerObjectId = 9, PilerLayerObjectIds = [9, 10], EmptyIndependentPath = true } };
        switch (fault)
        {
            case "missing": plan.BeltPathReversal = null; break;
            case "selection": plan.BeltPathReversal!.SelectedObjectId++; break;
            case "duplicate": plan.BeltPathReversal!.BeltObjectIds.Add(7); break;
            case "cargo": plan.BeltPathReversal!.EmptyIndependentPath = false; break;
            case "operation": plan.BeltPathReversal!.NativeOperation = "direct_connection_write"; break;
            case "piler": plan.BeltPathReversal!.PilerLayerObjectIds = [10]; break;
        }
        var bridge = new FakeBridgeClient(SuccessResult()) { ConfigurePlan = plan };
        var result = await SpherewrightTools.PrepareConfigureBuildingAsync(bridge, "synthetic-session", 104,
            7, 0, "synthetic-belt-hash", mode: BuildingConfigurationModes.ReverseBeltPath);
        Assert.Equal(fault != "valid", result.IsError);
        if (fault != "valid") Assert.DoesNotContain("synthetic-reverse-token", result.StructuredContent!.Value.GetRawText());
    }
}
