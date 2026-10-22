using Spherewright.Contracts.Sessions;

namespace Spherewright.Bridge.Core.Safety;

// Continue an already completed admission under the SAME remaining authority.
// Consumption is not Native outcome authentication. Charging is never reopened.
public static class VoyagerCompletedRemoteContinuationPolicy
{
    public const int EvidenceVersion = 1;
    public const string ContextDomain = "voyager-completed-remote-context-v1";
    public static string ContextFingerprint(string journeyJson, string checkpointJson,
        string checkpointNativeFingerprint, string primaryFingerprint, long sourceTick,
        long primaryTick, string journalCheckpointJson, string validatedJournalHash) =>
        CanonicalStateHash.Combine(ContextDomain, journeyJson, checkpointJson, checkpointNativeFingerprint,
            primaryFingerprint, sourceTick, primaryTick, journalCheckpointJson, validatedJournalHash);

    public static string? ValidateRequest(PrepareOwnedWorldResumeRequest r)
    {
        if (r.RecoveryMode != OwnedWorldResumeModes.ContinueCompletedRemoteLastExit
            || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(r.ExpectedRemoteRecoveryContextFingerprint)
            || string.IsNullOrWhiteSpace(r.PriorConsumedResumeToken) || r.PriorConsumedResumeToken.Length > 256
            || r.PriorActionKnownTerminalCompleted != true || r.PriorActionKnownTerminalFailed.HasValue
            || !string.IsNullOrEmpty(r.PriorSourceFingerprint) || !string.IsNullOrEmpty(r.PriorLegacySourceFingerprint)
            || !string.IsNullOrEmpty(r.PriorIntermediateSourceFingerprint)
            || !string.IsNullOrEmpty(r.LegacyLocalMoveUnknownEvidenceJson) || !string.IsNullOrEmpty(r.ExpectedLegacyLocalMoveUnknownEvidenceHash))
            return "Completed-remote continuation requires the exact current context, immediate consumed token and explicit known-terminal-completed caller attestation; failed, unknown and legacy witnesses are not interchangeable.";
        if (r.UserConfirmedInConversation || r.MinimumRecoveryGameTick.HasValue || r.ExpectedRecoveryGameTick.HasValue
            || r.RecoveryDeadlineUtc.HasValue || !string.IsNullOrEmpty(r.RecoveryScope)
            || r.MaximumChargeGameTicks != 0 || r.MaximumChargeWallSeconds != 0d)
            return "Completed-remote prepare derives the current Native tick and inherits the existing authorization only. It accepts no new deadline, charge caps, budget or prepare-time confirmation.";
        return null;
    }

    public static string? Blocker(VoyagerRemoteResumeFacts f, string expectedContext, string actualContext,
        bool immediateConsumptionProved, bool completedAttested, long? successfulFlightTick,
        VoyagerRecoveryAuthorization? prior, NativeResumeDecisionEvidence? evidence = null)
    {
        evidence?.Number("source_file_tick", f.SourceGameTick); evidence?.Number("primary_file_tick", f.PrimaryGameTick);
        evidence?.Number("credential_minimum_tick", f.TicketMinimumGameTick); evidence?.Number("spent_ticks", f.SpentGameTicks);
        evidence?.Number("elapsed_ticks", f.LastLandedGameTick.HasValue ? f.SourceGameTick - f.LastLandedGameTick.Value : null);
        evidence?.Number("maximum_ticks", f.MaximumGameTicks); evidence?.Number("return_reserve_ticks", f.ReturnReserveGameTicks);
        evidence?.Number("original_window_wall_cap_s", prior?.Proposal.MaximumChargeWallSeconds);
        evidence?.Number("original_window_wall_used_s", prior is null ? null : (f.NowUtc - prior.WindowOriginUtc()).TotalSeconds);
        evidence?.Number("original_deadline_wall_remaining_s", prior is null ? null : (prior.Proposal.DeadlineUtc - f.NowUtc).TotalSeconds);
        bool All(params (string Name, bool? Value)[] checks)
        {
            evidence?.NotReached(checks.Select(c => c.Name).ToArray());
            foreach (var c in checks) if (evidence is null ? c.Value != true : !evidence.Require(c.Name, c.Value)) return false;
            return true;
        }
        if (!All(("completed_fixed_last_exit", f.DefaultLastExit))) return "completed_remote_fixed_last_exit_required";
        if (!All(("completed_native_identity_version", f.ExactOwnedIdentityAndVersion), ("completed_peaceful", f.Peaceful)))
            return "remote_resume_native_identity_unproved";
        if (!All(("completed_full_source_primary", f.FullSourceAndPrimaryEvidence))) return "remote_resume_full_file_evidence_unproved";
        if (!All(("completed_remote_landed", f.RemoteLandedOutbound), ("completed_successful_outbound", f.CheckpointFlightSucceededWithoutReload)))
            return "remote_resume_successful_outbound_required";
        if (!All(("completed_source_covers_primary", f.SourceGameTick >= f.PrimaryGameTick),
            ("completed_source_covers_credential", f.SourceGameTick >= f.TicketMinimumGameTick)))
            return "remote_resume_newer_progress_rollback_forbidden";
        if (!All(("completed_current_journey_checkpoint", f.ExactDurableJourneyAndCheckpoint),
            ("completed_original_journal", f.OriginalJournalCheckpointPresent),
            ("completed_immediate_consumption", immediateConsumptionProved), ("completed_caller_attestation", completedAttested)))
            return "completed_remote_current_provenance_unproved";
        if (!All(("completed_context_canonical", OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(expectedContext)),
            ("completed_context_matches", expectedContext == actualContext))) return "completed_remote_context_mismatch";
        if (!All(("completed_landing_observed", f.LastLandedGameTick.HasValue),
            ("completed_source_equals_current_minimum", f.SourceGameTick == f.TicketMinimumGameTick),
            ("completed_source_after_landing", f.SourceGameTick > f.LastLandedGameTick),
            ("completed_accounted_landing", f.LastAccountedGameTick == f.LastLandedGameTick),
            ("completed_original_success_tick", successfulFlightTick == f.LastLandedGameTick)))
            return "completed_remote_original_accounting_unproved";
        if (!All(("completed_prior_authorization_observed", prior is not null),
            ("completed_proposal_observed", f.RecoveryProposal is not null))) return "completed_remote_inherited_window_unproved";
        if (!All(("completed_identical_proposal", SameProposal(prior!.Proposal, f.RecoveryProposal!)),
            ("completed_original_origin_observed", prior.WindowOriginUtc() != default),
            ("completed_origin_not_after_confirmation", prior.WindowOriginUtc() <= prior.ConfirmedAtUtc),
            ("completed_confirmation_not_future", prior.ConfirmedAtUtc <= f.NowUtc))) return "completed_remote_inherited_window_unproved";
        if (!All(("completed_clock_not_reversed", f.NowUtc >= prior.WindowOriginUtc()),
            ("completed_original_wall_remaining", (f.NowUtc - prior.WindowOriginUtc()).TotalSeconds < prior.Proposal.MaximumChargeWallSeconds),
            ("completed_original_deadline_remaining", prior.Proposal.DeadlineUtc > f.NowUtc)))
            return "completed_remote_original_window_exhausted";
        return VoyagerRemoteResumePolicy.RemainingJourneyBlocker(f, evidence);
    }

    private static bool SameProposal(VoyagerRecoveryWindowProposal a, VoyagerRecoveryWindowProposal b) =>
        a.DeadlineUtc == b.DeadlineUtc && a.Scope == b.Scope
        && a.MaximumChargeGameTicks == b.MaximumChargeGameTicks && a.MaximumChargeWallSeconds == b.MaximumChargeWallSeconds;

    public static string? StrictFundingBlocker(VoyagerRemoteIdleAdmissionFacts? f, NativeResumeDecisionEvidence? evidence = null)
    {
        evidence?.NotReached("completed_resume_cost_observed", "completed_resume_strict_core_funded", "completed_resume_strict_loaded_funded");
        var rawP = f?.MovementReserve?.PerNativeTickEnergyJoules;
        var p = rawP.GetValueOrDefault(double.NaN);
        bool? cost = f?.MovementReserve is null || !rawP.HasValue ? null
            : f.MovementReserve.Proved && !double.IsNaN(p) && !double.IsInfinity(p) && p > 0d;
        if (evidence is null ? cost != true : !evidence.Require("completed_resume_cost_observed", cost))
            return "completed_remote_strict_cost_unproved";
        var core = f!.CoreEnergyJoules; var loaded = f.JourneyBudget?.AvailableLoadedEnergyJoules;
        var floor = f.JourneyBudget?.LockedReturnFloorJoules;
        evidence?.Number("completed_resume_core_j", core); evidence?.Number("completed_resume_loaded_j", loaded);
        evidence?.Number("completed_resume_return_floor_j", floor); evidence?.Number("completed_resume_per_tick_j", rawP);
        bool? coreFunded = !core.HasValue ? null : !double.IsNaN(core.Value) && !double.IsInfinity(core.Value)
            && core >= VoyagerLocalWalkReservePolicy.StopGameTicks * p;
        if (evidence is null ? coreFunded != true : !evidence.Require("completed_resume_strict_core_funded", coreFunded))
            return "completed_remote_strict_core_unfunded";
        bool? loadedFunded = !loaded.HasValue || !floor.HasValue ? null
            : !double.IsNaN(loaded.Value) && !double.IsInfinity(loaded.Value)
            && !double.IsNaN(floor.Value) && !double.IsInfinity(floor.Value) && floor >= 0d
            && loaded >= floor + VoyagerLocalWalkReservePolicy.StopGameTicks * p;
        return (evidence is null ? loadedFunded == true : evidence.Require("completed_resume_strict_loaded_funded", loadedFunded))
            ? null : "completed_remote_strict_loaded_unfunded";
    }

    public static bool HasMatchingEcho(PrepareOwnedWorldResumeRequest r, PreparedOwnedWorldResumePlan p) =>
        p.Prepared && p.RecoveryMode == OwnedWorldResumeModes.ContinueCompletedRemoteLastExit
        && p.RecoveryEvidenceVersion == EvidenceVersion && p.CompletedRemoteLastExitContinuationUsed
        && !p.RemotePrimaryRecoveryAuthorizationUsed
        && p.PriorActionTerminalCompletionAttested && !p.PriorActionTerminalFailureAttested
        && !p.FailedRemoteLastExitReauthorizationUsed && !p.PriorFailedResumeProvenanceUsed
        && !p.PriorFailedResumeV2ProvenanceUsed && !p.PriorFailedResumeV3ProvenanceUsed
        && !p.LegacyLocalMoveUnknownProvenanceUsed && p.NewChargingProhibited && !p.NativeChargeFeasibilityRequiresFreshCapture
        && p.RecoveryScope == VoyagerRecoveryWindowProposal.TaskScope
        && p.MaximumChargeGameTicks >= 1 && p.MaximumChargeGameTicks <= 18000
        && !double.IsNaN(p.MaximumChargeWallSeconds) && !double.IsInfinity(p.MaximumChargeWallSeconds)
        && p.MaximumChargeWallSeconds > 0 && p.MaximumChargeWallSeconds <= 300
        && p.InheritedWindowConfirmedAtUtc.HasValue && p.EffectiveRecoveryDeadlineUtc.HasValue
        && p.InheritedWindowConfirmedAtUtc < p.EffectiveRecoveryDeadlineUtc
        && p.RemoteRecoveryContextFingerprint == r.ExpectedRemoteRecoveryContextFingerprint
        && p.ExactEmbeddedIdentityVerified && p.UserConfirmationRequired && !p.CommitAllowedNow
        && OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(p.ConfirmationDigest)
        && p.CandidateGameTick.HasValue && p.CandidateGameTick == p.MinimumGameTick;
}
