using Spherewright.Bridge.Core.Safety;
using Xunit;
namespace Spherewright.Bridge.Core.Tests;
public sealed class NativeLandingTickEnergyPolicyTests
{
    [Fact]
    public void FlyDriftWalkShareARealTickBoundWithoutCallingAnIncomingFlyTailStrictWalkIdle()
    {
        var f=Facts();var cost=NativeLandingTickEnergyPolicy.Evaluate(f);Assert.NotNull(cost);
        Assert.InRange(cost!.Value,1,100_000_000);
        f.ObservedRtsSpeed=35;Assert.True(NativeLandingTickEnergyPolicy.Evaluate(f)>cost);
        f.VerticalSpeed=100;Assert.True(NativeLandingTickEnergyPolicy.Evaluate(f)>cost);
    }
    [Theory]
    [InlineData("input")][InlineData("jump")][InlineData("gravity")][InlineData("vertical")][InlineData("walk")]
    public void UnknownOrUnownedNativeCostsCannotProduceP(string reason)
    {
        var f=Facts();switch(reason){case "input":f.OwnedOrderAndNoManualInput=false;break;
            case "jump":f.JumpAndGuideExcluded=false;break;case "gravity":f.GravityUpper=null;break;
            case "vertical":f.VerticalSpeed=double.NaN;break;case "walk":f.WalkSpeed=0;break;}
        Assert.Null(NativeLandingTickEnergyPolicy.Evaluate(f));
    }
    private static NativeLandingTickEnergyFacts Facts()=>new() {OwnedOrderAndNoManualInput=true,JumpAndGuideExcluded=true,
        WalkSpeed=10,ObservedRtsSpeed=25,WalkPower=500000,OtherWorkPower=1110000,ThrustPowerPerAcceleration=24000,
        ConsumptionRatio=1,GravityUpper=300,VerticalSpeed=-32.9,IndependentEnergyPerTick=100000,PotentialJumpEnergy=900000};
}
