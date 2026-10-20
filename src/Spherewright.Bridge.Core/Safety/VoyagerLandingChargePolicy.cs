namespace Spherewright.Bridge.Core.Safety;

// A pending original flight, not an idle admission or a new recovery grant.
// Generation is deliberately absent: both the current and failure ticks must
// already be funded, and the complete original 13-tick loaded reserve remains.
public sealed class VoyagerLandingChargeFacts
{
    public VoyagerRemoteIdleAdmissionFacts? Admission { get; set; }
    public bool ContinuingWait { get; set; }
    public bool OriginalFlightActive { get; set; }
    public bool NativeGuardFresh { get; set; }
    public bool CostEnvelopeStable { get; set; }
    public string? EnvironmentBlocker { get; set; }
    public long ContactAtGameTick { get; set; }
    public long StableAtGameTick { get; set; }
    public long FlightDeadlineGameTick { get; set; }
    public DateTimeOffset NowUtc { get; set; }
    public DateTimeOffset ControlLeaseExpiresAtUtc { get; set; }
    public double CoreCapacityJoules { get; set; }
    public double ReturnCoreMinimumJoules { get; set; }
}

public static class VoyagerLandingChargePolicy
{
    public const string CoreBlocker = "remote_idle_core_stop_reserve_insufficient";

    public static bool NativeSequenceProved(long armedAtGameTick, long? lastPrePlayerGameTick,
        long currentGameTick, bool postPlayer)
    {
        if (armedAtGameTick < 0 || currentGameTick < armedAtGameTick
            || lastPrePlayerGameTick.HasValue && lastPrePlayerGameTick.Value <= armedAtGameTick) return false;
        if (postPlayer) return lastPrePlayerGameTick.HasValue && lastPrePlayerGameTick.Value == currentGameTick;
        var previous = lastPrePlayerGameTick ?? armedAtGameTick;
        var gap = (decimal)currentGameTick - previous;
        return gap > 0 && gap <= VoyagerControlDeadlinePolicy.MaximumControlGapGameTicks;
    }

    public static string? Blocker(VoyagerLandingChargeFacts f, long minimumStableGameTicks, long contactTimeoutGameTicks)
    {
        if (f is null) throw new ArgumentNullException(nameof(f));
        if (!f.OriginalFlightActive) return "voyager_landing_charge_original_flight_unproved";
        if (!f.NativeGuardFresh) return "voyager_landing_charge_pre_player_guard_unproved";
        if (!f.CostEnvelopeStable) return "voyager_landing_charge_cost_envelope_changed";
        if (f.EnvironmentBlocker is not null) return f.EnvironmentBlocker;
        if (f.Admission is null) return "voyager_landing_charge_capture_unproved";
        var admission = VoyagerRemoteIdleAdmissionPolicy.Evaluate(f.Admission);
        if (admission.BlockerCode != CoreBlocker && !(f.ContinuingWait && admission.Proved))
            return admission.BlockerCode ?? "voyager_landing_charge_entry_requires_core_blocker";

        var budget = f.Admission.JourneyBudget;
        if (budget is null || f.NowUtc == default || f.ControlLeaseExpiresAtUtc <= f.NowUtc
            || f.ControlLeaseExpiresAtUtc - f.NowUtc > TimeSpan.FromSeconds(VoyagerControlDeadlinePolicy.ControlLeaseSeconds)
            || minimumStableGameTicks < 1 || contactTimeoutGameTicks < minimumStableGameTicks
            || contactTimeoutGameTicks == long.MaxValue || f.ContactAtGameTick < 0
            || f.StableAtGameTick < f.ContactAtGameTick || budget.CurrentGameTick < f.StableAtGameTick
            || (decimal)budget.CurrentGameTick - f.StableAtGameTick < minimumStableGameTicks
            || (decimal)budget.CurrentGameTick - f.ContactAtGameTick > contactTimeoutGameTicks
            || budget.CurrentGameTick >= f.FlightDeadlineGameTick)
            return "voyager_landing_charge_original_window_exhausted";

        // ProjectSpent includes the entire current leg. LastLanded=current is
        // essential: adding the same landing wait a second time invents spend.
        if (budget.LastLandedGameTick != budget.CurrentGameTick)
            return "voyager_landing_charge_accounting_projection_unproved";
        var shape = VoyagerRecoveryWindowPolicy.ChargeBudgetBlocker(budget, f.NowUtc, budget.JourneyDeadlineUtc);
        if (shape is not null) return shape;
        var fullBudget = VoyagerLocalActionBudgetPolicy.Evaluate(budget);
        if (!fullBudget.Proved) return fullBudget.BlockerCode ?? "voyager_landing_charge_full_budget_unproved";
        var reserve = f.Admission.MovementReserve!;
        return VoyagerRecoveryWindowPolicy.ChargeBlocker(f.Admission.CoreEnergyJoules ?? double.NaN,
            f.CoreCapacityJoules, budget.AvailableLoadedEnergyJoules ?? double.NaN,
            budget.LockedReturnFloorJoules ?? double.NaN, reserve.PerNativeTickEnergyJoules ?? double.NaN,
            f.ReturnCoreMinimumJoules, budget.SpentGameTicks, 0, budget.MaximumGameTicks,
            budget.ReturnReserveGameTicks, budget.CurrentGameTick - f.ContactAtGameTick,
            contactTimeoutGameTicks + 1, 0d, (f.ControlLeaseExpiresAtUtc - f.NowUtc).TotalSeconds,
            f.NowUtc, budget.JourneyDeadlineUtc);
    }
}
