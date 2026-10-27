using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeSavedConstructionTailPolicyTests
{
    [Theory]
    [InlineData(30d,10d,700L,true)] [InlineData(40d,10d,700L,false)] [InlineData(40d,10d,752L,true)]
    public void SettledAnchorUsesOnlyTheObservedDistanceAndRemainingOriginalTail(double distance,double speed,long remaining,bool expected)
        => Assert.Equal(expected,NativeSavedConstructionTailPolicy.StableReturnFitsRemaining(distance,speed,remaining));
    [Theory]
    [InlineData(160f,10f)] [InlineData(200f,10f)] [InlineData(6000f,12f)]
    public void AlreadyReturningDronesRecycleInsideThePaidTailAcrossRadialAndTangentialStarts(float radius,float speed)
    {
        var q=radius+2.8f; var eject=new Vector3Snapshot { Y=q };
        for(var angle=0;angle<24;angle++)
        {
            var radians=angle*Math.PI/12d;
            var f=new NativeConstructionDroneMotionFacts { Stage=4,SpeedMetersPerSecond=speed,
                Eject=eject,Target=eject,Position=new UniversalPositionSnapshot { X=39.9*Math.Cos(radians),Y=q+39.9*Math.Sin(radians) } };
            var ticks=0;
            for(;ticks<NativeSavedConstructionTailPolicy.MaximumGameTicks;ticks++)
            {
                Assert.True(NativeSavedConstructionTailPolicy.CopiedReturnStepProved(f,radius,0,0,eject));
                if(NativeConstructionDroneReservePolicy.CopiedStepWouldNativeRecycle(f)) break;
                f.Position=NativeConstructionDroneReservePolicy.PredictCopiedOrdinaryStep(f)!;
            }
            Assert.InRange(ticks,1,NativeSavedConstructionTailPolicy.MaximumGameTicks-1);
        }
    }

    [Theory]
    [InlineData("outbound")] [InlineData("working")] [InlineData("anchor")]
    [InlineData("eject")] [InlineData("radius")] [InlineData("distance")] [InlineData("speed")]
    public void StopOnlyProofRejectsWorkStagesAndAnyLostMotionPremise(string fault)
    {
        var eject=new Vector3Snapshot { Y=202.8f }; var r=200d;var anchor=0d;var offset=0d;
        var f=new NativeConstructionDroneMotionFacts { Stage=4,SpeedMetersPerSecond=10,Eject=eject,Target=eject,Position=new() { X=4,Y=202.8 } };
        switch(fault)
        {
            case "outbound":f.Stage=1;break; case "working":f.Stage=3;break;
            case "anchor":anchor=.02501;break;case "eject":offset=.02601;break;
            case "radius":r=120;break;case "distance":f.Position.X=40.01;break;case "speed":f.SpeedMetersPerSecond=9.99f;break;
        }
        Assert.False(NativeSavedConstructionTailPolicy.CopiedReturnStepProved(f,r,anchor,offset,eject));
    }
}
