using Spherewright.Contracts.Sessions;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>Pair-specific disclosure for researched active-credential primary migrations, never default resume.</summary>
public static class OwnedWorldPrimaryMigrationPolicy
{
    public const int EvidenceVersion = 5;
    public const string ConfirmationPrompt =
        "Confirm after reading this prepared plan: migrate only the exact previously owned primary save "
        + "from 0.10.35.29104 to 0.10.35.29131 at the verified candidate tick, preserving its owned identity "
        + "and complete gameplay journal. No other save, import copy, LastExit or autosave will be loaded. "
        + "The unchanged active credential is durably consumed before native loading; a crash or failed "
        + "load after consumption requires investigation, not replay. The exact third journal transition "
        + "must be durable before normal saving issues a new target-version credential. Continue?";
    public const int NextPatchEvidenceVersion = 6;
    public const string NextPatchConfirmationPrompt =
        "Confirm after reading this prepared plan: migrate only the exact previously owned primary save "
        + "from 0.10.35.29131 to 0.10.35.29145 at the verified candidate tick, preserving its owned identity "
        + "and complete gameplay journal. No other save, import copy, LastExit or autosave will be loaded. "
        + "The unchanged active credential is durably consumed before native loading; a crash or failed "
        + "load after consumption requires investigation, not replay. The exact fourth journal transition "
        + "must be durable before normal saving issues a new target-version credential. Continue?";

    public static bool IsSupportedPair(string source, string target) =>
        (source == OwnedWorldVersionCompatibilityPolicy.CurrentPatchVersion
            && target == OwnedWorldVersionCompatibilityPolicy.LatestPatchVersion)
        || (source == OwnedWorldVersionCompatibilityPolicy.LatestPatchVersion
            && target == OwnedWorldVersionCompatibilityPolicy.NextPatchVersion);

    public static int EvidenceVersionFor(string source, string target) =>
        !IsSupportedPair(source, target) ? 0
            : target == OwnedWorldVersionCompatibilityPolicy.NextPatchVersion ? NextPatchEvidenceVersion : EvidenceVersion;

    public static string ConfirmationPromptFor(string source, string target) =>
        !IsSupportedPair(source, target) ? string.Empty
            : target == OwnedWorldVersionCompatibilityPolicy.NextPatchVersion ? NextPatchConfirmationPrompt : ConfirmationPrompt;

    public static string? ValidateRequest(PrepareOwnedWorldResumeRequest request) =>
        request.RecoveryMode != OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary
        || request.UserConfirmedInConversation || request.MinimumRecoveryGameTick.HasValue
        || request.ExpectedRecoveryGameTick.HasValue
            ? "Version-changed-primary prepare only discloses the exact protected primary; confirmation belongs to a subsequent commit."
            : null;

    public static string? ValidateCandidate(long savedTick, string source, string target,
        OwnedSavePrefixEvidence evidence) =>
        !IsSupportedPair(source, target)
            ? "Only the researched 0.10.35.29104 to 0.10.35.29131 or 0.10.35.29131 to 0.10.35.29145 active-primary migration is supported."
            : OwnedWorldReauthorizationPolicy.ValidateCandidate(savedTick, source, evidence);

    public static bool HasMatchingEcho(PreparedOwnedWorldResumePlan plan) =>
        plan.Prepared && plan.RecoveryMode == OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary
        && IsSupportedPair(plan.SourceGameVersion, plan.TargetGameVersion)
        && plan.RecoveryEvidenceVersion == EvidenceVersionFor(plan.SourceGameVersion, plan.TargetGameVersion)
        && plan.ExactEmbeddedIdentityVerified && plan.CandidateGameTick >= 0
        && plan.CandidateGameTick == plan.MinimumGameTick
        && plan.UserConfirmationRequired && !plan.CommitAllowedNow
        && !string.IsNullOrWhiteSpace(plan.ConfirmationDigest)
        && plan.ConfirmationPrompt == ConfirmationPromptFor(plan.SourceGameVersion, plan.TargetGameVersion);
}
