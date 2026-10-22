namespace Spherewright.Bridge.Core.Safety;

public sealed class VoyagerRecoveryWindowProposal
{
    public const string TaskScope = "continue_original_finite_task";
    public VoyagerRecoveryWindowProposal(DateTimeOffset deadlineUtc, string scope, long maximumChargeGameTicks, double maximumChargeWallSeconds)
    { DeadlineUtc = deadlineUtc; Scope = scope; MaximumChargeGameTicks = maximumChargeGameTicks; MaximumChargeWallSeconds = maximumChargeWallSeconds; }
    public DateTimeOffset DeadlineUtc { get; }
    public string Scope { get; }
    public long MaximumChargeGameTicks { get; }
    public double MaximumChargeWallSeconds { get; }
}

public sealed class VoyagerRecoveryAuthorization
{
    public VoyagerRecoveryAuthorization(string resumeActionId, string evidenceFingerprint, string confirmationDigest,
        DateTimeOffset confirmedAtUtc, VoyagerRecoveryWindowProposal proposal, string? priorConsumedResumeTokenHash = null,
        DateTimeOffset? inheritedWindowConfirmedAtUtc = null)
    { ResumeActionId = resumeActionId; EvidenceFingerprint = evidenceFingerprint; ConfirmationDigest = confirmationDigest;
      ConfirmedAtUtc = confirmedAtUtc; Proposal = proposal; PriorConsumedResumeTokenHash = priorConsumedResumeTokenHash;
      InheritedWindowConfirmedAtUtc = inheritedWindowConfirmedAtUtc; }
    public string ResumeActionId { get; }
    public string EvidenceFingerprint { get; }
    public string ConfirmationDigest { get; }
    public DateTimeOffset ConfirmedAtUtc { get; }
    public VoyagerRecoveryWindowProposal Proposal { get; }
    public string? PriorConsumedResumeTokenHash { get; }
    public DateTimeOffset? InheritedWindowConfirmedAtUtc { get; }
    public DateTimeOffset WindowOriginUtc() => InheritedWindowConfirmedAtUtc ?? ConfirmedAtUtc;
    // Json.NET convention: legacy authorizations retain their exact serialized shape.
    public bool ShouldSerializePriorConsumedResumeTokenHash() => PriorConsumedResumeTokenHash is not null;
    public bool ShouldSerializeInheritedWindowConfirmedAtUtc() => InheritedWindowConfirmedAtUtc.HasValue;
}

public static class VoyagerRecoveryWindowPolicy
{
    public const string ChargingPending = "remote_resume_charging_pending";
    public static string? ChargeBudgetBlocker(VoyagerLocalActionBudgetFacts? f, DateTimeOffset now, DateTimeOffset effectiveDeadline,
        NativeResumeDecisionEvidence? evidence = null)
    {
        bool All(params (string Name, bool Value)[] checks)
        {
            evidence?.NotReached(checks.Select(c => c.Name).ToArray());
            foreach (var check in checks)
                if (evidence is null ? !check.Value : !evidence.Require(check.Name, check.Value)) return false;
            return true;
        }
        if (!All(("charge_journey_observed", f is not null))) return "remote_resume_charge_original_budget_unproved";
        if (!All(("charge_journey_binding", f!.ExactRemoteBinding), ("charge_actual_idle", f.NativePlayerIdle),
            ("charge_landing_known", f.LastLandedGameTick.HasValue), ("charge_landing_nonnegative", f.LastLandedGameTick >= 0),
            ("charge_after_landing", f.CurrentGameTick >= f.LastLandedGameTick), ("charge_spent_valid", f.SpentGameTicks >= 0),
            ("charge_maximum_valid", f.MaximumGameTicks >= 1), ("charge_return_minimum", f.ReturnReserveGameTicks >= 3600),
            ("charge_return_maximum", f.ReturnReserveGameTicks <= 108000), ("charge_current_work", f.WorkGameTicks == 1),
            ("charge_original_tail", f.TailGameTicks == VoyagerLocalWalkReservePolicy.StopGameTicks),
            ("charge_extra_warpers_minimum", f.OriginalExtraWarperReserve >= 0), ("charge_extra_warpers_maximum", f.OriginalExtraWarperReserve <= 100),
            ("charge_sample_clock_known", f.NowUtc != default), ("charge_sample_not_future", now >= f.NowUtc),
            ("charge_sample_fresh", now - f.NowUtc <= TimeSpan.FromSeconds(2)), ("charge_deadline_bound", f.JourneyDeadlineUtc == effectiveDeadline),
            ("charge_loaded_known", f.AvailableLoadedEnergyJoules.HasValue && Finite(f.AvailableLoadedEnergyJoules.Value)),
            ("charge_floor_known", f.LockedReturnFloorJoules.HasValue && Finite(f.LockedReturnFloorJoules.Value)),
            ("charge_work_power_known", f.WorkPowerUpperWatts.HasValue && Finite(f.WorkPowerUpperWatts.Value)),
            ("charge_tail_cost_known", Finite(f.TailAndNextTickEnergyJoules))))
            return "remote_resume_charge_original_budget_unproved";
        if (!All(("charge_warpers_observed", f.NativeWarperCount.HasValue),
            ("charge_original_warpers_retained", f.NativeWarperCount >= 1 + f.OriginalExtraWarperReserve)))
            return "remote_resume_charge_original_warper_reserve_insufficient";
        return null;
    }
    public static string? ProposalBlocker(VoyagerRecoveryWindowProposal p, DateTimeOffset now, DateTimeOffset originalIssued,
        DateTimeOffset originalDeadline, long returnTicks)
    {
        if (p.Scope != VoyagerRecoveryWindowProposal.TaskScope || now == default || originalIssued == default
            || originalDeadline <= originalIssued || p.DeadlineUtc <= now
            || p.DeadlineUtc - now > originalDeadline - originalIssued
            || returnTicks < 3600 || (p.DeadlineUtc - now).TotalSeconds <= returnTicks / 60d + 1d + 1d / 60d
            || p.MaximumChargeGameTicks < 1 || p.MaximumChargeGameTicks > 18000
            || !Finite(p.MaximumChargeWallSeconds) || p.MaximumChargeWallSeconds <= 0d || p.MaximumChargeWallSeconds > 300d)
            return "remote_resume_recovery_window_invalid";
        return null;
    }

    public static string? ChargeBlocker(double core, double capacity, double loaded, double floor, double perTick,
        double returnCoreMinimum, long spent, long elapsed, long maximum, long returnTicks,
        long chargedTicks, long chargeTickCap, double chargedWall, double chargeWallCap,
        DateTimeOffset now, DateTimeOffset deadline, NativeResumeDecisionEvidence? evidence = null)
    {
        evidence?.Number("core_j", core); evidence?.Number("core_capacity_j", capacity);
        evidence?.Number("loaded_j", loaded); evidence?.Number("return_floor_j", floor);
        evidence?.Number("per_tick_j", perTick); evidence?.Number("original_return_core_min_j", returnCoreMinimum);
        evidence?.Number("spent_ticks", spent); evidence?.Number("elapsed_ticks", elapsed);
        evidence?.Number("maximum_ticks", maximum); evidence?.Number("return_ticks", returnTicks);
        evidence?.Number("charge_ticks_used", chargedTicks); evidence?.Number("charge_tick_cap", chargeTickCap);
        evidence?.Number("charge_wall_used_s", chargedWall); evidence?.Number("charge_wall_cap_s", chargeWallCap);
        evidence?.Number("return_wall_seconds", (deadline - now).TotalSeconds);
        bool All(params (string Name, bool Value)[] checks)
        {
            evidence?.NotReached(checks.Select(c => c.Name).ToArray());
            foreach (var check in checks)
                if (evidence is null ? !check.Value : !evidence.Require(check.Name, check.Value)) return false;
            return true;
        }
        if (!All(("charge_core_finite", Finite(core)), ("charge_capacity_finite", Finite(capacity)),
            ("charge_loaded_finite", Finite(loaded)), ("charge_floor_finite", Finite(floor)),
            ("charge_cost_finite", Finite(perTick)), ("charge_cost_positive", perTick > 0d),
            ("charge_return_core_finite", Finite(returnCoreMinimum)), ("charge_stop_cost_finite", Finite(13d * perTick)),
            ("charge_stop_goal_reachable", 13d * perTick <= capacity), ("charge_original_core_goal_reachable", returnCoreMinimum <= capacity)))
            return "remote_resume_charge_goal_or_cost_unproved";
        if (!All(("charge_current_and_failure_core_funded", core >= 2d * perTick),
            ("charge_current_and_failure_loaded_funded", loaded >= floor + 2d * perTick)))
            return "remote_resume_charge_current_and_failure_tick_unfunded";
        if (!All(("charge_spent_nonnegative", spent >= 0), ("charge_elapsed_nonnegative", elapsed >= 0),
            ("charge_original_return_minimum", returnTicks >= 3600), ("charge_used_nonnegative", chargedTicks >= 0),
            ("charge_ticks_remaining", chargedTicks < chargeTickCap), ("charge_wall_known", Finite(chargedWall)),
            ("charge_wall_remaining", chargedWall < chargeWallCap),
            ("charge_original_tick_reserve", (decimal)spent + elapsed + returnTicks + 2 <= maximum),
            ("charge_original_wall_reserve", (deadline - now).TotalSeconds > returnTicks / 60d + 1d + 1d / 60d)))
            return "remote_resume_charge_finite_budget_exhausted";
        return null;
    }
    private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x) && x >= 0d;
}
