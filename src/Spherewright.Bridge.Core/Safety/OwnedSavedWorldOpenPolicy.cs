using Spherewright.Contracts.Sessions;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>Opening durable world data grants no continuation of an expired automatic task.</summary>
public static class OwnedSavedWorldOpenPolicy
{
    public const int EvidenceVersion = 1;
    public const string AutomationStoppedCode = "SAVED_WORLD_OPEN_AUTOMATION_STOPPED";

    public static string? ValidateRequest(PrepareOwnedWorldResumeRequest r)
    {
        if (r.RecoveryMode != OwnedWorldResumeModes.OpenSavedPrimary || r.UserConfirmedInConversation
            || r.RecoveryDeadlineUtc.HasValue || !string.IsNullOrEmpty(r.RecoveryScope)
            || r.MaximumChargeGameTicks != 0 || r.MaximumChargeWallSeconds != 0
            || OwnedWorldRecoveryPolicy.HasPriorFailedResumeWitness(r)
            || r.PriorActionKnownTerminalCompleted.HasValue
            || !string.IsNullOrEmpty(r.ExpectedRemoteRecoveryContextFingerprint)
            || !string.IsNullOrEmpty(r.LegacyLocalMoveUnknownEvidenceJson)
            || !string.IsNullOrEmpty(r.ExpectedLegacyLocalMoveUnknownEvidenceHash))
            return "Saved-world opening accepts no task continuation, charging, new clock, outcome attestation or prepare confirmation.";
        if (r.AcceptUnstoredProgressLoss)
            return r.MinimumRecoveryGameTick is long floor && floor >= 0
                && r.ExpectedRecoveryGameTick is long exact && exact >= 0 && exact < floor
                ? null : "Discarding unstored progress requires the exact known progress floor and exact saved-primary tick together.";
        return r.MinimumRecoveryGameTick.HasValue || r.ExpectedRecoveryGameTick.HasValue
            ? "Tick overrides require explicit acceptance of the exact unstored progress loss." : null;
    }

    public static string? ValidateCandidate(PrepareOwnedWorldResumeRequest r, long knownProgress,
        string currentVersion, OwnedSavePrefixEvidence source)
    {
        if (ValidateRequest(r) is { } error) return error;
        if (!source.MatchesExpectedIdentity || !source.Peaceful || source.GameVersion != currentVersion)
            return "The complete saved primary must prove its exact owned identity, peaceful mode and current Native version.";
        if (r.AcceptUnstoredProgressLoss)
            return r.MinimumRecoveryGameTick == knownProgress && r.ExpectedRecoveryGameTick == source.GameTick
                ? null : "The approved progress floor or saved-primary tick changed; no load is allowed.";
        return source.GameTick >= knownProgress ? null
            : "The saved primary is older than known progress; explicit exact progress-loss acceptance is required.";
    }

    public static bool HasMatchingEcho(PreparedOwnedWorldResumePlan p) =>
        p.Prepared && p.RecoveryMode == OwnedWorldResumeModes.OpenSavedPrimary
        && p.RecoveryEvidenceVersion == EvidenceVersion && p.OpensSavedWorldOnly
        && p.PreviousAutomationRemainsStopped && p.NewChargingProhibited
        && p.ExactEmbeddedIdentityVerified && p.UserConfirmationRequired && !p.CommitAllowedNow
        && p.CandidateGameTick == p.MinimumGameTick && p.DiscardedUnstoredProgressGameTicks >= 0
        && p.EffectiveRecoveryDeadlineUtc is null && p.MaximumChargeGameTicks == 0 && p.MaximumChargeWallSeconds == 0
        && string.IsNullOrEmpty(p.RecoveryScope) && !p.NativeChargeFeasibilityRequiresFreshCapture
        && !p.PriorFailedResumeProvenanceUsed && !p.PriorFailedResumeV2ProvenanceUsed && !p.PriorFailedResumeV3ProvenanceUsed
        && !p.PriorActionTerminalFailureAttested && !p.PriorActionTerminalCompletionAttested
        && !p.FailedRemoteLastExitReauthorizationUsed && !p.CompletedRemoteLastExitContinuationUsed
        && !p.RemotePrimaryRecoveryAuthorizationUsed
        && !p.LegacyLocalMoveUnknownProvenanceUsed
        && !string.IsNullOrEmpty(p.ConfirmationDigest);
}
