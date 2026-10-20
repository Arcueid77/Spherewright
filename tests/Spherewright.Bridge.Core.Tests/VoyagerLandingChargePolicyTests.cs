using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerLandingChargePolicyTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static string? Blocker(VoyagerLandingChargeFacts f) => VoyagerLandingChargePolicy.Blocker(f, 600, 7200);

    [Theory]
    [InlineData(null, 100, true, false)]
    [InlineData(null, 101, true, false)]
    [InlineData(null, 100, false, false)]
    [InlineData(null, 101, false, true)]
    [InlineData(null, 102, false, true)]
    [InlineData(null, 103, false, false)]
    [InlineData(101L, 101, true, true)]
    [InlineData(101L, 102, true, false)]
    [InlineData(101L, 101, false, false)]
    [InlineData(101L, 100, false, false)]
    [InlineData(101L, 102, false, true)]
    [InlineData(101L, 104, false, false)]
    public void ArmingAfterPlayerCannotInventARealPrePlayerObservation(long? lastPre, long current, bool postPlayer, bool proved)
    {
        Assert.Equal(proved, VoyagerLandingChargePolicy.NativeSequenceProved(100, lastPre, current, postPlayer));
    }

    [Fact]
    public void FundedCoreShortfallCanWaitButCannotAdmitOrRequireReturnDepartureCore()
    {
        var f = Good();
        Assert.True(f.Admission!.CoreEnergyJoules < f.ReturnCoreMinimumJoules);
        Assert.Equal(VoyagerLandingChargePolicy.CoreBlocker,
            VoyagerRemoteIdleAdmissionPolicy.Evaluate(f.Admission).BlockerCode);
        Assert.Null(Blocker(f));
        Assert.False(VoyagerRemoteIdleAdmissionPolicy.Evaluate(f.Admission).Proved);
    }

    [Fact]
    public void FullThirteenTickAdmissionIsStillRequiredToFinish()
    {
        var f = Good(); var p = P(f);
        f.Admission!.CoreEnergyJoules = 13 * p - .01;
        Assert.Null(Blocker(f));
        Assert.False(VoyagerRemoteIdleAdmissionPolicy.Evaluate(f.Admission).Proved);
        f.Admission.CoreEnergyJoules = 13 * p;
        Assert.True(VoyagerRemoteIdleAdmissionPolicy.Evaluate(f.Admission).Proved);
        Assert.Equal("voyager_landing_charge_entry_requires_core_blocker", Blocker(f));
        f.ContinuingWait = true;
        Assert.Null(Blocker(f)); // Only the existing post601 path may now seal.
    }

    [Fact]
    public void TwoTickLoadedFundingCannotHideFailureOfTheFullOriginalTailBudget()
    {
        var f = Good(); var budget = f.Admission!.JourneyBudget!;
        budget.AvailableLoadedEnergyJoules = budget.LockedReturnFloorJoules + 3 * P(f);
        Assert.Equal(VoyagerLandingChargePolicy.CoreBlocker,
            VoyagerRemoteIdleAdmissionPolicy.Evaluate(f.Admission).BlockerCode);
        Assert.Equal("remote_local_original_return_floor_insufficient", Blocker(f));
        budget.AvailableLoadedEnergyJoules = budget.LockedReturnFloorJoules
            + budget.WorkPowerUpperWatts / 60 + budget.TailAndNextTickEnergyJoules;
        Assert.Equal("remote_local_original_return_floor_insufficient", Blocker(f));
        budget.AvailableLoadedEnergyJoules++;
        Assert.Null(Blocker(f));
    }

    [Theory]
    [InlineData(null, "remote_resume_charge_original_warper_reserve_insufficient")]
    [InlineData(1, "remote_resume_charge_original_warper_reserve_insufficient")]
    public void CoreFirstBlockerDoesNotProveTheLaterWarperGate(int? count, string code)
    {
        var f = Good(); f.Admission!.JourneyBudget!.NativeWarperCount = count;
        Assert.Equal(VoyagerLandingChargePolicy.CoreBlocker,
            VoyagerRemoteIdleAdmissionPolicy.Evaluate(f.Admission).BlockerCode);
        Assert.Equal(code, Blocker(f));
    }

    [Fact]
    public void FullWorkTailAndReturnTicksAreReservedEvenDuringCharge()
    {
        var f = Good(); var b = f.Admission!.JourneyBudget!;
        b.MaximumGameTicks = b.SpentGameTicks + b.ReturnReserveGameTicks + 13;
        Assert.Equal("remote_local_original_return_time_insufficient", Blocker(f));
        b.MaximumGameTicks++;
        Assert.Null(Blocker(f));
    }

    [Fact]
    public void CurrentAndFailureTicksMustBeFundedWithoutFutureGeneration()
    {
        var f = Good(); f.Admission!.CoreEnergyJoules = 2 * P(f);
        Assert.Null(Blocker(f));
        f.Admission.CoreEnergyJoules -= .01;
        Assert.Equal("remote_resume_charge_current_and_failure_tick_unfunded", Blocker(f));
    }

    [Theory]
    [InlineData("scene")]
    [InlineData("movement")]
    [InlineData("ground")]
    [InlineData("order")]
    public void AContinuingWaitCannotAbsorbAnUnrelatedNativeFailure(string predicate)
    {
        var f = Good(); f.ContinuingWait = true;
        f.Admission!.NativeBlockerCode = "test_native_" + predicate;
        Assert.Equal(f.Admission.NativeBlockerCode, Blocker(f));
    }

    [Theory]
    [InlineData("owner", "voyager_landing_charge_original_flight_unproved")]
    [InlineData("observer", "voyager_landing_charge_pre_player_guard_unproved")]
    [InlineData("cost", "voyager_landing_charge_cost_envelope_changed")]
    [InlineData("environment", "test_research_or_construction_changed")]
    public void LosingAControlPremiseCannotContinueTheOriginalWait(string fault, string code)
    {
        var f = Good(); f.ContinuingWait = true;
        switch (fault)
        {
            case "owner": f.OriginalFlightActive = false; break;
            case "observer": f.NativeGuardFresh = false; break;
            case "cost": f.CostEnvelopeStable = false; break;
            case "environment": f.EnvironmentBlocker = code; break;
        }
        Assert.Equal(code, Blocker(f));
        f.Admission!.CoreEnergyJoules = 13 * P(f); // Later funding cannot revive an invalid original owner.
        Assert.Equal(code, Blocker(f));
    }

    [Fact]
    public void OriginalStableAndContactClocksAreNotRestarted()
    {
        var f = Good(); var b = f.Admission!.JourneyBudget!;
        f.StableAtGameTick = b.CurrentGameTick - 599;
        Assert.Equal("voyager_landing_charge_original_window_exhausted", Blocker(f));
        f.StableAtGameTick--;
        Assert.Null(Blocker(f));
        b.CurrentGameTick = 7200; b.LastLandedGameTick = 7200;
        Assert.Null(Blocker(f)); // Existing Native timeout is strict >7200.
        b.CurrentGameTick = 7201; b.LastLandedGameTick = 7201;
        Assert.Equal("voyager_landing_charge_original_window_exhausted", Blocker(f));
    }

    [Fact]
    public void LeaseFlightAndOriginalReturnWallDeadlinesRemainAbsolute()
    {
        var f = Good(); f.ControlLeaseExpiresAtUtc = Now;
        Assert.Equal("voyager_landing_charge_original_window_exhausted", Blocker(f));
        f = Good(); f.ControlLeaseExpiresAtUtc = Now.AddSeconds(30).AddTicks(1);
        Assert.Equal("voyager_landing_charge_original_window_exhausted", Blocker(f));
        f = Good(); f.FlightDeadlineGameTick = f.Admission!.JourneyBudget!.CurrentGameTick;
        Assert.Equal("voyager_landing_charge_original_window_exhausted", Blocker(f));
        f = Good(); f.Admission!.JourneyBudget!.JourneyDeadlineUtc = Now.AddSeconds(900);
        Assert.Equal("remote_idle_return_wall_reserve_insufficient", Blocker(f));
    }

    [Fact]
    public void TheLegProjectionIncludesWaitingExactlyOnce()
    {
        var f = Good(); var b = f.Admission!.JourneyBudget!;
        b.SpentGameTicks = VoyagerRemoteIdleAdmissionPolicy.ProjectSpentGameTicks(200, 100, b.CurrentGameTick, false)!.Value;
        b.MaximumGameTicks = b.SpentGameTicks + b.ReturnReserveGameTicks + 14;
        Assert.Null(Blocker(f));
        b.LastLandedGameTick--;
        Assert.Equal("voyager_landing_charge_accounting_projection_unproved", Blocker(f));
    }

    [Theory]
    [InlineData("core", "remote_idle_energy_unknown")]
    [InlineData("capacity", "remote_resume_charge_goal_or_cost_unproved")]
    [InlineData("return_goal", "remote_resume_charge_goal_or_cost_unproved")]
    [InlineData("thirteen_tick_goal", "remote_resume_charge_goal_or_cost_unproved")]
    public void UnknownOrUnreachableFundingCannotStartAWait(string fault, string code)
    {
        var f = Good();
        switch (fault)
        {
            case "core": f.Admission!.CoreEnergyJoules = double.NaN; break;
            case "capacity": f.CoreCapacityJoules = double.PositiveInfinity; break;
            case "return_goal": f.ReturnCoreMinimumJoules = f.CoreCapacityJoules + 1; break;
            case "thirteen_tick_goal": f.CoreCapacityJoules = 13 * P(f) - 1; break;
        }
        Assert.Equal(code, Blocker(f));
    }

    private static double P(VoyagerLandingChargeFacts f) => f.Admission!.MovementReserve!.PerNativeTickEnergyJoules!.Value;

    private static VoyagerLandingChargeFacts Good()
    {
        var reserve = VoyagerLocalWalkReservePolicy.Evaluate(new VoyagerLocalWalkFacts
        {
            ModeTransitionsExcluded = true, IndependentMechaCosts = NativeMechaBackgroundCostPolicyTests.Idle(),
            WalkSpeed = 12, WalkPowerWatts = 1000, MiningPowerWatts = 200, OtherWorkPowerWatts = 100,
            ReactorConsumptionRatio = 2, RtsSpeed = 0, ManualSpeed = 0, SoftLandingRecover = 1,
            IncludePotentialWaterDrift = true, NativeThrustPowerPerAcceleration = 300, NativeGravityUpperBound = 50,
        });
        var p = reserve.PerNativeTickEnergyJoules!.Value;
        return new VoyagerLandingChargeFacts
        {
            OriginalFlightActive = true, NativeGuardFresh = true, CostEnvelopeStable = true,
            ContactAtGameTick = 0, StableAtGameTick = 100, FlightDeadlineGameTick = 20000,
            NowUtc = Now, ControlLeaseExpiresAtUtc = Now.AddSeconds(30),
            CoreCapacityJoules = 1_600_000_000, ReturnCoreMinimumJoules = 1_280_000_000,
            Admission = new VoyagerRemoteIdleAdmissionFacts
            {
                MovementReserve = reserve, CoreEnergyJoules = 3 * p,
                JourneyBudget = new VoyagerLocalActionBudgetFacts
                {
                    ExactRemoteBinding = true, NativePlayerIdle = true, CurrentGameTick = 1000,
                    LastLandedGameTick = 1000, SpentGameTicks = 200, MaximumGameTicks = 108000,
                    ReturnReserveGameTicks = 54000, WorkGameTicks = 1, TailGameTicks = 13,
                    NowUtc = Now, JourneyDeadlineUtc = Now.AddHours(1),
                    AvailableLoadedEnergyJoules = 5_000_000_000 + 100 * p, LockedReturnFloorJoules = 5_000_000_000,
                    WorkPowerUpperWatts = reserve.WorkPowerUpperWatts, TailAndNextTickEnergyJoules = reserve.StopEnergyJoules!.Value,
                    NativeWarperCount = 3, OriginalExtraWarperReserve = 1,
                },
            },
        };
    }
}
