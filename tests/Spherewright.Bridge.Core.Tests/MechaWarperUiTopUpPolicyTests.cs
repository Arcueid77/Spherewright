using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;
public sealed class MechaWarperUiTopUpPolicyTests
{
    [Fact]
    public void PausedSpaceUsesItsOwnPermitAndBoundedSailStopInsteadOfPretendingGrounded()
    {
        var f=Facts(2,20);f.CurrentPlanetVerified=false;f.IsGrounded=false;f.PausedSpaceSourceAuthorized=true;f.SpaceStopSpeed=.4;
        Assert.True(MechaWarperUiTopUpPolicy.Evaluate(f,18).Allowed);
        Assert.False(MechaWarperSupplyPolicy.Evaluate(f,18).Allowed);
        f.PausedSpaceSourceAuthorized=false;Assert.False(MechaWarperUiTopUpPolicy.Evaluate(f,18).Allowed);
        f.PausedSpaceSourceAuthorized=true;f.SpaceStopSpeed=.6;Assert.False(MechaWarperUiTopUpPolicy.Evaluate(f,18).Allowed);
    }
    [Theory]
    [InlineData(2,20,18,20,2)][InlineData(2,5,5,7,0)][InlineData(19,20,1,20,19)]
    public void NativeUiTransferUsesOnlyRealRemainingCapacity(int chamber,int package,int count,int afterChamber,int afterPackage)
    {
        var before=Facts(chamber,package);Assert.True(MechaWarperUiTopUpPolicy.Evaluate(before,count).Allowed);
        Assert.False(MechaWarperSupplyPolicy.Evaluate(before,count).Allowed);
        var after=Facts(afterChamber,afterPackage);Assert.True(MechaWarperUiTopUpPolicy.Verify(before,after,count,true).Succeeded);
        after.PackageCount++;Assert.False(MechaWarperUiTopUpPolicy.Verify(before,after,count,true).Succeeded);
    }
    [Theory]
    [InlineData(0,20,20)][InlineData(20,20,1)][InlineData(2,20,20)][InlineData(2,0,18)][InlineData(2,20,-1)]
    public void EmptyFullBlindOrUnfundedNativeTransferRefuses(int chamber,int package,int count)
        =>Assert.False(MechaWarperUiTopUpPolicy.Evaluate(Facts(chamber,package),count).Allowed);
    [Theory]
    [InlineData("foreign")][InlineData("moving")][InlineData("package-inc")][InlineData("chamber-inc")][InlineData("wrong-shape")]
    public void EveryNativeBindingAndZeroIncPremiseIsRequired(string fault)
    {
        var f=Facts(2,20);
        switch(fault){case "foreign":f.OwnedBindingVerified=false;break;case "moving":f.IsIdle=false;break;
            case "package-inc":f.PackageInc=1;break;case "chamber-inc":f.ChamberInc=1;break;case "wrong-shape":f.ChamberStackSize=21;break;}
        Assert.False(MechaWarperUiTopUpPolicy.Evaluate(f,18).Allowed);
    }
    private static MechaWarperSupplyFacts Facts(int chamber,int package)=>new() {OwnedBindingVerified=true,
        CurrentPlanetVerified=true,PlayerStateHashFresh=true,IsAlive=true,IsGrounded=true,ControllerEnabled=true,IsIdle=true,
        ChamberIsFiltered=true,ChamberSlotCount=1,ChamberGridCount=1,ChamberItemId=1210,ChamberFilterItemId=1210,
        ChamberStackSize=20,ChamberCount=chamber,PackageCount=package,AutoReplenishWarper=true};
}
