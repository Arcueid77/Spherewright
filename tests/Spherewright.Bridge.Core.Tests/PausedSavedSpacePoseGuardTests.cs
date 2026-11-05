using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class PausedSavedSpacePoseGuardTests
{
    [Fact]
    public void PausedSavedSailPreservesNonzeroInertialMomentumWithoutClaimingGroundedIdle()
    {
        var world=new object();var player=new object();var controller=new object();var p=Pose();
        var guard=PausedSavedSpacePoseGuard.Capture(world,player,controller,1000,1000,"Sail",p);
        Assert.NotNull(guard);Assert.True(p.Speed>.5);
        Assert.True(guard!.Matches(world,player,controller,1000,"Sail",p));
        Assert.False(guard.Matches(world,player,controller,1000,"Walk",p));
        guard.Invalidate();Assert.False(guard.Matches(world,player,controller,1000,"Sail",p));
    }
    [Theory]
    [InlineData("world")][InlineData("player")][InlineData("controller")][InlineData("tick")]
    [InlineData("resume")][InlineData("position")][InlineData("momentum-direction")][InlineData("warp")]
    [InlineData("unknown")][InlineData("inconsistent-speed")]
    public void CompleteFrozenNativeCutRejectsEveryBindingOrMomentumChange(string fault)
    {
        var world=new object();var player=new object();var controller=new object();var p=Pose();
        var guard=PausedSavedSpacePoseGuard.Capture(world,player,controller,1000,1000,"Sail",p)!;long tick=1000;
        switch(fault){case "world":world=new();break;case "player":player=new();break;case "controller":controller=new();break;
            case "tick":tick++;break;case "resume":p.NativePaused=false;break;case "position":p.UniverseX+=.1;break;
            case "momentum-direction":p.VelocityZ=-p.VelocityZ;break;case "warp":p.WarpCommand=true;break;
            case "unknown":p.VelocityX=double.NaN;break;case "inconsistent-speed":p.Speed=0;break;}
        Assert.False(guard.Matches(world,player,controller,tick,"Sail",p));
    }
    internal static NativeSpaceTransitSnapshot Pose()=>new() {StarId=1,NativePaused=true,
        UniverseX=10,UniverseY=20,UniverseZ=30,VelocityZ=7.807988126320179,Speed=7.807988126320179};
}
