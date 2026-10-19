using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerRecoveryWindowPolicyTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 2, 0, 0, 0, TimeSpan.Zero);
    [Fact]
    public void OneAbsoluteWindowIsBoundedByOriginalSpanAndCannotRenewByLaterValidation()
    {
        var p = new VoyagerRecoveryWindowProposal(Now.AddMinutes(30), VoyagerRecoveryWindowProposal.TaskScope, 9000, 240);
        var issued = Now.AddHours(-3); var original = issued.AddHours(1);
        Assert.Null(VoyagerRecoveryWindowPolicy.ProposalBlocker(p, Now, issued, original, 54000));
        Assert.Null(VoyagerRecoveryWindowPolicy.ProposalBlocker(p, Now.AddMinutes(1), issued, original, 54000));
        Assert.Equal(Now.AddMinutes(30), p.DeadlineUtc);
        Assert.NotNull(VoyagerRecoveryWindowPolicy.ProposalBlocker(p, p.DeadlineUtc, issued, original, 54000));
        Assert.NotNull(VoyagerRecoveryWindowPolicy.ProposalBlocker(new(Now.AddHours(2), p.Scope, 1, 1), Now, issued, original, 54000));
    }
    [Theory]
    [InlineData(0, 1)] [InlineData(18001, 1)] [InlineData(1, 0)] [InlineData(1, 301)] [InlineData(1, double.NaN)]
    public void InvalidOrLeaseExtendingChargeCapsRefuse(long ticks, double wall) =>
        Assert.NotNull(VoyagerRecoveryWindowPolicy.ProposalBlocker(new(Now.AddHours(1), VoyagerRecoveryWindowProposal.TaskScope, ticks, wall),
            Now, Now.AddHours(-3), Now.AddHours(-1), 54000));

    [Theory]
    [InlineData("missing")] [InlineData("warper")] [InlineData("binding")] [InlineData("stale")]
    [InlineData("tick")] [InlineData("energy")] [InlineData("wrong_clock")] [InlineData("missing_power")]
    public void EveryOriginalNonStopBudgetPremiseIsRequired(string fault)
    {
        var f = Budget();
        switch (fault)
        {
            case "warper": f.NativeWarperCount = 1; break;
            case "binding": f.ExactRemoteBinding = false; break;
            case "stale": f.NowUtc = Now.AddSeconds(-3); break;
            case "tick": f.LastLandedGameTick = 200; break;
            case "energy": f.AvailableLoadedEnergyJoules = null; break;
            case "wrong_clock": f.JourneyDeadlineUtc = Now; break;
            case "missing_power": f.WorkPowerUpperWatts = null; break;
        }
        Assert.NotNull(VoyagerRecoveryWindowPolicy.ChargeBudgetBlocker(fault == "missing" ? null : f, Now, Now.AddHours(1)));
    }
    [Fact]
    public void TwoTicksFundCurrentAndFollowingFailureWithoutCreditingGeneration()
    {
        Assert.Null(VoyagerRecoveryWindowPolicy.ChargeBudgetBlocker(Budget(), Now, Now.AddHours(1)));
        Assert.Null(Charge(core: 20));
        const double p = 10; var afterCurrent = 20 - p; var afterFailure = afterCurrent - p;
        Assert.Equal(0, afterFailure); Assert.NotNull(Charge(core: 19.999));
        Assert.NotNull(Charge(core: 20, loaded: 119.999));
        Assert.NotNull(Charge(core: 20, cap: 129)); // Strict13 can never fit.
        Assert.NotNull(Charge(core: 20, returnCore: 1001));
        Assert.NotNull(Charge(core: 20, spent: 99999));
    }
    [Theory]
    [InlineData(100, 0)] [InlineData(0, 10)]
    public void TickAndWallCapsExpireWithoutExtendingTheOldBudget(long charged, double wall) =>
        Assert.NotNull(VoyagerRecoveryWindowPolicy.ChargeBlocker(20, 1000, 1000, 100, 10, 800, 0, 0, 108000, 54000,
            charged, 100, wall, 10, Now, Now.AddHours(1)));
    private static string? Charge(double core, double loaded = 120, double cap = 1000, double returnCore = 800, long spent = 0) =>
        VoyagerRecoveryWindowPolicy.ChargeBlocker(core, cap, loaded, 100, 10, returnCore, spent, 0, 108000, 54000, 0, 100, 0, 10, Now, Now.AddHours(1));
    private static VoyagerLocalActionBudgetFacts Budget() => new()
    {
        ExactRemoteBinding = true, NativePlayerIdle = true, CurrentGameTick = 100, LastLandedGameTick = 50,
        SpentGameTicks = 1000, MaximumGameTicks = 108000, ReturnReserveGameTicks = 54000,
        WorkGameTicks = 1, TailGameTicks = 13, NowUtc = Now, JourneyDeadlineUtc = Now.AddHours(1),
        AvailableLoadedEnergyJoules = 1000, LockedReturnFloorJoules = 100, WorkPowerUpperWatts = 600,
        TailAndNextTickEnergyJoules = 130, NativeWarperCount = 2, OriginalExtraWarperReserve = 1,
    };
}
