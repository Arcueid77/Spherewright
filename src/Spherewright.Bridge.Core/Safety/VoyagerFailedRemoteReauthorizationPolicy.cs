using Spherewright.Contracts.Sessions;

namespace Spherewright.Bridge.Core.Safety;

// A new owner-authorized current context, not an opening of a prior opaque
// authorization and not a durable certificate of the prior action's outcome.
public static class VoyagerFailedRemoteReauthorizationPolicy
{
    public const int EvidenceVersion = 1;
    public const string ContextDomain = "voyager-zero-progress-remote-context-v1";

    public static string ContextFingerprint(string journeyJson, string checkpointJson,
        string checkpointNativeFingerprint, string primaryFingerprint, long sourceTick,
        long primaryTick, string journalCheckpointJson, string validatedJournalHash) =>
        CanonicalStateHash.Combine(ContextDomain, journeyJson, checkpointJson,
            checkpointNativeFingerprint, primaryFingerprint, sourceTick, primaryTick,
            journalCheckpointJson, validatedJournalHash);

    public static string? ValidateRequest(PrepareOwnedWorldResumeRequest r)
    {
        if (r.RecoveryMode != OwnedWorldResumeModes.ReauthorizeFailedRemoteLastExit
            || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(r.ExpectedRemoteRecoveryContextFingerprint)
            || string.IsNullOrWhiteSpace(r.PriorConsumedResumeToken) || r.PriorConsumedResumeToken.Length > 256
            || r.PriorActionKnownTerminalFailed != true
            || !string.IsNullOrEmpty(r.PriorSourceFingerprint) || !string.IsNullOrEmpty(r.PriorLegacySourceFingerprint)
            || !string.IsNullOrEmpty(r.PriorIntermediateSourceFingerprint))
            return "Failed-remote-LastExit reauthorization requires the exact owner context, immediately prior consumed token and explicit known-terminal-failed caller attestation; legacy source witnesses are not accepted in this mode.";
        if (r.UserConfirmedInConversation || r.MinimumRecoveryGameTick.HasValue || r.ExpectedRecoveryGameTick.HasValue)
            return "Failed-remote-LastExit prepare only discloses the current context; confirmation belongs to commit and candidate ticks are Native-derived.";
        if (!r.RecoveryDeadlineUtc.HasValue || r.RecoveryScope != VoyagerRecoveryWindowProposal.TaskScope
            || r.MaximumChargeGameTicks < 1 || r.MaximumChargeGameTicks > 18000
            || double.IsNaN(r.MaximumChargeWallSeconds) || double.IsInfinity(r.MaximumChargeWallSeconds)
            || r.MaximumChargeWallSeconds <= 0d || r.MaximumChargeWallSeconds > 300d)
            return "Failed-remote-LastExit reauthorization requires its finite absolute UTC, original task scope and both charge caps together.";
        return null;
    }

    public static string? Blocker(VoyagerRemoteResumeFacts f, string expectedContext, string actualContext,
        bool immediateConsumptionProved, bool terminalFailureAttested, long? successfulFlightTick,
        VoyagerRecoveryWindowProposal? priorProposal)
    {
        if (!f.DefaultLastExit) return "remote_reauthorization_fixed_last_exit_required";
        if (!f.ExactOwnedIdentityAndVersion || !f.Peaceful) return "remote_resume_native_identity_unproved";
        if (!f.FullSourceAndPrimaryEvidence) return "remote_resume_full_file_evidence_unproved";
        if (!f.ExactDurableJourneyAndCheckpoint || !f.OriginalJournalCheckpointPresent
            || !immediateConsumptionProved || !terminalFailureAttested)
            return "remote_reauthorization_current_provenance_unproved";
        if (!OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(expectedContext)
            || !string.Equals(expectedContext, actualContext, StringComparison.Ordinal))
            return "remote_reauthorization_owner_context_mismatch";
        // The current Native source/minimum edge may follow finite local work.
        // This does not certify an old action's outcome; all local elapsed ticks
        // remain charged by RemainingJourneyBlocker against the original budget.
        if (!f.LastLandedGameTick.HasValue || f.LastLandedGameTick < 0 || f.LastLandedGameTick == long.MaxValue
            || f.SourceGameTick != f.TicketMinimumGameTick || f.SourceGameTick <= f.LastLandedGameTick.Value
            || f.LastAccountedGameTick != f.LastLandedGameTick || successfulFlightTick != f.LastLandedGameTick)
            return "remote_reauthorization_zero_progress_edge_unproved";
        if (priorProposal is null || f.RecoveryProposal is null
            || priorProposal.Scope != VoyagerRecoveryWindowProposal.TaskScope
            || priorProposal.MaximumChargeGameTicks < 1 || priorProposal.MaximumChargeGameTicks > 18000
            || double.IsNaN(priorProposal.MaximumChargeWallSeconds) || double.IsInfinity(priorProposal.MaximumChargeWallSeconds)
            || priorProposal.MaximumChargeWallSeconds <= 0d || priorProposal.MaximumChargeWallSeconds > 300d
            || f.RecoveryProposal.MaximumChargeGameTicks > priorProposal.MaximumChargeGameTicks
            || f.RecoveryProposal.MaximumChargeWallSeconds > priorProposal.MaximumChargeWallSeconds)
            return "remote_reauthorization_prior_charge_caps_exceeded";
        return VoyagerRemoteResumePolicy.RemainingJourneyBlocker(f);
    }

    public static bool HasMatchingEcho(PrepareOwnedWorldResumeRequest r, PreparedOwnedWorldResumePlan p) =>
        p.Prepared && p.RecoveryMode == OwnedWorldResumeModes.ReauthorizeFailedRemoteLastExit
        && !p.RemotePrimaryRecoveryAuthorizationUsed
        && p.RecoveryEvidenceVersion == EvidenceVersion && p.FailedRemoteLastExitReauthorizationUsed
        && p.RemoteRecoveryContextFingerprint == r.ExpectedRemoteRecoveryContextFingerprint
        && !p.PriorFailedResumeProvenanceUsed && !p.PriorFailedResumeV2ProvenanceUsed && !p.PriorFailedResumeV3ProvenanceUsed
        && p.PriorActionTerminalFailureAttested && p.ExactEmbeddedIdentityVerified && p.UserConfirmationRequired
        && !p.CommitAllowedNow && OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(p.ConfirmationDigest)
        && p.CandidateGameTick.HasValue && p.CandidateGameTick == p.MinimumGameTick
        && p.OriginalJourneyDeadlineUtc.HasValue && p.EffectiveRecoveryDeadlineUtc == r.RecoveryDeadlineUtc
        && p.RecoveryScope == r.RecoveryScope && p.MaximumChargeGameTicks == r.MaximumChargeGameTicks
        && p.MaximumChargeWallSeconds == r.MaximumChargeWallSeconds && p.NativeChargeFeasibilityRequiresFreshCapture;
}
