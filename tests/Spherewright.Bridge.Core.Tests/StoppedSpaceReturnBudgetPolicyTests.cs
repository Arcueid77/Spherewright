using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;
public sealed class StoppedSpaceReturnBudgetPolicyTests
{
    [Fact]
    public void CurrentSourceChargesAllObservedTicksAndKeepsOriginalReturnEnvelope()
    {
        var f=Facts();var d=StoppedSpaceReturnBudgetPolicy.Evaluate(f);
        Assert.True(d.Proved,d.Blocker);Assert.Equal(100,d.ConservedPriorSpent);
        Assert.Equal(54000,f.OriginalReturnTicks);Assert.Equal(2_400_000_000d,d.RequiredFlightEnergy);
        f.RecordedSpentTicks=120;Assert.Equal(120,StoppedSpaceReturnBudgetPolicy.Evaluate(f).ConservedPriorSpent);
    }
    [Theory]
    [InlineData("source-before-start")][InlineData("cap")][InlineData("wall")][InlineData("unknown-energy")]
    [InlineData("core-floor")][InlineData("ratio")][InlineData("distance")][InlineData("loaded-floor")][InlineData("2P")]
    public void UnknownOrUnfundedTransitionsNeverResumeSimulation(string fault)
    {
        var f=Facts();switch(fault){case "source-before-start":f.SourceTick=0;break;
            case "cap":f.RequestedTicks=54001;break;case "wall":f.RequestedWallSeconds=double.PositiveInfinity;break;
            case "unknown-energy":f.LoadedEnergy=double.NaN;break;case "core-floor":f.Core=1_270_000_000;break;
            case "ratio":f.MinimumCoreRatio=.79;break;case "distance":f.Distance=1e12;break;
            case "loaded-floor":f.LoadedEnergy=f.WithdrawalFloor+1;break;case "2P":f.NextTickEnergyUpper=f.Core;break;}
        Assert.False(StoppedSpaceReturnBudgetPolicy.Evaluate(f).Proved);
    }
    private static StoppedSpaceReturnBudgetFacts Facts()=>new() {SourceTick=1100,JourneyStartedTick=1000,
        RecordedSpentTicks=50,JourneyMaximumTicks=108000,OriginalReturnTicks=54000,RequestedTicks=54000,
        RequestedWallSeconds=14400,Distance=40000,SailSpeed=1000,Core=1_600_000_000,CoreCapacity=1_600_000_000,
        MinimumCoreRatio=.8,OriginalReturnCoreMinimum=1_280_000_000,LoadedEnergy=50_000_000_000,
        WithdrawalFloor=3_000_000_000,NextTickEnergyUpper=10_000_000};
}
