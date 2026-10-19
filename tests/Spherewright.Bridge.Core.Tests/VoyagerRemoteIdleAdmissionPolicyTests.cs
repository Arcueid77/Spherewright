using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerRemoteIdleAdmissionPolicyTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LandingAndOngoingIdleUseTheSameProjectedOriginalAllowance()
    {
        var f = Good();
        var projected = VoyagerRemoteIdleAdmissionPolicy.ProjectSpentGameTicks(40, 100, 700, false);
        Assert.Equal(640, projected);
        f.JourneyBudget!.SpentGameTicks = projected!.Value;
        f.JourneyBudget.LastLandedGameTick = f.JourneyBudget.CurrentGameTick = 700;
        Assert.True(VoyagerRemoteIdleAdmissionPolicy.Evaluate(f).Proved);
        // Persisting the projection does not charge the same completed leg twice.
        Assert.Equal(projected, VoyagerRemoteIdleAdmissionPolicy.ProjectSpentGameTicks(projected.Value, 100, 700, true));
        f.JourneyBudget.CurrentGameTick++;
        Assert.True(VoyagerRemoteIdleAdmissionPolicy.Evaluate(f).Proved);
        f.JourneyBudget.MaximumGameTicks = 640 + 1 + 13 + f.JourneyBudget.ReturnReserveGameTicks;
        Assert.Equal("remote_local_original_return_time_insufficient", VoyagerRemoteIdleAdmissionPolicy.Evaluate(f).BlockerCode);
    }

    [Theory]
    [InlineData("remote_idle_scene_unproved")]
    [InlineData("remote_idle_movement_mode_unproved")]
    [InlineData("remote_idle_frame_movement_mode_unproved")]
    [InlineData("remote_idle_walk_manual_velocity_nonzero")]
    [InlineData("remote_idle_current_order_active")]
    [InlineData("remote_idle_construction_drones_active")]
    [InlineData("remote_idle_navigation_active")]
    public void AStableLandingCannotOverrideTheExactNativeIdleBlocker(string code)
    {
        var f = Good();
        f.NativeBlockerCode = code;
        // Even plentiful energy and a stable Walk observation cannot promote
        // a scene, movement, order or drone failure to a successful handoff.
        f.CoreEnergyJoules = f.JourneyBudget!.AvailableLoadedEnergyJoules = 20_000_000_000d;
        var decision = VoyagerRemoteIdleAdmissionPolicy.Evaluate(f);
        Assert.False(decision.Proved);
        Assert.Equal(code, decision.BlockerCode);
    }

    [Fact]
    public void LoadedFuelCannotSubstituteForThirteenTickCoreReserve()
    {
        var f = Good();
        f.JourneyBudget!.AvailableLoadedEnergyJoules = 20_000_000_000d;
        var margin = f.MovementReserve!.PerNativeTickEnergyJoules!.Value * 13;
        f.CoreEnergyJoules = margin - .01;
        Assert.Equal("remote_idle_core_stop_reserve_insufficient", VoyagerRemoteIdleAdmissionPolicy.Evaluate(f).BlockerCode);
        f.CoreEnergyJoules = margin;
        Assert.True(VoyagerRemoteIdleAdmissionPolicy.Evaluate(f).Proved);
    }

    [Fact]
    public void FullCoreCannotBorrowTheOriginalLoadedReturnFloor()
    {
        var f = Good();
        f.CoreEnergyJoules = 1_600_000_000;
        var floor = f.JourneyBudget!.LockedReturnFloorJoules!.Value;
        f.JourneyBudget.AvailableLoadedEnergyJoules = floor + f.MovementReserve!.StopEnergyJoules!.Value - .01;
        Assert.Equal("remote_idle_loaded_stop_reserve_insufficient", VoyagerRemoteIdleAdmissionPolicy.Evaluate(f).BlockerCode);
        // The original local-action policy additionally charges one work tick
        // and keeps its strict greater-than comparison at exact equality.
        f.JourneyBudget.AvailableLoadedEnergyJoules = floor + f.MovementReserve.StopEnergyJoules.Value
            + f.MovementReserve.WorkPowerUpperWatts!.Value / 60d;
        Assert.Equal("remote_local_original_return_floor_insufficient", VoyagerRemoteIdleAdmissionPolicy.Evaluate(f).BlockerCode);
        f.JourneyBudget.AvailableLoadedEnergyJoules++;
        Assert.True(VoyagerRemoteIdleAdmissionPolicy.Evaluate(f).Proved);
    }

    [Fact]
    public void IdleKeepsIndependentOneSecondCleanupAndOriginalUtcDeadline()
    {
        var f = Good();
        var seconds = f.JourneyBudget!.ReturnReserveGameTicks / 60d + 1d + 1d / 60d;
        f.JourneyBudget.JourneyDeadlineUtc = Now.AddSeconds(seconds - .001);
        Assert.Equal("remote_idle_return_wall_reserve_insufficient", VoyagerRemoteIdleAdmissionPolicy.Evaluate(f).BlockerCode);
        f.JourneyBudget.JourneyDeadlineUtc = Now.AddSeconds(seconds + .001);
        Assert.True(VoyagerRemoteIdleAdmissionPolicy.Evaluate(f).Proved);
    }

    [Theory]
    [InlineData("core_nan", "remote_idle_energy_unknown")]
    [InlineData("core_zero", "remote_idle_core_stop_reserve_insufficient")]
    [InlineData("loaded_unknown", "remote_idle_energy_unknown")]
    [InlineData("floor_infinite", "remote_idle_energy_unknown")]
    [InlineData("warper_missing", "remote_local_original_warper_reserve_missing")]
    [InlineData("landed_unknown", "remote_local_bounds_invalid")]
    [InlineData("cost_unknown", "remote_idle_movement_cost_unproved")]
    [InlineData("gravity_unknown", "native_potential_drift_cost_unproved")]
    public void MissingFactsCannotBecomeASuccessfulHandoff(string fault, string code)
    {
        var f = Good();
        switch (fault)
        {
            case "core_nan": f.CoreEnergyJoules = double.NaN; break;
            case "core_zero": f.CoreEnergyJoules = 0; break;
            case "loaded_unknown": f.JourneyBudget!.AvailableLoadedEnergyJoules = null; break;
            case "floor_infinite": f.JourneyBudget!.LockedReturnFloorJoules = double.PositiveInfinity; break;
            case "warper_missing": f.JourneyBudget!.NativeWarperCount = null; break;
            case "landed_unknown": f.JourneyBudget!.LastLandedGameTick = null; break;
            case "cost_unknown": f.MovementReserve = null; break;
            case "gravity_unknown":
                var native = MovementFacts(); native.NativeGravityUpperBound = null;
                f.MovementReserve = VoyagerLocalWalkReservePolicy.Evaluate(native); break;
        }
        var d = VoyagerRemoteIdleAdmissionPolicy.Evaluate(f);
        Assert.False(d.Proved); Assert.Equal(code, d.BlockerCode);
    }

    [Fact]
    public void CaptureFailureRetainsStageAndTypeWithoutAnExceptionMessage()
    {
        var d = VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_capture_failed", "construction_drones", "InvalidOperationException");
        Assert.False(d.Proved);
        Assert.Equal("construction_drones", d.CaptureStage);
        Assert.Equal("InvalidOperationException", d.CaptureExceptionType);
    }

    [Fact]
    public void NativeFailurePrecedesCostAndCoreFailuresDeterministically()
    {
        var f = Good(); f.NativeBlockerCode = "remote_idle_scene_unproved";
        f.MovementReserve = null; f.CoreEnergyJoules = double.NaN;
        Assert.Equal("remote_idle_scene_unproved", VoyagerRemoteIdleAdmissionPolicy.Evaluate(f).BlockerCode);
        f.NativeBlockerCode = null;
        Assert.Equal("remote_idle_movement_cost_unproved", VoyagerRemoteIdleAdmissionPolicy.Evaluate(f).BlockerCode);
    }

    [Fact]
    public void AccountingProjectionDoesNotWrapOrCreateCreditOnReversedTicks()
    {
        Assert.Null(VoyagerRemoteIdleAdmissionPolicy.ProjectSpentGameTicks(long.MaxValue, 0, 1, false));
        Assert.Null(VoyagerRemoteIdleAdmissionPolicy.ProjectSpentGameTicks(-1, 0, 1, false));
        Assert.Equal(50, VoyagerRemoteIdleAdmissionPolicy.ProjectSpentGameTicks(50, 100, 90, false));
        Assert.Equal(long.MaxValue, VoyagerRemoteIdleAdmissionPolicy.ProjectSpentGameTicks(long.MaxValue, 0, 1, true));
    }

    private static VoyagerRemoteIdleAdmissionFacts Good()
    {
        var reserve = VoyagerLocalWalkReservePolicy.Evaluate(MovementFacts());
        return new VoyagerRemoteIdleAdmissionFacts
        {
            MovementReserve = reserve, CoreEnergyJoules = 1_600_000_000,
            JourneyBudget = new VoyagerLocalActionBudgetFacts
            {
                ExactRemoteBinding = true, NativePlayerIdle = true,
                CurrentGameTick = 100, LastLandedGameTick = 100, SpentGameTicks = 10,
                MaximumGameTicks = 108000, ReturnReserveGameTicks = 54000,
                WorkGameTicks = 1, TailGameTicks = 13, NowUtc = Now, JourneyDeadlineUtc = Now.AddHours(1),
                AvailableLoadedEnergyJoules = 10_000_000_000, LockedReturnFloorJoules = 5_000_000_000,
                WorkPowerUpperWatts = reserve.WorkPowerUpperWatts,
                TailAndNextTickEnergyJoules = reserve.StopEnergyJoules!.Value,
                NativeWarperCount = 3, OriginalExtraWarperReserve = 1,
            },
        };
    }

    private static VoyagerLocalWalkFacts MovementFacts() => new()
    {
        ModeTransitionsExcluded = true, IndependentMechaCosts = NativeMechaBackgroundCostPolicyTests.Idle(),
        WalkSpeed = 12, WalkPowerWatts = 1000, MiningPowerWatts = 200, OtherWorkPowerWatts = 100,
        ReactorConsumptionRatio = 2, RtsSpeed = 0, ManualSpeed = 0, SoftLandingRecover = 1,
        IncludePotentialWaterDrift = true, NativeThrustPowerPerAcceleration = 300, NativeGravityUpperBound = 50,
    };
}
