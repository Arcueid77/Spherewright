using Spherewright.Contracts.Actions;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("entity")]
    [InlineData("item")]
    [InlineData("count")]
    [InlineData("request")]
    [InlineData("inc")]
    [InlineData("occupied")]
    [InlineData("operation")]
    [InlineData("path")]
    [InlineData("length")]
    [InlineData("members")]
    [InlineData("duplicate")]
    [InlineData("budget")]
    public async Task BeltInsertionRequiresItsExactNativeSingleItemEcho(string fault)
    {
        var plan = new PreparedNormalAction { Prepared = true, ActionKind = NormalActionKinds.Transfer,
            PlanToken = "synthetic-belt-insertion-token", BeltCargoInsertion = new() { SelectedObjectId = 9,
                PathId = 1, PathLengthCells = 64, BeltObjectIds = [9], ItemId = 1101, EmptyIndependentPath = true },
            ItemBudget = [new() { ItemId = 1101, Count = 1, Direction = TransferDirections.PlayerToBeltCargo }] };
        switch (fault)
        {
            case "missing": plan.BeltCargoInsertion = null; break;
            case "entity": plan.BeltCargoInsertion!.SelectedObjectId++; break;
            case "item": plan.BeltCargoInsertion!.ItemId++; break;
            case "count": plan.BeltCargoInsertion!.Count++; break;
            case "inc": plan.BeltCargoInsertion!.Inc++; break;
            case "occupied": plan.BeltCargoInsertion!.EmptyIndependentPath = false; break;
            case "operation": plan.BeltCargoInsertion!.NativeOperation = "buffer_write"; break;
            case "path": plan.BeltCargoInsertion!.PathId = 0; break;
            case "length": plan.BeltCargoInsertion!.PathLengthCells = 4097; break;
            case "members": plan.BeltCargoInsertion!.BeltObjectIds = [10]; break;
            case "duplicate": plan.BeltCargoInsertion!.BeltObjectIds.Add(9); break;
            case "budget": plan.ItemBudget.Clear(); break;
        }
        var bridge = new FakeBridgeClient(SuccessResult()) { TransferPlan = plan };
        var result = await SpherewrightTools.PrepareTransferAsync(bridge, "synthetic-session", 104,
            TransferDirections.PlayerToBeltCargo, 9, 1101, fault == "request" ? 2 : 1, "player", "belt");
        Assert.Equal(fault != "valid", result.IsError);
        if (fault != "valid") Assert.DoesNotContain("synthetic-belt-insertion-token", result.StructuredContent!.Value.GetRawText());
    }
}
