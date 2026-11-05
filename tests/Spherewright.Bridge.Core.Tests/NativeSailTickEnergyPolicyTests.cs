using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;
public sealed class NativeSailTickEnergyPolicyTests
{
    [Fact]
    public void FullCleanupFuelMarginIsNotOneNativeTickCoreCost()
    {
        var fuel=VoyagerControlReservePolicy.Evaluate(new() {AvailableLoadedEnergyJoules=50e9,ProtectedReturnFloorJoules=1.6e9,
            ThrustPowerPerAcceleration=24000,EnergyConsumptionCoefficient=1,NativeEnergyScale=1,OrdinarySpeedUpperBound=2000,
            WarpStartPowerPerSpeed=400,WarpKeepingPowerPerSpeed=80,MaxWarpSpeed=1e6,HasNativeWarpTail=false,
            ExtraMechaPowerUpperBoundWatts=1_610_000});
        Assert.Equal(369_420_000d,fuel.RequiredMarginJoules);
        var tick=NativeSailTickEnergyPolicy.Evaluate(Facts());Assert.True(tick.Proved,tick.Blocker);
        Assert.InRange(tick.PerNativeTickJoules!.Value,1,100_000_000);
        var f=Runtime();f.NextTickUpper=fuel.RequiredMarginJoules!.Value;
        Assert.Equal("space_return_core_two_native_ticks",StoppedSpaceReturnRuntimePolicy.FirstBlocker(f));
        var captured=StoppedSpaceReturnRuntimePolicy.CaptureFailure(f)!;
        Assert.Equal(738_840_000d,captured.Values["required_two_native_ticks_j"]);
        Assert.Equal(f.Core,captured.Values["core_j"]);
        f.NextTickUpper=tick.PerNativeTickJoules.Value;f.LoadedControlMargin=fuel.RequiredMarginJoules.Value;
        Assert.True(StoppedSpaceReturnRuntimePolicy.AllowsNextNativeTick(f));
        f.Core=2*f.NextTickUpper-1;
        Assert.Equal("space_return_core_two_native_ticks",StoppedSpaceReturnRuntimePolicy.FirstBlocker(f));
        f.Core=2*f.NextTickUpper;f.LoadedEnergy=f.WithdrawalFloor+f.LoadedControlMargin+2*f.NextTickUpper-1;
        Assert.Equal("space_return_loaded_control_reserve",StoppedSpaceReturnRuntimePolicy.FirstBlocker(f));
    }
    [Theory]
    [InlineData("planet",200d,1000d,200d,40d)]
    [InlineData("planet",2000d,2000d,200d,10d)]
    [InlineData("star",500d,1000d,300d,100d)]
    public void AtmosphereAndGravityBoundCoversNativeRadialAndTangentialBranches(string body,double speed,double radiusDistance,double bodyRadius,double gravity)
    {
        var f=Facts();f.BodyKind=body;f.RelativeHomeSpeed=speed;f.BodyDistance=radiusDistance;f.BodyRadius=bodyRadius;f.GravityUpper=gravity;
        var d=NativeSailTickEnergyPolicy.Evaluate(f);Assert.True(d.Proved,d.Blocker);
        var v=speed*(1+1e-5)+1+Math.Max(7,Math.Max(1,speed)*.02)+f.HomeToLocalBodyVelocity!.Value;
        var altitude=radiusDistance-bodyRadius;var target=body=="planet"?60d:300d;var factor=body=="planet"?.7d:2d;
        var desired=altitude>target?-(altitude-target)*.01:(target-altitude)*(body=="planet"?.5:.1);
        foreach(var radialFraction in new[]{-1d,-.5d,0d,.5d,1d})
        foreach(var affinity in new[]{0d,.5d,1d})
        {
            var radial=v*radialFraction;var tangent=Math.Sqrt(Math.Max(0,v*v-radial*radial));
            var a=.08*(1-affinity)*(1-affinity);
            var radialDelta=-radial*a+desired*a-tangent*tangent/radiusDistance/60*(1-affinity);
            var tangentDelta=(Math.Sqrt(gravity*radiusDistance*factor)-tangent)*a;
            var native=3*24000*(Math.Sqrt(radialDelta*radialDelta+tangentDelta*tangentDelta)*1.5+gravity/60);
            Assert.True(d.NativeSailJoules>=native);
        }
    }
    [Theory]
    [InlineData("input")][InlineData("affinity")][InlineData("unknown")][InlineData("geometry")][InlineData("float-range")]
    public void MissingOrUnsupportedNativePremisesNeverProduceP(string failure)
    {
        var f=Facts();switch(failure){case "input":f.ManualInputAndNavigationExcluded=false;break;
            case "affinity":f.NativeInputAffinitiesBound=false;break;case "unknown":f.GravityUpper=null;break;
            case "geometry":f.BodyDistance=0;break;case "float-range":f.RelativeHomeSpeed=1e20;break;}
        var d=NativeSailTickEnergyPolicy.Evaluate(f);Assert.False(d.Proved);Assert.Null(d.PerNativeTickJoules);
    }
    private static NativeSailTickEnergyFacts Facts()=>new() {NativeSailBranchBound=true,ManualInputAndNavigationExcluded=true,
        NativeInputAffinitiesBound=true,ThrustPowerPerAcceleration=24000,ConsumptionRatio=1,NativeEnergyScale=1,
        RelativeHomeSpeed=200,HomeToLocalBodyVelocity=10,MaximumSailSpeed=2000,BodyKind="planet",
        BodyDistance=1000,BodyRadius=200,GravityUpper=40,IndependentEnergyPerTick=100_000};
    private static StoppedSpaceReturnRuntimeFacts Runtime()
    {
        var now=DateTimeOffset.UtcNow;
        return new(){ExactNativeBinding=true,ExactSourceQuarantine=true,ExactOriginalLedger=true,NativeObserverBound=true,
            NativeControlAvailable=true,PlayerOrderAbsentOrOwned=true,MovementState="Sail",CurrentStar=1,HomeStar=1,
            SourceTick=1000,CurrentTick=3101,MaximumTicks=54000,Now=now,Deadline=now.AddHours(1),ObservationLease=now.AddSeconds(30),
            WarpCommand=false,Warping=false,WarpState=0,StoredWarpers=20,StartingWarpers=20,
            Core=738_823_189.0585988,LoadedEnergy=50e9,WithdrawalFloor=1.6e9};
    }
}
