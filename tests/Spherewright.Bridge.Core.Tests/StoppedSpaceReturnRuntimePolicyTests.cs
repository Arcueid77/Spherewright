using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;
public sealed class StoppedSpaceReturnRuntimePolicyTests
{
    [Fact]
    public void LegalNativeSailFlyDriftWalkSequencePreservesOneBoundedHomeTask()
    {
        var f=Facts();
        foreach(var state in new[]{"Sail","Fly","Drift","Walk"})
        {f.MovementState=state;f.CurrentTick++;Assert.True(StoppedSpaceReturnRuntimePolicy.AllowsNextNativeTick(f));}
        Assert.Equal(20,f.StoredWarpers);Assert.Equal(54000,f.MaximumTicks);
    }
    [Theory]
    [InlineData("world")][InlineData("quarantine")][InlineData("ledger")][InlineData("observer")]
    [InlineData("foreign-order")][InlineData("star")][InlineData("cap")][InlineData("lease")][InlineData("deadline")]
    [InlineData("warp")][InlineData("unknown-warp")][InlineData("warper-debit")][InlineData("energy")][InlineData("2P")]
    public void EachRealRuntimeBoundaryFailureRequiresPauseBeforeAnotherNativePlayerTick(string fault)
    {
        var f=Facts();switch(fault){case "world":f.ExactNativeBinding=false;break;
            case "quarantine":f.ExactSourceQuarantine=false;break;case "ledger":f.ExactOriginalLedger=false;break;
            case "observer":f.NativeObserverBound=false;break;case "foreign-order":f.PlayerOrderAbsentOrOwned=false;break;
            case "star":f.CurrentStar=2;break;case "cap":f.CurrentTick=f.SourceTick+f.MaximumTicks;break;
            case "lease":f.ObservationLease=f.Now;break;case "deadline":f.Deadline=f.Now;break;
            case "warp":f.WarpCommand=true;break;case "unknown-warp":f.WarpState=null;break;
            case "warper-debit":f.StoredWarpers--;break;case "energy":f.LoadedEnergy=double.NaN;break;
            case "2P":f.Core=f.NextTickUpper;break;}
        Assert.False(StoppedSpaceReturnRuntimePolicy.AllowsNextNativeTick(f));
    }
    private static StoppedSpaceReturnRuntimeFacts Facts()
    {
        var now=DateTimeOffset.UtcNow;
        return new() {ExactNativeBinding=true,ExactSourceQuarantine=true,ExactOriginalLedger=true,
            NativeObserverBound=true,PlayerOrderAbsentOrOwned=true,MovementState="Sail",CurrentStar=1,HomeStar=1,
            SourceTick=1000,CurrentTick=1000,MaximumTicks=54000,Now=now,Deadline=now.AddHours(4),ObservationLease=now.AddSeconds(30),
            WarpCommand=false,Warping=false,WarpState=0,StoredWarpers=20,StartingWarpers=20,
            Core=1_600_000_000,LoadedEnergy=50_000_000_000,NextTickUpper=10_000_000,WithdrawalFloor=3_000_000_000};
    }
}
