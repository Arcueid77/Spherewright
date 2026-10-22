using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Safety;

// Copied server task identity/bounds, never a client supplied safety claim.
public sealed class VoyagerReturnOnlyIdleBudgetFacts
{
    public bool? ExactTaskBinding { get; set; }
    public long StartedAtGameTick { get; set; }
    public long MaximumGameTicks { get; set; }
    public DateTimeOffset DeadlineUtc { get; set; }
}

public static class VoyagerReturnOnlyIdleBudget
{
    public static NativeResumeFailureSnapshot? TimeFailure(VoyagerReturnOnlyIdleBudgetFacts t, VoyagerLocalActionBudgetFacts f)
    {
        var e = Evidence(t, f);
        e.NotReached("exact_return_task", "task_cap_valid", "task_tick_observed", "next601_and_stop_reserved",
            "return_wall_reserved", "native_idle", "original_warper_reserve", "loaded_energy_observed", "original_loaded_floor");
        var remaining = VoyagerReturnOnlyRecoveryPolicy.RemainingTicks(t.StartedAtGameTick, t.MaximumGameTicks, f.CurrentGameTick);
        var next = f.CurrentGameTick < long.MaxValue
            ? VoyagerReturnOnlyRecoveryPolicy.RemainingTicks(t.StartedAtGameTick, t.MaximumGameTicks, f.CurrentGameTick + 1) : null;
        var flight = VoyagerReturnOnlyRecoveryPolicy.FlightTicks(next, f.ReturnReserveGameTicks);
        var seconds = (t.DeadlineUtc - f.NowUtc).TotalSeconds - VoyagerControlDeadlinePolicy.StopCleanupSeconds;
        e.Number("new_remaining_ticks", remaining); e.Number("flight_ticks_after_next601", flight);
        e.Number("new_remaining_wall_seconds", seconds);
        if (!e.Require("exact_return_task", t.ExactTaskBinding)
            || !e.Require("task_cap_valid", t.MaximumGameTicks >= VoyagerReturnOnlyRecoveryPolicy.MinimumTotalGameTicks
                && t.MaximumGameTicks <= VoyagerReturnOnlyRecoveryPolicy.MaximumGameTicks)
            || !e.Require("task_tick_observed", remaining.HasValue)
            || !e.Require("next601_and_stop_reserved", flight >= 3600)
            || !e.Require("return_wall_reserved", t.DeadlineUtc.Offset == TimeSpan.Zero && f.NowUtc != default
                && seconds > flight / 60d + VoyagerRemoteIdleAdmissionPolicy.StopWallSeconds + 1d / 60d))
            return e.Failure("return_only_idle_time_unproved");
        return null;
    }

    public static NativeResumeFailureSnapshot? ReserveFailure(VoyagerReturnOnlyIdleBudgetFacts t, VoyagerLocalActionBudgetFacts f)
    {
        var e = Evidence(t, f);
        e.NotReached("native_binding", "native_idle", "original_warper_reserve", "loaded_energy_observed",
            "return_floor_observed", "work_cost_observed", "tail_cost_observed", "original_loaded_floor");
        if (!e.Require("native_binding", f.ExactRemoteBinding) || !e.Require("native_idle", f.NativePlayerIdle)
            || !e.Require("original_warper_reserve", f.NativeWarperCount.HasValue
                ? f.OriginalExtraWarperReserve >= 0 && f.NativeWarperCount >= 1 + f.OriginalExtraWarperReserve : null))
            return e.Failure("return_only_idle_native_reserve_unproved");
        bool Known(double? v) => v.HasValue && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value) && v >= 0;
        bool? Observed(double? v) => v.HasValue ? Known(v) : null;
        if (!e.Require("loaded_energy_observed", Observed(f.AvailableLoadedEnergyJoules))
            || !e.Require("return_floor_observed", Observed(f.LockedReturnFloorJoules))
            || !e.Require("work_cost_observed", Observed(f.WorkPowerUpperWatts))
            || !e.Require("tail_cost_observed", Known(f.TailAndNextTickEnergyJoules)))
            return e.Failure("return_only_idle_energy_unknown");
        var required = f.LockedReturnFloorJoules!.Value + f.WorkPowerUpperWatts!.Value / 60d + f.TailAndNextTickEnergyJoules;
        e.Number("required_loaded_j", required); e.Number("loaded_j", f.AvailableLoadedEnergyJoules);
        return e.Require("original_loaded_floor", Known(required) && f.AvailableLoadedEnergyJoules > required)
            ? null : e.Failure("return_only_idle_original_floor_insufficient");
    }

    private static NativeResumeDecisionEvidence Evidence(VoyagerReturnOnlyIdleBudgetFacts t, VoyagerLocalActionBudgetFacts f)
    {
        var e = new NativeResumeDecisionEvidence("return_only_native_idle", f.CurrentGameTick, f.NowUtc, true);
        e.Number("task_start_tick", t.StartedAtGameTick); e.Number("new_tick_cap", t.MaximumGameTicks);
        e.Number("original_spent_ticks", f.SpentGameTicks); e.Number("original_maximum_ticks", f.MaximumGameTicks);
        e.Number("original_return_limit_ticks", f.ReturnReserveGameTicks);
        e.Number("reserved_stop_and_entered601_ticks", VoyagerReturnOnlyRecoveryPolicy.StopAndTailGameTicks);
        return e;
    }
}
