namespace Spherewright.Bridge.Core.Safety;

// Copied, same-frame facts only. A successful flight may hand control to idle
// protection only after the same strict admission used by that protection.
public sealed class VoyagerRemoteIdleAdmissionFacts
{
    public string? NativeBlockerCode { get; set; }
    public VoyagerLocalWalkReserve? MovementReserve { get; set; }
    public double? CoreEnergyJoules { get; set; }
    public VoyagerLocalActionBudgetFacts? JourneyBudget { get; set; }
}

public sealed class VoyagerRemoteIdleAdmissionDecision
{
    public bool Proved { get; internal set; }
    // The first failed predicate, in Native-capture, cost, wall, core, loaded,
    // then original-journey order. Never substitute a later generic failure.
    public string? BlockerCode { get; internal set; }
    public string? CaptureStage { get; internal set; }
    public string? CaptureExceptionType { get; internal set; }
}

public static class VoyagerRemoteIdleAdmissionPolicy
{
    public const double StopWallSeconds = 1d;

    public static VoyagerRemoteIdleAdmissionDecision Blocked(string code,
        string? captureStage = null, string? captureExceptionType = null) => new()
    {
        BlockerCode = code, CaptureStage = captureStage, CaptureExceptionType = captureExceptionType,
    };

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

    public static VoyagerRemoteIdleAdmissionDecision Evaluate(VoyagerRemoteIdleAdmissionFacts facts)
    {
        if (facts is null) throw new ArgumentNullException(nameof(facts));
        if (facts.NativeBlockerCode is not null) return Blocked(facts.NativeBlockerCode);
        var reserve = facts.MovementReserve;
        if (reserve is null || !reserve.Proved)
            return Blocked(reserve?.BlockerCode ?? "remote_idle_movement_cost_unproved");
        var budget = facts.JourneyBudget;
        if (budget is null) return Blocked("remote_idle_journey_unproved");
        if ((budget.JourneyDeadlineUtc - budget.NowUtc).TotalSeconds
            <= budget.ReturnReserveGameTicks / 60d + StopWallSeconds + 1d / 60d)
            return Blocked("remote_idle_return_wall_reserve_insufficient");

        if (!Known(facts.CoreEnergyJoules) || !Known(budget.AvailableLoadedEnergyJoules)
            || !Known(budget.LockedReturnFloorJoules) || !Known(reserve.PerNativeTickEnergyJoules))
            return Blocked("remote_idle_energy_unknown");
        var margin = reserve.PerNativeTickEnergyJoules!.Value * VoyagerLocalWalkReservePolicy.StopGameTicks;
        var required = budget.LockedReturnFloorJoules!.Value + margin;
        if (!Finite(margin) || !Finite(required)) return Blocked("remote_idle_energy_overflow");
        if (facts.CoreEnergyJoules <= 0d || facts.CoreEnergyJoules < margin)
            return Blocked("remote_idle_core_stop_reserve_insufficient");
        if (budget.AvailableLoadedEnergyJoules < required)
            return Blocked("remote_idle_loaded_stop_reserve_insufficient");

        // Keep the existing 13-tick funding and strict original-journey energy
        // comparison. Core recharge is not waived at the success boundary.
        if (!VoyagerLocalWalkReservePolicy.CanFund(facts.CoreEnergyJoules, budget.AvailableLoadedEnergyJoules,
            budget.LockedReturnFloorJoules, reserve.PerNativeTickEnergyJoules, VoyagerLocalWalkReservePolicy.StopGameTicks))
            return Blocked("remote_idle_stop_reserve_unproved");
        var journey = VoyagerLocalActionBudgetPolicy.Evaluate(budget);
        return journey.Proved ? new VoyagerRemoteIdleAdmissionDecision { Proved = true }
            : Blocked(journey.BlockerCode ?? "remote_idle_journey_unproved");
    }

    private static bool Known(double? value) => value.HasValue && Finite(value.Value);
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
}
