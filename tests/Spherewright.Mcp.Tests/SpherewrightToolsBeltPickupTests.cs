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
    [InlineData("operation")]
    [InlineData("budget")]
    [InlineData("inc")]
    [InlineData("request")]
    [InlineData("duplicate")]
    public async Task BeltPickupRequiresItsCompleteNativeEchoBeforeExposingAToken(string fault)
    {
        var plan = new PreparedNormalAction { Prepared = true, ActionKind = NormalActionKinds.Transfer,
            PlanToken = "synthetic-belt-pickup-token", BeltCargoPickup = new() { SelectedObjectId = 9,
                PathId = 1, BeltObjectIds = [9], InserterObjectIds = [10], Items = [new() { ItemId = 1101, Count = 2 }] },
            ItemBudget = [new() { ItemId = 1101, Count = 2, Direction = TransferDirections.BeltCargoToPlayer }] };
        switch (fault)
        {
            case "missing": plan.BeltCargoPickup = null; break;
            case "entity": plan.BeltCargoPickup!.SelectedObjectId++; break;
            case "operation": plan.BeltCargoPickup!.NativeOperation = "buffer_clear"; break;
            case "budget": plan.ItemBudget.Clear(); break;
            case "inc": plan.BeltCargoPickup!.Items[0].Inc = 1; break;
            case "duplicate": plan.BeltCargoPickup!.BeltObjectIds.Add(9); break;
        }
        var bridge = new FakeBridgeClient(SuccessResult()) { TransferPlan = plan };
        var result = await SpherewrightTools.PrepareTransferAsync(bridge, "synthetic-session", 104,
            TransferDirections.BeltCargoToPlayer, 9, fault == "request" ? 1101 : 0, 0, "player", "belt");
        Assert.Equal(fault != "valid", result.IsError);
        if (fault != "valid") Assert.DoesNotContain("synthetic-belt-pickup-token", result.StructuredContent!.Value.GetRawText());
    }
}
