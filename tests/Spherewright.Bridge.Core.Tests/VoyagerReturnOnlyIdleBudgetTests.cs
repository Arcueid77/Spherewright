using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerReturnOnlyIdleBudgetTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static VoyagerReturnOnlyIdleBudgetFacts Task() => new()
    {
        ExactTaskBinding = true,
        StartedAtGameTick = 100,
        MaximumGameTicks = 54000,
        DeadlineUtc = Now.AddMinutes(20)
    };
    private static VoyagerLocalActionBudgetFacts Legacy() => new()
    {
        ExactRemoteBinding = true,
        NativePlayerIdle = true,
        CurrentGameTick = 139,
        LastLandedGameTick = 100,
        SpentGameTicks = 108000,
        MaximumGameTicks = 108000,
        ReturnReserveGameTicks = 54000,
        WorkGameTicks = 1,
        TailGameTicks = 13,
        NowUtc = Now,
        JourneyDeadlineUtc = Now.AddDays(-1),
        NativeWarperCount = 2,
        OriginalExtraWarperReserve = 1,
        AvailableLoadedEnergyJoules = 10e9,
        LockedReturnFloorJoules = 5e9,
        WorkPowerUpperWatts = 1000,
        TailAndNextTickEnergyJoules = 13000
    };

    [Fact]
    public void SeparateTaskOwnsIdleWithoutRefundingTheExhaustedOriginalLedger()
    {
        var f = Legacy(); var t = Task();
        Assert.False(VoyagerLocalActionBudgetPolicy.Evaluate(f).Proved);
        Assert.Null(VoyagerReturnOnlyIdleBudget.TimeFailure(t, f));
        Assert.Null(VoyagerReturnOnlyIdleBudget.ReserveFailure(t, f));
        Assert.Equal(108000, f.SpentGameTicks); Assert.Equal(108000, f.MaximumGameTicks);
        Assert.Equal(Now.AddDays(-1), f.JourneyDeadlineUtc); Assert.Equal(54000, f.ReturnReserveGameTicks);
        Assert.Equal(53961, VoyagerReturnOnlyRecoveryPolicy.RemainingTicks(t.StartedAtGameTick, t.MaximumGameTicks, f.CurrentGameTick));
    }

    [Theory]
    [InlineData("binding_false", "exact_return_task", "false")]
    [InlineData("binding_unknown", "exact_return_task", "unknown")]
    [InlineData("over_cap", "task_cap_valid", "false")]
    [InlineData("backward", "task_tick_observed", "false")]
    [InlineData("exhausted", "next601_and_stop_reserved", "false")]
    [InlineData("wall", "return_wall_reserved", "false")]
    public void InvalidOrUnknownTaskCannotBorrowAnOldSuccessfulBudget(string change, string predicate, string truth)
    {
        var f = Legacy(); var t = Task();
        switch (change)
        {
            case "binding_false": t.ExactTaskBinding = false; break;
            case "binding_unknown": t.ExactTaskBinding = null; break;
            case "over_cap": t.MaximumGameTicks++; break;
            case "backward": f.CurrentGameTick = 99; break;
            case "exhausted": f.CurrentGameTick = 100 + 54000 - VoyagerReturnOnlyRecoveryPolicy.MinimumTotalGameTicks; break;
            case "wall": t.DeadlineUtc = Now.AddSeconds(30); break;
        }
        var failure = Assert.IsType<Spherewright.Contracts.Players.NativeResumeFailureSnapshot>(VoyagerReturnOnlyIdleBudget.TimeFailure(t, f));
        Assert.Equal(predicate, failure.FirstFailedPredicate); Assert.Equal(truth, failure.Predicates[predicate]);
        Assert.Equal("not_reached", failure.Predicates["native_idle"]);
        Assert.Equal(108000d, failure.Values["original_spent_ticks"]);
    }

    [Fact]
    public void Entered601AndStopStayInsideTheSameTotal()
    {
        var f = Legacy(); var t = Task();
        f.CurrentGameTick = 100 + 54000 - VoyagerReturnOnlyRecoveryPolicy.MinimumTotalGameTicks - 1;
        Assert.Null(VoyagerReturnOnlyIdleBudget.TimeFailure(t, f));
        f.CurrentGameTick++;
        Assert.Equal("next601_and_stop_reserved", VoyagerReturnOnlyIdleBudget.TimeFailure(t, f)!.FirstFailedPredicate);
    }

    [Theory]
    [InlineData("warper", "original_warper_reserve", "unknown")]
    [InlineData("idle", "native_idle", "false")]
    [InlineData("loaded", "loaded_energy_observed", "unknown")]
    [InlineData("floor", "original_loaded_floor", "false")]
    public void NativeReservesStillFailClosed(string change, string predicate, string truth)
    {
        var f = Legacy(); var t = Task();
        switch (change)
        {
            case "warper": f.NativeWarperCount = null; break;
            case "idle": f.NativePlayerIdle = false; break;
            case "loaded": f.AvailableLoadedEnergyJoules = null; break;
            case "floor": f.AvailableLoadedEnergyJoules = 5e9; break;
        }
        var failure = VoyagerReturnOnlyIdleBudget.ReserveFailure(t, f)!;
        Assert.Equal(predicate, failure.FirstFailedPredicate); Assert.Equal(truth, failure.Predicates[predicate]);
    }

    [Fact]
    public void BootstrapFundsContactAndTheEntered601WithoutClaimingStrictIdleOrRefundingOldTime()
    {
        var f = Legacy(); var t = Task(); f.NativePlayerIdle = false;
        var cost = VoyagerLocalWalkReservePolicy.Evaluate(new()
        {
            ModeTransitionsExcluded = true,
            IndependentMechaCosts = NativeMechaBackgroundCostPolicyTests.Idle(),
            WalkSpeed = 12,
            WalkPowerWatts = 1000,
            MiningPowerWatts = 200,
            OtherWorkPowerWatts = 100,
            ReactorConsumptionRatio = 2,
            RtsSpeed = 0,
            ManualSpeed = 0,
            SoftLandingRecover = 1,
            IncludePotentialWaterDrift = true,
            NativeThrustPowerPerAcceleration = 300,
            NativeGravityUpperBound = 50
        });
        Assert.True(cost.Proved); var perTick = cost.PerNativeTickEnergyJoules!.Value;
        f.WorkPowerUpperWatts = cost.WorkPowerUpperWatts; f.TailAndNextTickEnergyJoules = cost.StopEnergyJoules!.Value;
        var core = NativeResumeContactPolicy.FundedTicks * perTick;
        Assert.Null(VoyagerReturnOnlyIdleBudget.BootstrapFailure(t, f, core, perTick, NativeResumeContactPolicy.FundedTicks));
        Assert.Equal("native_idle", VoyagerReturnOnlyIdleBudget.ReserveFailure(t, f)!.FirstFailedPredicate);
        // Declared Native601 energy contract: an entered frame finishes after Pause.
        core -= perTick;
        Assert.Equal(VoyagerLocalWalkReservePolicy.StopGameTicks * perTick, core, 8);
        var failed = VoyagerReturnOnlyIdleBudget.BootstrapFailure(t, f, core, perTick, NativeResumeContactPolicy.FundedTicks)!;
        Assert.Equal("contact_stop_funded", failed.FirstFailedPredicate);
        Assert.Equal("return_only_native_bootstrap", failed.Stage);
        Assert.Equal("not_reached", failed.Predicates["strict_idle"]);
        Assert.Equal(108000, f.SpentGameTicks); Assert.Equal(Now.AddDays(-1), f.JourneyDeadlineUtc);
    }

    [Theory]
    [InlineData("core", "core_observed", "unknown")]
    [InlineData("cost", "tick_cost_observed", "unknown")]
    [InlineData("warper", "original_warper_reserve", "unknown")]
    [InlineData("loaded", "loaded_energy_observed", "unknown")]
    [InlineData("window", "return_wall_reserved", "false")]
    [InlineData("ticks", "next601_and_stop_reserved", "false")]
    [InlineData("binding", "exact_return_task", "unknown")]
    public void BootstrapCannotBorrowUnknownResourcesOrAnExpiredTask(string fault, string predicate, string truth)
    {
        var f = Legacy(); var t = Task(); f.NativePlayerIdle = false; double? core = 1e8, cost = 2;
        switch (fault)
        {
            case "core": core = null; break;
            case "cost": cost = null; break;
            case "warper": f.NativeWarperCount = null; break;
            case "loaded": f.AvailableLoadedEnergyJoules = null; break;
            case "window": t.DeadlineUtc = Now.AddSeconds(-1); break;
            case "ticks": f.CurrentGameTick = t.StartedAtGameTick + t.MaximumGameTicks; break;
            case "binding": t.ExactTaskBinding = null; break;
        }
        var failed = VoyagerReturnOnlyIdleBudget.BootstrapFailure(t, f, core, cost, NativeResumeContactPolicy.FundedTicks)!;
        Assert.Equal(predicate, failed.FirstFailedPredicate); Assert.Equal(truth, failed.Predicates[predicate]);
        Assert.Equal("return_only_native_bootstrap", failed.Stage);
        Assert.Equal(f.CurrentGameTick, failed.CapturedAtGameTick);
    }
}
