using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Theory]
    [InlineData("valid")][InlineData("missing")][InlineData("entity")][InlineData("operation")][InlineData("budget")][InlineData("count")][InlineData("request")][InlineData("case")]
    public async Task NativeProductionResetRequiresItsExactWholeReturnEchoBeforeAnyTokenIsExposed(string fault)
    {
        var p=new PreparedNormalAction{Prepared=true,ActionKind=NormalActionKinds.Transfer,PlanToken="synthetic-native-reset-plan",DestinationObjectId=9,
            ProductionReset=new(){EvidenceVersion=1,EntityId=9,BuildingItemId=2309,OriginalRecipeId=32,Kind="assembler",
                NativeOperation="FactorySystem.TakeBackItems_Assembler",DedicatedEmptyPackageSlots=2,Returns=[new(){ItemId=1011,Count=1},new(){ItemId=1120,Count=2}]},
            ItemBudget=[new(){ItemId=1011,Count=1,Direction=TransferDirections.ProductionResetToPlayer},new(){ItemId=1120,Count=2,Direction=TransferDirections.ProductionResetToPlayer}]};
        switch(fault){case "missing":p.ProductionReset=null;break;case "entity":p.ProductionReset!.EntityId=10;break;
            case "operation":p.ProductionReset!.NativeOperation="direct_buffer_clear";break;case "budget":p.ItemBudget.Clear();break;
            case "count":p.ItemBudget[0].Count=2;break;}
        var bridge=new FakeBridgeClient(SuccessResult()){TransferPlan=p};
        var result=await SpherewrightTools.PrepareTransferAsync(bridge,"synthetic-session",402,fault=="case" ? "Production-Reset-To-Player" : TransferDirections.ProductionResetToPlayer,9,
            fault=="request" ? 1011 : 0,0,"player-hash","device-hash");
        Assert.Equal(fault!="valid",result.IsError);
        if(fault!="valid")Assert.DoesNotContain("synthetic-native-reset-plan",result.StructuredContent!.Value.GetRawText());
    }
}
