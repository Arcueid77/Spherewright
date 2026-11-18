using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class AssemblerInputPolicyTests
{
    [Theory]
    [InlineData(1)] [InlineData(10)] [InlineData(25)] [InlineData(100)]
    public void AcceptsOwnedRecipeInputsIncludingAFullNativeByteOfProliferator(int count)
        => Assert.True(AssemblerInputPolicy.CanInsert(1404,count,100,2,4,true,true,true,true,true));

    [Fact]
    public void RefusesMissingStockWrongRecipeAndOutOfRangeBeforeAnyInsertion()
    {
        Assert.False(AssemblerInputPolicy.CanInsert(1404,101,101,0,0,true,true,true,true,true));
        Assert.False(AssemblerInputPolicy.CanInsert(1404,10,9,0,0,true,true,true,true,true));
        Assert.False(AssemblerInputPolicy.CanInsert(1404,10,10,0,0,true,false,true,true,true));
        Assert.False(AssemblerInputPolicy.CanInsert(1404,10,10,0,0,true,true,false,true,true));
        Assert.False(AssemblerInputPolicy.CanInsert(1404,10,10,0,0,true,true,true,true,false));
        Assert.False(AssemblerInputPolicy.CanInsert(1404,1,1,int.MaxValue,0,true,true,true,true,true));
    }

    [Fact]
    public void ToolEchoMustMatchTheExactDeviceAndRequestedItems()
    {
        var prepared=new PreparedNormalAction { Prepared=true, CommitAllowedNow=true, ActionKind=NormalActionKinds.Transfer,
          DestinationObjectId=987, ItemBudget=new(){new(){ItemId=1404,Count=10,Direction=TransferDirections.PlayerToAssemblerInput}}, AssemblerInput=new()
        { EvidenceVersion=1, EntityId=987, BuildingItemId=2304, RecipeId=70, ItemId=1404, Count=10,
          BeforeInputCount=0, NativeOperation=AssemblerInputPolicy.NativeOperation } };
        Assert.True(AssemblerInputPolicy.HasMatchingEcho(987,1404,10,prepared));
        Assert.False(AssemblerInputPolicy.HasMatchingEcho(988,1404,10,prepared));
        Assert.False(AssemblerInputPolicy.HasMatchingEcho(987,1404,9,prepared));
        prepared.AssemblerInput.NativeOperation="other";
        Assert.False(AssemblerInputPolicy.HasMatchingEcho(987,1404,10,prepared));
    }

    [Fact]
    public void SameTickReceiptProvesCountAndProliferatorConservation()
    {
        var result=new ActionResultSnapshot { Terminal=true, Succeeded=true, ActionKind=NormalActionKinds.Transfer,
            CompletedAtGameTick=123, AssemblerInputReadback=new() { EntityId=987,RecipeId=70,ItemId=1404,RequestedCount=10,
                InputCountBefore=2,InputCountAfter=12,InputIncBefore=1,InputIncAfter=31,
                PackageCountBefore=15,PackageCountAfter=5,PackageIncBefore=40,PackageIncAfter=10,
                ExactNativeBindingVerified=true,OtherBuffersPreserved=true,OtherPackageStockPreserved=true,
                ConservationVerified=true,CapturedAtGameTick=123 } };
        Assert.True(AssemblerInputPolicy.HasConservedResult(987,1404,10,result));
        result.AssemblerInputReadback.InputIncAfter++;
        Assert.False(AssemblerInputPolicy.HasConservedResult(987,1404,10,result));
        result.AssemblerInputReadback.InputIncAfter--;
        result.AssemblerInputReadback.CapturedAtGameTick++;
        Assert.False(AssemblerInputPolicy.HasConservedResult(987,1404,10,result));
    }

    [Fact]
    public void NativeInputChangePreservesOtherRecipeInputsAndOutput()
    {
        var served=new[]{2,0};var inc=new[]{1,0};var produced=new[]{3};
        Assert.True(AssemblerInputPolicy.BuffersConserved(served,new[]{2,10},inc,new[]{1,30},produced,new[]{3},1,10,30));
        Assert.False(AssemblerInputPolicy.BuffersConserved(served,new[]{3,10},inc,new[]{1,30},produced,new[]{3},1,10,30));
        Assert.False(AssemblerInputPolicy.BuffersConserved(served,new[]{2,10},inc,new[]{1,29},produced,new[]{3},1,10,30));
        Assert.False(AssemblerInputPolicy.BuffersConserved(served,new[]{2,10},inc,new[]{1,30},produced,new[]{4},1,10,30));
    }
}
