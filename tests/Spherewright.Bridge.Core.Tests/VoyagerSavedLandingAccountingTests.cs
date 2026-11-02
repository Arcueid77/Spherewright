using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerSavedLandingAccountingTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static VoyagerLocalActionBudgetFacts Saved() => new()
    {
        ExactRemoteBinding = true, NativePlayerIdle = true, CurrentGameTick = 1210,
        SavedLandingAccountingAnchorGameTick = 1200, SavedLandingSourceBindingProved = true,
        SpentGameTicks = 200, MaximumGameTicks = 108000, ReturnReserveGameTicks = 54000,
        WorkGameTicks = 1, TailGameTicks = VoyagerLocalWalkReservePolicy.StopGameTicks,
        NowUtc = Now, JourneyDeadlineUtc = Now.AddMinutes(55),
        AvailableLoadedEnergyJoules = 100_000_000_000, LockedReturnFloorJoules = 5_000_000_000,
        WorkPowerUpperWatts = 1_000, TailAndNextTickEnergyJoules = 100_000,
        NativeWarperCount = 2, OriginalExtraWarperReserve = 1,
    };

    [Fact]
    public void SavedLandingUsesAnIndependentBudgetAnchorWithoutInventingAnOldLanding()
    {
        var f = Saved();
        Assert.True(VoyagerLocalActionBudgetPolicy.Evaluate(f).Proved);
        Assert.Null(VoyagerRecoveryWindowPolicy.ChargeBudgetBlocker(f, Now, f.JourneyDeadlineUtc));
        Assert.Null(f.LastLandedGameTick); Assert.Equal(200, f.SpentGameTicks);
        Assert.Equal(1200, VoyagerLocalActionBudgetPolicy.AccountingAnchorGameTick(f));
        f.MaximumGameTicks = 200 + 10 + 54000 + 1 + VoyagerLocalWalkReservePolicy.StopGameTicks - 1;
        Assert.Equal("remote_local_original_return_time_insufficient", VoyagerLocalActionBudgetPolicy.Evaluate(f).BlockerCode);
    }

    [Theory]
    [InlineData("no_binding")][InlineData("no_anchor")][InlineData("negative")]
    [InlineData("mixed_landing")][InlineData("future")][InlineData("foreign_runtime")]
    public void PartialMixedOrRegressedSavedAccountingCannotFundIdleOrCharging(string fault)
    {
        var f = Saved();
        switch (fault)
        {
            case "no_binding": f.SavedLandingSourceBindingProved = false; break;
            case "no_anchor": f.SavedLandingAccountingAnchorGameTick = null; break;
            case "negative": f.SavedLandingAccountingAnchorGameTick = -1; break;
            case "mixed_landing": f.LastLandedGameTick = 1200; break;
            case "future": f.SavedLandingAccountingAnchorGameTick = 1211; break;
            case "foreign_runtime": f.ExactRemoteBinding = false; break;
        }
        Assert.False(VoyagerLocalActionBudgetPolicy.Evaluate(f).Proved);
        Assert.NotNull(VoyagerRecoveryWindowPolicy.ChargeBudgetBlocker(f, Now, f.JourneyDeadlineUtc));
    }

    [Fact]
    public void LegacyLandingStillUsesItsOriginalAnchorAndDeadline()
    {
        var f = Saved(); f.SavedLandingAccountingAnchorGameTick = null; f.SavedLandingSourceBindingProved = false;
        f.LastLandedGameTick = 1200;
        Assert.True(VoyagerLocalActionBudgetPolicy.Evaluate(f).Proved);
        Assert.Null(VoyagerRecoveryWindowPolicy.ChargeBudgetBlocker(f, Now, f.JourneyDeadlineUtc));
        f.JourneyDeadlineUtc = Now.AddSeconds(900);
        Assert.False(VoyagerLocalActionBudgetPolicy.Evaluate(f).Proved);
    }
}
