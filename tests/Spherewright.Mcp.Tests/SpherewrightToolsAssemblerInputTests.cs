using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Theory]
    [InlineData("valid")] [InlineData("missing")] [InlineData("device")] [InlineData("item")]
    [InlineData("count")] [InlineData("operation")] [InlineData("budget")]
    public async Task AssemblerInputRequiresExactNativeEchoBeforeExposingTransferToken(string fault)
    {
        var plan=new PreparedNormalAction { Prepared=true, CommitAllowedNow=true, ActionKind=NormalActionKinds.Transfer,
            PlanToken="synthetic-assembler-plan", DestinationObjectId=987,
            AssemblerInput=new() { EvidenceVersion=1, EntityId=987, BuildingItemId=2304, RecipeId=70,
                ItemId=1404, Count=10, BeforeInputCount=0, NativeOperation=AssemblerInputPolicy.NativeOperation },
            ItemBudget=new(){new(){ItemId=1404,Count=10,Direction=TransferDirections.PlayerToAssemblerInput}} };
        switch(fault)
        {
            case "missing":plan.AssemblerInput=null;break;
            case "device":plan.AssemblerInput!.EntityId=988;break;
            case "item":plan.AssemblerInput!.ItemId=1123;break;
            case "count":plan.AssemblerInput!.Count=9;break;
            case "operation":plan.AssemblerInput!.NativeOperation="buffer_write";break;
            case "budget":plan.ItemBudget.Clear();break;
        }
        var bridge=new FakeBridgeClient(SuccessResult()){TransferPlan=plan};
        var result=await SpherewrightTools.PrepareTransferAsync(bridge,"synthetic-session",402,
            TransferDirections.PlayerToAssemblerInput,987,1404,10,"player-hash","assembler-hash");
        Assert.Equal(fault!="valid",result.IsError);
        if(fault!="valid")Assert.DoesNotContain("synthetic-assembler-plan",result.StructuredContent!.Value.GetRawText());
    }
}
