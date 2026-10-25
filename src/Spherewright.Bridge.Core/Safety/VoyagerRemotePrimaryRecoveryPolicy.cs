using Spherewright.Contracts.Sessions;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Errors;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>A new explicit finite authority for the current primary, never an old action replay.</summary>
public static class VoyagerRemotePrimaryRecoveryPolicy
{
    public const int EvidenceVersion = 2;

    public static long? ConservedSpent(long currentSpent, long originalSpent, long sourceTick, long knownProgress)
    {
        if (originalSpent < 0 || currentSpent < originalSpent || sourceTick < 0 || knownProgress < 0) return null;
        var required = (decimal)originalSpent + Math.Max(0m, (decimal)knownProgress - sourceTick);
        return required <= long.MaxValue ? Math.Max(currentSpent, (long)required) : (long?)null;
    }

    public static string? ValidateRequest(PrepareOwnedWorldResumeRequest r) => ValidateRequest(r, null);

    public static NativeResumeFailureSnapshot? RequestFailure(PrepareOwnedWorldResumeRequest r, DateTimeOffset now)
    {
        var evidence = new NativeResumeDecisionEvidence("remote_primary_request", null, now, true);
        evidence.Number("requested_charge_ticks", r.MaximumChargeGameTicks);
        evidence.Number("requested_charge_wall_s", r.MaximumChargeWallSeconds);
        evidence.Number("requested_additional_voyager_ticks", r.AdditionalVoyagerGameTicks);
        evidence.Number("requested_known_floor", r.MinimumRecoveryGameTick);
        evidence.Number("requested_primary_tick", r.ExpectedRecoveryGameTick);
        evidence.Observe("prior_failure_claim", r.PriorActionKnownTerminalFailed);
        evidence.Observe("prior_completion_claim", r.PriorActionKnownTerminalCompleted);
        evidence.NotReached("primary_file_evidence", "native_load", "native_initialization", "native_contact", "healthy_takeover");
        return ValidateRequest(r, evidence) is null ? null : evidence.Failure(BridgeErrorCodes.InvalidRequest);
    }

    private static string? ValidateRequest(PrepareOwnedWorldResumeRequest r, NativeResumeDecisionEvidence? e)
    {
        bool All(params (string Name, bool Value)[] checks)
        {
            e?.NotReached(checks.Select(c => c.Name).ToArray());
            foreach (var c in checks) if (e is null ? !c.Value : !e.Require(c.Name, c.Value)) return false;
            return true;
        }
        if (!All(("request_primary_mode", r.RecoveryMode == OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery),
            ("request_additional_tick_minimum", r.AdditionalVoyagerGameTicks >= 0),
            ("request_additional_tick_maximum", r.AdditionalVoyagerGameTicks <= VoyagerRecoveryWindowPolicy.MaximumAdditionalVoyagerGameTicks),
            ("request_prepare_unconfirmed", !r.UserConfirmedInConversation), ("request_new_deadline_observed", r.RecoveryDeadlineUtc.HasValue),
            ("request_original_task_scope", r.RecoveryScope == VoyagerRecoveryWindowProposal.TaskScope),
            ("request_charge_tick_minimum", r.MaximumChargeGameTicks >= 1), ("request_charge_tick_maximum", r.MaximumChargeGameTicks <= 18000),
            ("request_charge_wall_finite", !double.IsNaN(r.MaximumChargeWallSeconds) && !double.IsInfinity(r.MaximumChargeWallSeconds)),
            ("request_charge_wall_minimum", r.MaximumChargeWallSeconds > 0), ("request_charge_wall_maximum", r.MaximumChargeWallSeconds <= 300),
            ("request_no_prior_token", string.IsNullOrEmpty(r.PriorConsumedResumeToken)),
            ("request_no_prior_source", string.IsNullOrEmpty(r.PriorSourceFingerprint)),
            ("request_no_prior_legacy_source", string.IsNullOrEmpty(r.PriorLegacySourceFingerprint)),
            ("request_no_prior_intermediate_source", string.IsNullOrEmpty(r.PriorIntermediateSourceFingerprint)),
            ("request_no_prior_failure_claim", !r.PriorActionKnownTerminalFailed.HasValue),
            ("request_no_prior_completion_claim", !r.PriorActionKnownTerminalCompleted.HasValue),
            ("request_no_other_context", string.IsNullOrEmpty(r.ExpectedRemoteRecoveryContextFingerprint)),
            ("request_no_legacy_capture", string.IsNullOrEmpty(r.LegacyLocalMoveUnknownEvidenceJson)),
            ("request_no_legacy_hash", string.IsNullOrEmpty(r.ExpectedLegacyLocalMoveUnknownEvidenceHash))))
            return "Remote-primary recovery requires a new explicit finite window and charge caps, followed by separate commit confirmation. It accepts no prior-action outcome claim or replay witness.";
        if (r.AcceptUnstoredProgressLoss)
            return All(("request_known_floor_observed", r.MinimumRecoveryGameTick.HasValue),
                ("request_known_floor_nonnegative", r.MinimumRecoveryGameTick >= 0),
                ("request_exact_primary_observed", r.ExpectedRecoveryGameTick.HasValue),
                ("request_exact_primary_nonnegative", r.ExpectedRecoveryGameTick >= 0),
                ("request_exact_positive_loss", r.ExpectedRecoveryGameTick < r.MinimumRecoveryGameTick))
                ? null : "Exact known progress floor and exact approved primary tick are required for progress loss.";
        return All(("request_no_unapproved_floor_override", !r.MinimumRecoveryGameTick.HasValue),
            ("request_no_unapproved_primary_override", !r.ExpectedRecoveryGameTick.HasValue))
            ? null : "Tick overrides require explicit exact progress-loss acceptance.";
    }

    public static string? Blocker(PrepareOwnedWorldResumeRequest r, VoyagerRemoteResumeFacts f,
        long knownProgress, NativeResumeDecisionEvidence e, long? observedBudgetFloor = null)
    {
        e.Number("primary_tick", f.PrimaryGameTick); e.Number("source_tick", f.SourceGameTick);
        e.Number("known_progress_tick", knownProgress); e.Number("credential_minimum_tick", f.TicketMinimumGameTick);
        e.Number("conserved_observed_progress_floor", observedBudgetFloor);
        e.Number("accounted_tick", f.LastAccountedGameTick); e.Number("landing_tick", f.LastLandedGameTick);
        e.Number("spent_ticks", f.SpentGameTicks); e.Number("maximum_ticks", f.MaximumGameTicks);
        e.Number("return_reserve_ticks", f.ReturnReserveGameTicks);
        e.Number("original_wall_remaining_s", (f.OriginalDeadlineUtc - f.NowUtc).TotalSeconds);
        e.Number("proposed_wall_remaining_s", f.RecoveryProposal is null ? null : (f.RecoveryProposal.DeadlineUtc - f.NowUtc).TotalSeconds);
        e.NotReached("primary_request_valid", "primary_source_kind", "primary_identity_version", "primary_peaceful",
            "primary_complete_files", "primary_current_journey_checkpoint", "primary_original_journal",
            "primary_remote_landed", "primary_successful_outbound", "primary_source_is_current_primary",
            "primary_known_floor_covers_credential", "primary_exact_loss_authority", "primary_landing_observed",
            "primary_accounting_nonnegative", "primary_source_covers_accounted", "primary_source_covers_landing",
            "primary_new_window_observed");
        if (!e.Require("primary_request_valid", ValidateRequest(r, e) is null)) return "remote_primary_request_invalid";
        if (!e.Require("primary_source_kind", !f.DefaultLastExit)) return "remote_primary_exact_primary_required";
        if (!e.Require("primary_identity_version", f.ExactOwnedIdentityAndVersion)
            || !e.Require("primary_peaceful", f.Peaceful)) return "remote_resume_native_identity_unproved";
        if (!e.Require("primary_complete_files", f.FullSourceAndPrimaryEvidence)) return "remote_resume_full_file_evidence_unproved";
        if (!e.Require("primary_current_journey_checkpoint", f.ExactDurableJourneyAndCheckpoint)
            || !e.Require("primary_original_journal", f.OriginalJournalCheckpointPresent)) return "remote_primary_original_provenance_unproved";
        if (!e.Require("primary_remote_landed", f.RemoteLandedOutbound)
            || !e.Require("primary_successful_outbound", f.CheckpointFlightSucceededWithoutReload)) return "remote_resume_successful_outbound_required";
        if (!e.Require("primary_source_is_current_primary", f.SourceGameTick >= 0 && f.SourceGameTick == f.PrimaryGameTick)
            || !e.Require("primary_known_floor_covers_credential", f.TicketMinimumGameTick >= 0 && knownProgress >= f.TicketMinimumGameTick)
            || !e.Require("primary_exact_loss_authority", r.AcceptUnstoredProgressLoss
                ? r.MinimumRecoveryGameTick == knownProgress && r.ExpectedRecoveryGameTick == f.SourceGameTick
                : f.SourceGameTick >= knownProgress)) return "remote_primary_progress_loss_not_authorized";
        if (!e.Require("primary_landing_observed", f.LastLandedGameTick.HasValue)
            || !e.Require("primary_accounting_nonnegative", f.LastAccountedGameTick >= 0 && f.LastLandedGameTick >= 0)
            || !e.Require("primary_source_covers_accounted", f.SourceGameTick >= f.LastAccountedGameTick)
            || !e.Require("primary_source_covers_landing", f.SourceGameTick >= f.LastLandedGameTick))
            return "remote_resume_newer_progress_rollback_forbidden";
        if (!e.Require("primary_new_window_observed", f.RecoveryProposal is not null)) return "remote_primary_new_authority_required";
        if (!e.Require("primary_additional_ticks_match", f.RecoveryProposal!.AdditionalVoyagerGameTicks == r.AdditionalVoyagerGameTicks))
            return "remote_primary_additional_tick_authority_mismatch";
        // Discarding world progress does not refund the original Journey's time.
        return VoyagerRemoteResumePolicy.FiniteJourneyBudgetBlocker(f, e,
            Math.Max(0, Math.Max(knownProgress, observedBudgetFloor ?? knownProgress) - f.SourceGameTick));
    }

    public static bool HasMatchingEcho(PrepareOwnedWorldResumeRequest r, PreparedOwnedWorldResumePlan p) =>
        ValidateRequest(r) is null && p.Prepared && p.RecoveryMode == OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery
        && p.RecoveryEvidenceVersion == EvidenceVersion && p.RemotePrimaryRecoveryAuthorizationUsed
        && !p.OpensSavedWorldOnly && !p.PreviousAutomationRemainsStopped && !p.CompletedRemoteLastExitContinuationUsed
        && !p.FailedRemoteLastExitReauthorizationUsed && !p.PriorActionTerminalCompletionAttested
        && !p.PriorActionTerminalFailureAttested && !p.PriorFailedResumeProvenanceUsed
        && !p.PriorFailedResumeV2ProvenanceUsed && !p.PriorFailedResumeV3ProvenanceUsed
        && !p.LegacyLocalMoveUnknownProvenanceUsed && p.InheritedWindowConfirmedAtUtc is null
        && p.EffectiveRecoveryDeadlineUtc == r.RecoveryDeadlineUtc && p.RecoveryScope == r.RecoveryScope
        && p.OriginalJourneyDeadlineUtc.HasValue && !string.IsNullOrEmpty(p.SourceGameVersion)
        && p.SourceGameVersion == p.TargetGameVersion
        && p.MaximumChargeGameTicks == r.MaximumChargeGameTicks && p.MaximumChargeWallSeconds == r.MaximumChargeWallSeconds
        && p.AdditionalVoyagerGameTicks == r.AdditionalVoyagerGameTicks
        && (r.AdditionalVoyagerGameTicks == 0 || p.OriginalJourneyMaximumGameTicks is { } original && original >= 7200 && original <= 108000
            && p.EffectiveJourneyMaximumGameTicks == original + r.AdditionalVoyagerGameTicks)
        && p.NativeChargeFeasibilityRequiresFreshCapture && !p.NewChargingProhibited
        && p.DiscardedUnstoredProgressGameTicks.HasValue && p.DiscardedUnstoredProgressGameTicks >= 0
        && p.ConservedObservedProgressDebitGameTicks.HasValue
        && p.ConservedObservedProgressDebitGameTicks >= p.DiscardedUnstoredProgressGameTicks
        && (r.AcceptUnstoredProgressLoss
            ? p.CandidateGameTick == r.ExpectedRecoveryGameTick
                && p.DiscardedUnstoredProgressGameTicks == r.MinimumRecoveryGameTick - r.ExpectedRecoveryGameTick
            : p.DiscardedUnstoredProgressGameTicks == 0)
        && p.CandidateGameTick == p.MinimumGameTick && p.ExactEmbeddedIdentityVerified
        && p.UserConfirmationRequired && !p.CommitAllowedNow && !p.NativeOriginalOutcomeProved
        && OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(p.ConfirmationDigest);
}
