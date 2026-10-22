namespace Spherewright.Bridge.Core.Safety;

// Copied, same-frame facts only. A successful flight may hand control to idle
// protection only after the same strict admission used by that protection.
public sealed class VoyagerRemoteIdleAdmissionFacts
{
    public string? NativeBlockerCode { get; set; }
    public VoyagerLocalWalkReserve? MovementReserve { get; set; }
    public double? CoreEnergyJoules { get; set; }
    public VoyagerLocalActionBudgetFacts? JourneyBudget { get; set; }
    // The separate return task owns its idle ticks. The exhausted old ledger
    // remains evidence; it is never projected into fresh travel allowance.
    public VoyagerReturnOnlyIdleBudgetFacts? ReturnOnlyBudget { get; set; }
}

public sealed class VoyagerRemoteIdleAdmissionDecision
{
    public bool Proved { get; internal set; }
    // The first failed predicate, in Native-capture, cost, wall, core, loaded,
    // then original-journey order. Never substitute a later generic failure.
    public string? BlockerCode { get; internal set; }
    public string? CaptureStage { get; internal set; }
    public string? CaptureExceptionType { get; internal set; }
    public Spherewright.Contracts.Players.NativeResumeFailureSnapshot? FailureEvidence { get; internal set; }
}

public static class VoyagerRemoteIdleAdmissionPolicy
{
    public const double StopWallSeconds = 1d;

    public static VoyagerRemoteIdleAdmissionDecision Blocked(string code,
        string? captureStage = null, string? captureExceptionType = null) => new()
        {
            BlockerCode = code,
            CaptureStage = captureStage,
            CaptureExceptionType = captureExceptionType,
        };

    public static VoyagerRemoteIdleAdmissionDecision Blocked(Spherewright.Contracts.Players.NativeResumeFailureSnapshot failure)
        => new() { BlockerCode = failure.Code, FailureEvidence = failure.Copy() };

    // The same projection is used before sealing and when committing the leg
    // accounting. Evaluating a handoff must not mutate or double-spend a leg.
    public static long? ProjectSpentGameTicks(long spentGameTicks, long startedGameTick,
        long observedGameTick, bool legAlreadyAccounted)
    {
        if (spentGameTicks < 0 || startedGameTick < 0 || observedGameTick < 0) return null;
        var projected = (decimal)spentGameTicks
            + (legAlreadyAccounted ? 0m : Math.Max(0m, (decimal)observedGameTick - startedGameTick));
        return projected <= long.MaxValue ? (long)projected : null;
    }

    public static VoyagerRemoteIdleAdmissionDecision Evaluate(VoyagerRemoteIdleAdmissionFacts facts, string captureStage)
    {
        var decision = Evaluate(facts);
        decision.CaptureStage = captureStage;
        return decision; // Preserve the same-decision failure; never reconstruct it from a code.
    }

    public static VoyagerRemoteIdleAdmissionDecision Evaluate(VoyagerRemoteIdleAdmissionFacts facts)
    {
        if (facts is null) throw new ArgumentNullException(nameof(facts));
        var budget = facts.JourneyBudget;
        var reserve = facts.MovementReserve;
        var e = new NativeResumeDecisionEvidence("native_idle_admission", budget?.CurrentGameTick, budget?.NowUtc, true);
        e.Number("core_j", facts.CoreEnergyJoules); e.Number("loaded_j", budget?.AvailableLoadedEnergyJoules);
        e.Number("return_floor_j", budget?.LockedReturnFloorJoules); e.Number("per_tick_j", reserve?.PerNativeTickEnergyJoules);
        e.Number("original_spent_ticks", budget?.SpentGameTicks); e.Number("original_maximum_ticks", budget?.MaximumGameTicks);
        e.Number("original_return_ticks", budget?.ReturnReserveGameTicks); e.Number("warpers", budget?.NativeWarperCount);
        e.NotReached("native_capture", "movement_cost", "journey_observed", "return_wall_reserved", "core_energy_observed",
            "loaded_energy_observed", "return_floor_observed", "tick_cost_observed", "stop_cost_finite", "core_stop_funded",
            "loaded_stop_funded", "native_stop_funding", "original_journey_budget");
        VoyagerRemoteIdleAdmissionDecision Fail(string code) => Blocked(e.Failure(code));
        VoyagerRemoteIdleAdmissionDecision Nested(Spherewright.Contracts.Players.NativeResumeFailureSnapshot failure)
        {
            var copied = e.Failure(failure.Code);
            foreach (var pair in copied.Values) if (!failure.Values.ContainsKey(pair.Key)) failure.Values[pair.Key] = pair.Value;
            foreach (var pair in copied.Predicates) if (!failure.Predicates.ContainsKey(pair.Key)) failure.Predicates[pair.Key] = pair.Value;
            return Blocked(failure);
        }
        if (!e.Require("native_capture", facts.NativeBlockerCode is null)) return Fail(facts.NativeBlockerCode!);
        if (!e.Require("movement_cost", reserve?.Proved)) return Fail(reserve?.BlockerCode ?? "remote_idle_movement_cost_unproved");
        if (!e.Require("journey_observed", budget is not null)) return Fail("remote_idle_journey_unproved");
        if (facts.ReturnOnlyBudget is { } returnOnly)
        {
            var failure = VoyagerReturnOnlyIdleBudget.TimeFailure(returnOnly, budget!);
            if (failure is not null) return Nested(failure);
        }
        else if (!e.Require("return_wall_reserved", (budget!.JourneyDeadlineUtc - budget.NowUtc).TotalSeconds
            > budget.ReturnReserveGameTicks / 60d + StopWallSeconds + 1d / 60d))
            return Fail("remote_idle_return_wall_reserve_insufficient");
        bool? Observed(double? value) => value.HasValue ? Known(value) : null;
        if (!e.Require("core_energy_observed", Observed(facts.CoreEnergyJoules))
            || !e.Require("loaded_energy_observed", Observed(budget!.AvailableLoadedEnergyJoules))
            || !e.Require("return_floor_observed", Observed(budget.LockedReturnFloorJoules))
            || !e.Require("tick_cost_observed", Observed(reserve!.PerNativeTickEnergyJoules)))
            return Fail("remote_idle_energy_unknown");
        var margin = reserve!.PerNativeTickEnergyJoules!.Value * VoyagerLocalWalkReservePolicy.StopGameTicks;
        var required = budget!.LockedReturnFloorJoules!.Value + margin;
        e.Number("stop_core_required_j", margin); e.Number("stop_loaded_required_j", required);
        if (!e.Require("stop_cost_finite", Finite(margin) && Finite(required))) return Fail("remote_idle_energy_overflow");
        if (!e.Require("core_stop_funded", facts.CoreEnergyJoules > 0d && facts.CoreEnergyJoules >= margin))
            return Fail("remote_idle_core_stop_reserve_insufficient");
        if (!e.Require("loaded_stop_funded", budget.AvailableLoadedEnergyJoules >= required))
            return Fail("remote_idle_loaded_stop_reserve_insufficient");
        if (!e.Require("native_stop_funding", VoyagerLocalWalkReservePolicy.CanFund(facts.CoreEnergyJoules, budget.AvailableLoadedEnergyJoules,
            budget.LockedReturnFloorJoules, reserve.PerNativeTickEnergyJoules, VoyagerLocalWalkReservePolicy.StopGameTicks)))
            return Fail("remote_idle_stop_reserve_unproved");
        if (facts.ReturnOnlyBudget is not null)
        {
            var failure = VoyagerReturnOnlyIdleBudget.ReserveFailure(facts.ReturnOnlyBudget, budget);
            return failure is null ? new() { Proved = true } : Nested(failure);
        }
        var journey = VoyagerLocalActionBudgetPolicy.Evaluate(budget);
        return e.Require("original_journey_budget", journey.Proved) ? new() { Proved = true }
            : Fail(journey.BlockerCode ?? "remote_idle_journey_unproved");
    }

    private static bool Known(double? value) => value.HasValue && Finite(value.Value);
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
}
