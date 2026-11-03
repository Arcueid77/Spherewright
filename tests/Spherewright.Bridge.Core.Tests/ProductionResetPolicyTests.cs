using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class ProductionResetPolicyTests
{
    private static ProductionResetFacts Chemical()=>new(){EntityId=1,BuildingItemId=2309,RecipeId=32,Kind="assembler",
        ExactNativeComponent=true,WithinBuildArea=true,EmptyPlayerHand=true,EmptyUnfilteredPackageSlots=2,
        Buffers=[new(){ItemId=1011,RawCount=1,NativeStackSize=100},new(){ItemId=1120,RawCount=2,NativeStackSize=100}]};
    [Fact]
    public void ObservedIdleChemicalResidualReturnsExactlyItsOneInputAndTwoByproducts()
    {
        var f=Chemical();Assert.Null(ProductionResetPolicy.Validate(f,out var p));
        Assert.Equal([1,2],p!.Returns.Select(x=>x.Count));Assert.Equal(2,p.DedicatedEmptyPackageSlots);
        Assert.Equal("FactorySystem.TakeBackItems_Assembler",p.NativeOperation);
    }
    [Theory]
    [InlineData("working")][InlineData("partial")][InlineData("bonus")][InlineData("capacity")][InlineData("hand")][InlineData("foreign")]
    public void WorkLossCapacityDropsAndUnprovedBindingsRemainBlocked(string fault)
    {
        var f=Chemical();switch(fault){case "working":f.Working=true;break;case "partial":f.ProductionProgress=1;break;
            case "bonus":f.ExtraProductionProgress=1;break;case "capacity":f.EmptyUnfilteredPackageSlots=1;break;
            case "hand":f.EmptyPlayerHand=false;break;case "foreign":f.ExactNativeComponent=false;break;}
        Assert.NotNull(ProductionResetPolicy.Validate(f,out _));
    }
    [Fact]
    public void UnconsumedWholeResearchMatricesCanReturnButFractionalPointsAndIncCannot()
    {
        var f=Chemical();f.Kind="lab-research-whole";f.RecipeId=0;f.BuildingItemId=2901;f.UnstackedLab=true;
        f.Buffers=[new(){ItemId=6003,RawCount=36000,RawInc=0,UnitsPerItem=3600,NativeStackSize=200}];
        Assert.Null(ProductionResetPolicy.Validate(f,out var p));Assert.Equal(10,p!.Returns.Single().Count);
        f.Buffers[0].RawCount=36030;Assert.NotNull(ProductionResetPolicy.Validate(f,out _));
        f.Buffers[0].RawCount=36000;f.Buffers[0].RawInc=1;Assert.NotNull(ProductionResetPolicy.Validate(f,out _));
    }
    [Fact]
    public void MatchingMcpEchoIsRequiredAndDoesNotTurnAResetIntoManufacture()
    {
        Assert.Null(ProductionResetPolicy.Validate(Chemical(),out var p));
        var prepared=new Spherewright.Contracts.Actions.PreparedNormalAction{Prepared=true,ActionKind="transfer",DestinationObjectId=1,ProductionReset=p,
            ItemBudget=p!.Returns.Select(x=>new Spherewright.Contracts.Actions.ActionItemBudget{ItemId=x.ItemId,Count=x.Count,Direction="production-reset-to-player"}).ToList()};
        Assert.True(ProductionResetPolicy.HasMatchingEcho(1,prepared));
        Assert.False(ProductionResetPolicy.HasMatchingEcho(1,new(){Prepared=true}));
        Assert.False(ProductionResetPolicy.HasMatchingEcho(2,new(){Prepared=true,ProductionReset=p}));
    }
}
