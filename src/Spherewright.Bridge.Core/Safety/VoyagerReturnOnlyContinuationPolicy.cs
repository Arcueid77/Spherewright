using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Bridge.Core.Safety;

// File/provenance admission only. No Native health or Load authority is created.
public sealed class VoyagerReturnOnlyContinuationFacts
{
    public bool? ExactOwnedNative { get; set; }
    public bool? ExactTaskCredentialSession { get; set; }
    public bool? OriginalCheckpointWitness { get; set; }
    public bool? JournalBound { get; set; }
    public bool? NoPendingRecovery { get; set; }
    public bool? RemoteReturnPhase { get; set; }
    public bool? CompleteSourceFiles { get; set; }
    public long? SourceGameTick { get; set; }
    public long? PrimaryGameTick { get; set; }
    public long? CredentialMinimumGameTick { get; set; }
    public long? PreservedBoundaryGameTick { get; set; }
    public bool? PreservedBoundaryProved { get; set; }
    public long TaskStartedAtGameTick { get; set; }
    public long OriginalMaximumGameTicks { get; set; }
    public long OriginalReturnLimitGameTicks { get; set; }
    public DateTimeOffset OriginalDeadlineUtc { get; set; }
    public DateTimeOffset? ProposedWallDeadlineUtc { get; set; }
    public DateTimeOffset NowUtc { get; set; }
}

public sealed class VoyagerReturnOnlyContinuationDecision
{
    public bool SourceEligible { get; internal set; }
    public long? RemainingGameTicks { get; internal set; }
    public DateTimeOffset? EffectiveDeadlineUtc { get; internal set; }
    public bool RequiresNewWallAuthority { get; internal set; }
    public NativeResumeFailureSnapshot? FirstFailure { get; internal set; }
}

public static class VoyagerReturnOnlyContinuationPolicy
{
    public const string Scope = "recover_and_return_only";
    // A separately authorized wall window does not change the existing task's
    // tick grant, original UTC terms, resource floors or Native phase limits.
    public const int MaximumWallSeconds = 3600;

    public static NativeResumeFailureSnapshot? EchoFailure(PrepareOwnedWorldResumeRequest r,
        PreparedOwnedWorldResumePlan? p, DateTimeOffset now)
    {
        var e = new NativeResumeDecisionEvidence("return_only_continuation_echo", null, now, true);
        var q = p?.ReturnOnlyContinuation;
        e.Number("requested_source_tick", r.ExpectedRecoveryGameTick);
        e.Number("returned_source_tick", q?.SourceGameTick);
        e.Number("original_task_maximum_ticks", q?.OriginalMaximumGameTicks);
        e.Number("remaining_task_ticks", q?.RemainingGameTicks);
        e.Number("remaining_wall_seconds", p?.EffectiveRecoveryDeadlineUtc is { } deadline
            ? (deadline - now).TotalSeconds : null);
        e.Number("requested_charge_ticks", r.MaximumChargeGameTicks);
        e.Number("returned_charge_ticks", p?.MaximumChargeGameTicks);
        e.Number("returned_charge_seconds", p?.MaximumChargeWallSeconds);
        var checks = new (string Name, bool? Value)[] {
            ("exact_return_continuation_request", RequestFailure(r) is null),
            ("returned_plan_observed", p is not null),
            ("prepared", p?.Prepared),
            ("exact_mode", p is null ? null : p.RecoveryMode == r.RecoveryMode),
            ("typed_evidence_version", p is null ? null : p.RecoveryEvidenceVersion == VoyagerRemoteResumePolicy.EvidenceVersion),
            ("single_use_plan_observed", p is null ? null : !string.IsNullOrWhiteSpace(p.PlanToken)),
            ("exact_source_tick", p is null ? null : p.CandidateGameTick.HasValue
                ? p.CandidateGameTick == r.ExpectedRecoveryGameTick && p.MinimumGameTick == r.ExpectedRecoveryGameTick : null),
            ("original_journey_window_observed", p is null ? null : p.OriginalJourneyDeadlineUtc.HasValue),
            ("exact_return_only_scope", p is null ? null : p.RecoveryScope == Scope),
            ("no_charging_grant", p is null ? null : p.NewChargingProhibited && p.MaximumChargeGameTicks == 0
                && p.MaximumChargeWallSeconds == 0 && !p.NativeChargeFeasibilityRequiresFreshCapture),
            ("exact_native_version", p is null ? null : !string.IsNullOrEmpty(p.SourceGameVersion)
                && p.SourceGameVersion == p.TargetGameVersion),
            ("exact_embedded_identity", p?.ExactEmbeddedIdentityVerified),
            ("digest_confirmation_required", p is null ? null : p.UserConfirmationRequired && !p.CommitAllowedNow
                && OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(p.ConfirmationDigest)),
            ("typed_return_preview_observed", p is null ? null : q is not null),
            ("original_task_observed", q is null ? null : !string.IsNullOrWhiteSpace(q.OriginalTaskId)),
            ("exact_preview_source_tick", q is null ? null : q.SourceGameTick == r.ExpectedRecoveryGameTick),
            ("original_tick_cap", q is null ? null : q.OriginalMaximumGameTicks >= VoyagerReturnOnlyRecoveryPolicy.MinimumTotalGameTicks
                && q.OriginalMaximumGameTicks <= VoyagerReturnOnlyRecoveryPolicy.MaximumGameTicks),
            ("remaining_original_ticks", q is null ? null : q.RemainingGameTicks > 0
                && q.RemainingGameTicks <= q.OriginalMaximumGameTicks && !q.NewTicksGranted),
            ("original_task_window_observed", q is null ? null : q.OriginalDeadlineUtc != default
                && q.OriginalDeadlineUtc.Offset == TimeSpan.Zero),
            ("effective_window_observed", p is null ? null : p.EffectiveRecoveryDeadlineUtc.HasValue),
            ("exact_effective_window", q is null || p is null ? null : q.EffectiveDeadlineUtc == p.EffectiveRecoveryDeadlineUtc
                && (!r.RecoveryDeadlineUtc.HasValue || q.EffectiveDeadlineUtc == r.RecoveryDeadlineUtc)),
            ("exact_wall_authority_disclosure", q is null ? null : q.NewWallAuthorityRequired == r.RecoveryDeadlineUtc.HasValue),
            ("no_other_recovery_authority", p is null ? null : !p.OpensSavedWorldOnly && !p.PreviousAutomationRemainsStopped
                && !p.RemotePrimaryRecoveryAuthorizationUsed && !p.CompletedRemoteLastExitContinuationUsed
                && !p.FailedRemoteLastExitReauthorizationUsed && !p.PriorActionTerminalCompletionAttested
                && !p.PriorActionTerminalFailureAttested && !p.PriorFailedResumeProvenanceUsed
                && !p.PriorFailedResumeV2ProvenanceUsed && !p.PriorFailedResumeV3ProvenanceUsed
                && !p.LegacyLocalMoveUnknownProvenanceUsed && p.InheritedWindowConfirmedAtUtc is null),
        };
        e.NotReached(checks.Select(c => c.Name).Concat(new[] { "owner_commit_confirmation", "native_load",
            "native_idle", "home_return", "home_save" }).ToArray());
        foreach (var check in checks)
            if (!e.Require(check.Name, check.Value)) return e.Failure(Spherewright.Contracts.Errors.BridgeErrorCodes.BridgeNotReady);
        return null;
    }
    public static string? ValidateRequest(PrepareOwnedWorldResumeRequest r)
    {
        if (RequestFailure(r) is not null)
            return "Return-only continuation requires the current credential, exact saved tick and return-only scope; confirmation belongs to commit. Tick grants, charging, rollback and prior-action outcome claims are unavailable.";
        return null;
    }

    public static NativeResumeFailureSnapshot? RequestFailure(PrepareOwnedWorldResumeRequest r)
    {
        var e = new NativeResumeDecisionEvidence("return_only_continuation_request", null, null, true);
        e.Number("expected_source_tick", r.ExpectedRecoveryGameTick);
        e.Number("requested_charge_ticks", r.MaximumChargeGameTicks);
        e.Number("requested_charge_seconds", r.MaximumChargeWallSeconds);
        var checks = new (string Name, bool Value)[]{
            ("explicit_return_continuation_mode",r.RecoveryMode==OwnedWorldResumeModes.ResumeReturnOnlyTask),
            ("prepare_unconfirmed",!r.UserConfirmedInConversation),
            ("credential_supplied",!string.IsNullOrWhiteSpace(r.ResumeToken)&&r.ResumeToken.Length<=256),
            ("return_only_scope",r.RecoveryScope==Scope),
            ("exact_expected_tick_supplied",r.ExpectedRecoveryGameTick.HasValue&&r.ExpectedRecoveryGameTick>=0),
            ("no_rollback_request",!r.AcceptUnstoredProgressLoss&&!r.MinimumRecoveryGameTick.HasValue),
            ("no_charging_grant",r.MaximumChargeGameTicks==0&&r.MaximumChargeWallSeconds==0d),
            ("no_prior_outcome_claim",!r.PriorActionKnownTerminalFailed.HasValue&&!r.PriorActionKnownTerminalCompleted.HasValue),
            ("no_predecessor_replay",string.IsNullOrEmpty(r.PriorConsumedResumeToken)&&string.IsNullOrEmpty(r.PriorSourceFingerprint)
                &&string.IsNullOrEmpty(r.PriorLegacySourceFingerprint)&&string.IsNullOrEmpty(r.PriorIntermediateSourceFingerprint)),
            ("no_legacy_context",string.IsNullOrEmpty(r.ExpectedRemoteRecoveryContextFingerprint)
                &&string.IsNullOrEmpty(r.LegacyLocalMoveUnknownEvidenceJson)&&string.IsNullOrEmpty(r.ExpectedLegacyLocalMoveUnknownEvidenceHash))};
        e.NotReached(checks.Select(c => c.Name).Concat(new[] { "protected_files", "native_load", "strict_idle", "home_return" }).ToArray());
        foreach (var check in checks) if (!e.Require(check.Name, check.Value)) return e.Failure("return_only_continuation_request_invalid");
        return null;
    }

    public static VoyagerReturnOnlyContinuationDecision Evaluate(VoyagerReturnOnlyContinuationFacts f)
    {
        var e = new NativeResumeDecisionEvidence("return_only_continuation_source", null, f.NowUtc, true);
        e.Number("source_tick", f.SourceGameTick); e.Number("primary_tick", f.PrimaryGameTick);
        e.Number("credential_minimum_tick", f.CredentialMinimumGameTick);
        e.Number("preserved_boundary_tick", f.PreservedBoundaryGameTick);
        e.Number("original_task_start_tick", f.TaskStartedAtGameTick);
        e.Number("original_task_maximum_ticks", f.OriginalMaximumGameTicks);
        var remaining = VoyagerReturnOnlyRecoveryPolicy.RemainingTicks(f.TaskStartedAtGameTick,
            f.OriginalMaximumGameTicks, f.SourceGameTick);
        e.Number("remaining_task_ticks", remaining);
        e.NotReached("owned_native", "task_session", "original_checkpoint_witness", "original_journal",
            "no_pending_recovery", "remote_return_phase", "complete_files", "source_tick_observed",
            "primary_tick_observed", "credential_minimum_observed", "source_equals_credential_minimum", "primary_not_newer", "original_task_cap", "remaining_return_ticks",
            "wall_clock_observed", "new_wall_window_finite", "wall_return_and_stop_reserved",
            "owner_commit_confirmation", "native_load", "native_idle", "home_return", "home_save");
        VoyagerReturnOnlyContinuationDecision Failed(string code) => new()
        {
            RemainingGameTicks = remaining,
            FirstFailure = e.Failure(code),
        };
        if (!e.Require("owned_native", f.ExactOwnedNative)
            || !e.Require("no_pending_recovery", f.NoPendingRecovery)
            || !e.Require("task_session", f.ExactTaskCredentialSession)
            || !e.Require("original_checkpoint_witness", f.OriginalCheckpointWitness)
            || !e.Require("original_journal", f.JournalBound)
            || !e.Require("remote_return_phase", f.RemoteReturnPhase)
            || !e.Require("complete_files", f.CompleteSourceFiles))
            return Failed("return_only_continuation_provenance_unproved");
        if (f.PreservedBoundaryGameTick.HasValue
            && (!e.Require("preservation_receipt_bound", f.PreservedBoundaryProved)
                || !e.Require("preservation_not_older_than_credential", f.PreservedBoundaryGameTick >= f.CredentialMinimumGameTick)))
            return Failed("return_only_preserved_boundary_unproved");
        var boundary = f.PreservedBoundaryGameTick ?? f.CredentialMinimumGameTick;
        if (!e.Require("source_tick_observed", f.SourceGameTick.HasValue ? true : null)
            || !e.Require("primary_tick_observed", f.PrimaryGameTick.HasValue ? true : null)
            || !e.Require("credential_minimum_observed", f.CredentialMinimumGameTick.HasValue ? true : null)
            // A larger number alone cannot establish this task's branch.
            || !e.Require("source_equals_credential_minimum", f.SourceGameTick >= 0
                && f.SourceGameTick == boundary)
            || !e.Require("primary_not_newer", f.PrimaryGameTick >= 0 && f.PrimaryGameTick <= f.SourceGameTick))
            return Failed("return_only_continuation_exact_saved_boundary_required");
        var flightTicks = VoyagerReturnOnlyRecoveryPolicy.FlightTicks(remaining, f.OriginalReturnLimitGameTicks);
        if (!e.Require("original_task_cap", f.OriginalMaximumGameTicks >= VoyagerReturnOnlyRecoveryPolicy.MinimumTotalGameTicks
                && f.OriginalMaximumGameTicks <= VoyagerReturnOnlyRecoveryPolicy.MaximumGameTicks)
            || !e.Require("remaining_return_ticks", remaining.HasValue && flightTicks >= 3600))
            return Failed("return_only_continuation_ticks_exhausted");
        var deadline = f.ProposedWallDeadlineUtc ?? f.OriginalDeadlineUtc;
        if (!e.Require("wall_clock_observed", f.NowUtc != default && f.NowUtc.Offset == TimeSpan.Zero))
            return Failed("return_only_continuation_clock_unproved");
        if (f.ProposedWallDeadlineUtc.HasValue
            && !e.Require("new_wall_window_finite", deadline.Offset == TimeSpan.Zero && deadline > f.NowUtc
                && deadline - f.NowUtc <= TimeSpan.FromSeconds(MaximumWallSeconds)))
            return Failed("return_only_continuation_wall_window_invalid");
        e.Number("remaining_wall_seconds", (deadline - f.NowUtc).TotalSeconds);
        if (!e.Require("wall_return_and_stop_reserved", deadline.Offset == TimeSpan.Zero
            && (deadline - f.NowUtc).TotalSeconds > flightTicks / 60d + VoyagerControlDeadlinePolicy.StopCleanupSeconds
                + VoyagerRemoteIdleAdmissionPolicy.StopWallSeconds + 1d / 60d))
            return Failed("return_only_continuation_wall_window_unavailable");
        return new()
        {
            SourceEligible = true,
            RemainingGameTicks = remaining,
            EffectiveDeadlineUtc = deadline,
            RequiresNewWallAuthority = f.ProposedWallDeadlineUtc.HasValue
        };
    }
}
