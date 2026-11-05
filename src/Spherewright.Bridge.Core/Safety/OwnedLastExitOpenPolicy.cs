using Spherewright.Contracts.Sessions;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>Exact latest owned LastExit data access, paused and without execution authority.</summary>
public static class OwnedLastExitOpenPolicy
{
    public const int EvidenceVersion = 1;

    public static string? ValidateRequest(PrepareOwnedWorldResumeRequest r)
    {
        if (r.RecoveryMode != OwnedWorldResumeModes.OpenSavedLastExit || r.UserConfirmedInConversation
            || r.AcceptUnstoredProgressLoss || r.MinimumRecoveryGameTick is not long floor || floor < 0
            || r.ExpectedRecoveryGameTick != floor || r.RecoveryDeadlineUtc.HasValue || !string.IsNullOrEmpty(r.RecoveryScope)
            || r.MaximumChargeGameTicks != 0 || r.MaximumChargeWallSeconds != 0 || r.AdditionalVoyagerGameTicks != 0
            || r.SurfaceRecoveryTarget is not null || OwnedWorldRecoveryPolicy.HasPriorFailedResumeWitness(r)
            || r.PriorActionKnownTerminalCompleted.HasValue || !string.IsNullOrEmpty(r.ExpectedRemoteRecoveryContextFingerprint)
            || !string.IsNullOrEmpty(r.LegacyLocalMoveUnknownEvidenceJson) || !string.IsNullOrEmpty(r.ExpectedLegacyLocalMoveUnknownEvidenceHash))
            return "Exact LastExit opening requires equal disclosed current tick/floor and accepts no rollback, execution, charging or outcome authority.";
        return null;
    }

    public static string? ValidateCandidate(PrepareOwnedWorldResumeRequest r,long knownProgress,
        string nativeVersion,OwnedSavePrefixEvidence source)
    {
        if (ValidateRequest(r) is {} error) return error;
        return source.MatchesExpectedIdentity && source.Peaceful && source.GameVersion == nativeVersion
            && source.GameTick == r.ExpectedRecoveryGameTick && source.GameTick >= knownProgress
            ? null : "The complete fixed LastExit must be the exact owned, current-version peaceful source covering all known progress.";
    }

    public static bool ConfirmsEcho(PreparedOwnedWorldResumePlan p) => p.Prepared
        && p.RecoveryMode == OwnedWorldResumeModes.OpenSavedLastExit && p.RecoveryEvidenceVersion == EvidenceVersion
        && p.OpensExactLastExitOnly && p.OpensSavedWorldOnly && p.PreviousAutomationRemainsStopped
        && p.NewChargingProhibited && p.ExactEmbeddedIdentityVerified && p.UserConfirmationRequired
        && !p.CommitAllowedNow && p.CandidateGameTick == p.MinimumGameTick
        && p.DiscardedUnstoredProgressGameTicks == 0 && p.EffectiveRecoveryDeadlineUtc is null
        && p.MaximumChargeGameTicks == 0 && p.MaximumChargeWallSeconds == 0
        && string.IsNullOrEmpty(p.RecoveryScope) && !string.IsNullOrEmpty(p.ConfirmationDigest);
}
