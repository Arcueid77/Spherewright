using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Theory]
    [InlineData("valid")][InlineData("missing")][InlineData("other_entity")][InlineData("other_fuel")]
    [InlineData("count")][InlineData("hot")][InlineData("private_write")][InlineData("budget")]
    public async Task ColdGeneratorFuelRequiresExactNativeEchoBeforeExposingTransferToken(string fault)
    {
        var p = new PreparedNormalAction { Prepared = true, ActionKind = NormalActionKinds.Transfer,
            PlanToken = "synthetic-cold-generator-plan", DestinationObjectId = 9,
            GeneratorColdStartFuel = new() { EvidenceVersion = 1, EntityId = 9, BuildingItemId = 2211,
                ItemId = 1802, Count = 8, EmptyUnlitGenerator = true, NativeOperation = GeneratorColdStartFuelPolicy.NativeOperation },
            ItemBudget = new() { new() { ItemId = 1802, Count = 8, Direction = TransferDirections.PlayerToGeneratorFuel } } };
        switch (fault)
        {
            case "missing": p.GeneratorColdStartFuel = null; break;
            case "other_entity": p.GeneratorColdStartFuel!.EntityId = 10; break;
            case "other_fuel": p.GeneratorColdStartFuel!.ItemId = 1801; break;
            case "count": p.GeneratorColdStartFuel!.Count = 9; break;
            case "hot": p.GeneratorColdStartFuel!.EmptyUnlitGenerator = false; break;
            case "private_write": p.GeneratorColdStartFuel!.NativeOperation = "buffer_write"; break;
            case "budget": p.ItemBudget.Clear(); break;
        }
        var bridge = new FakeBridgeClient(SuccessResult()) { TransferPlan = p };
        var result = await SpherewrightTools.PrepareTransferAsync(bridge, "synthetic-session", 402,
            TransferDirections.PlayerToGeneratorFuel, 9, 1802, 8, "player-hash", "generator-hash");
        Assert.Equal(fault != "valid", result.IsError);
        if (fault != "valid") Assert.DoesNotContain("synthetic-cold-generator-plan", result.StructuredContent!.Value.GetRawText());
    }
}
