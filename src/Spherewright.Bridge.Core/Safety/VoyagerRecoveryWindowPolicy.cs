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
        DateTimeOffset confirmedAtUtc, VoyagerRecoveryWindowProposal proposal)
    { ResumeActionId = resumeActionId; EvidenceFingerprint = evidenceFingerprint; ConfirmationDigest = confirmationDigest;
      ConfirmedAtUtc = confirmedAtUtc; Proposal = proposal; }
    public string ResumeActionId { get; }
    public string EvidenceFingerprint { get; }
    public string ConfirmationDigest { get; }
    public DateTimeOffset ConfirmedAtUtc { get; }
    public VoyagerRecoveryWindowProposal Proposal { get; }
}

public static class VoyagerRecoveryWindowPolicy
{
    public const string ChargingPending = "remote_resume_charging_pending";
    public static string? ChargeBudgetBlocker(VoyagerLocalActionBudgetFacts? f, DateTimeOffset now, DateTimeOffset effectiveDeadline)
    {
        if (f is null || !f.ExactRemoteBinding || !f.NativePlayerIdle || !f.LastLandedGameTick.HasValue
            || f.LastLandedGameTick < 0 || f.CurrentGameTick < f.LastLandedGameTick || f.SpentGameTicks < 0
            || f.MaximumGameTicks < 1 || f.ReturnReserveGameTicks < 3600 || f.ReturnReserveGameTicks > 108000
            || f.WorkGameTicks != 1 || f.TailGameTicks != VoyagerLocalWalkReservePolicy.StopGameTicks
            || f.OriginalExtraWarperReserve < 0 || f.OriginalExtraWarperReserve > 100
            || f.NowUtc == default || now < f.NowUtc || now - f.NowUtc > TimeSpan.FromSeconds(2)
            || f.JourneyDeadlineUtc != effectiveDeadline || !f.AvailableLoadedEnergyJoules.HasValue
            || !Finite(f.AvailableLoadedEnergyJoules.Value) || !f.LockedReturnFloorJoules.HasValue
            || !Finite(f.LockedReturnFloorJoules.Value) || !f.WorkPowerUpperWatts.HasValue
            || !Finite(f.WorkPowerUpperWatts.Value) || !Finite(f.TailAndNextTickEnergyJoules))
            return "remote_resume_charge_original_budget_unproved";
        if (!f.NativeWarperCount.HasValue || f.NativeWarperCount < 1 + f.OriginalExtraWarperReserve)
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
        DateTimeOffset now, DateTimeOffset deadline)
    {
        if (!Finite(core) || !Finite(capacity) || !Finite(loaded) || !Finite(floor) || !Finite(perTick)
            || perTick <= 0d || !Finite(returnCoreMinimum) || !Finite(13d * perTick)
            || 13d * perTick > capacity || returnCoreMinimum > capacity)
            return "remote_resume_charge_goal_or_cost_unproved";
        if (core < 2d * perTick || loaded < floor + 2d * perTick)
            return "remote_resume_charge_current_and_failure_tick_unfunded";
        if (spent < 0 || elapsed < 0 || returnTicks < 3600 || chargedTicks < 0 || chargedTicks >= chargeTickCap
            || !Finite(chargedWall) || chargedWall >= chargeWallCap
            || (decimal)spent + elapsed + returnTicks + 2 > maximum
            || (deadline - now).TotalSeconds <= returnTicks / 60d + 1d + 1d / 60d)
            return "remote_resume_charge_finite_budget_exhausted";
        return null;
    }
    private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x) && x >= 0d;
}
