using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Theory]
    [InlineData("valid")][InlineData("missing")][InlineData("entity")][InlineData("slot")]
    [InlineData("item")][InlineData("count")][InlineData("native")][InlineData("source")][InlineData("budget")]
    public async Task ExactStationPhysicalWithdrawalEchoIsRequiredBeforeAnyTokenIsExposed(string fault)
    {
        var p=new PreparedNormalAction{Prepared=true,ActionKind=NormalActionKinds.Transfer,
            PlanToken="synthetic-station-cargo-plan",SourceObjectId=77,
            StationCargoWithdrawal=new(){EntityId=77,StorageIndex=2,ItemId=1802,Count=1,
                NativeOperation=StationCargoWithdrawalPolicy.NativeOperation},
            ItemBudget=[new(){ItemId=1802,Count=1,Direction=TransferDirections.StationStorageToPlayer}]};
        switch(fault)
        {
            case "missing":p.StationCargoWithdrawal=null;break;
            case "entity":p.StationCargoWithdrawal!.EntityId++;break;
            case "slot":p.StationCargoWithdrawal!.StorageIndex=-1;break;
            case "item":p.StationCargoWithdrawal!.ItemId=1301;break;
            case "count":p.StationCargoWithdrawal!.Count=2;break;
            case "native":p.StationCargoWithdrawal!.NativeOperation="buffer-write";break;
            case "source":p.SourceObjectId=78;break;
            case "budget":p.ItemBudget.Clear();break;
        }
        var bridge=new FakeBridgeClient(SuccessResult()){TransferPlan=p};
        var result=await SpherewrightTools.PrepareTransferAsync(bridge,"synthetic-owned",901,
            TransferDirections.StationStorageToPlayer,77,1802,1,"player-hash","station-hash");
        Assert.Equal(fault!="valid",result.IsError);
        if(fault!="valid")Assert.DoesNotContain("synthetic-station-cargo-plan",result.StructuredContent!.Value.GetRawText());
    }
}
