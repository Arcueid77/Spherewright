using Spherewright.Contracts.Sessions;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>Explicit protected recovery from DSP's fixed AutoSave0 slot, with mode-specific credential age.</summary>
public static class OwnedWorldAutosaveRecoveryPolicy
{
    public const int EvidenceVersion = 3;
    public const int ActiveEvidenceVersion = 4;
    public const int PreapprovedExpiredEvidenceVersion = 5;
    public const string ConfirmationPrompt =
        "Confirm after reading this prepared plan: load only the exact protected AutoSave0 candidate "
        + "at the approved newer tick, preserving its owned identity and existing gameplay journal. "
        + "No primary, LastExit, other autosave or imported copy will be loaded. The expired credential "
        + "will be durably consumed before native loading; a crash or failed load after consumption "
        + "requires manual investigation, not replay. The source, current runtime and candidate must be "
        + "the same supported native version; migration is unavailable for this mode. A new restart "
        + "credential is issued only after successful adoption and normal saving. Continue?";

    public const string ActiveConfirmationPrompt =
        "Load only this exact verified fixed AutoSave0 candidate under the user's explicit conversation authority "
        + "for the disclosed owned identity and progress floor. An existing explicit grant matching this candidate "
        + "does not need a repeated question; a generic request to continue is not authority for another candidate. "
        + "No primary, LastExit, other autosave or imported copy will load. The active protected credential is "
        + "durably consumed before native loading without editing its expiry. Source, runtime and candidate must "
        + "match the supported version; migration is unavailable. Identity, full-file evidence and original Journal "
        + "are rechecked. Interruption after consumption requires investigation, not replay. Only successful "
        + "adoption and normal saving issue a new credential.";

    public const string PreapprovedExpiredConfirmationPrompt =
        "Load only this exact verified fixed AutoSave0 candidate under the user's explicit conversation authority "
        + "for the disclosed owned identity and progress floor. An existing explicit grant matching this candidate "
        + "may precede the fresh plan; a generic request to continue is not authority for another candidate. "
        + "No primary, LastExit, other autosave or imported copy will load. The expired protected credential is "
        + "durably consumed before native loading without editing its expiry. Source, runtime and candidate must "
        + "match the supported version; migration is unavailable. Identity, full-file evidence and original Journal "
        + "are rechecked. Interruption after consumption requires investigation, not replay. Only successful "
        + "adoption and normal saving issue a new credential.";

    public static bool IsFixedAutosaveMode(string mode) =>
        mode == OwnedWorldResumeModes.ReauthorizeExpiredAutosave0
        || mode == OwnedWorldResumeModes.ReauthorizeFixedAutosave0
        || mode == OwnedWorldResumeModes.ReauthorizePreapprovedExpiredAutosave0;

    public static int EvidenceVersionFor(string mode) => mode switch
    {
        OwnedWorldResumeModes.ReauthorizeExpiredAutosave0 => EvidenceVersion,
        OwnedWorldResumeModes.ReauthorizeFixedAutosave0 => ActiveEvidenceVersion,
        OwnedWorldResumeModes.ReauthorizePreapprovedExpiredAutosave0 => PreapprovedExpiredEvidenceVersion,
        _ => throw new ArgumentException("Unsupported fixed AutoSave0 mode.", nameof(mode)),
    };

    public static string ConfirmationPromptFor(string mode) => mode switch
    {
        OwnedWorldResumeModes.ReauthorizeExpiredAutosave0 => ConfirmationPrompt,
        OwnedWorldResumeModes.ReauthorizeFixedAutosave0 => ActiveConfirmationPrompt,
        OwnedWorldResumeModes.ReauthorizePreapprovedExpiredAutosave0 => PreapprovedExpiredConfirmationPrompt,
        _ => throw new ArgumentException("Unsupported fixed AutoSave0 mode.", nameof(mode)),
    };

    public static string? ValidateRequest(PrepareOwnedWorldResumeRequest request)
    {
        if (request.UserConfirmedInConversation)
            return "AutoSave0 prepare is read-only disclosure; conversation authority is acknowledged at commit, not prepare.";
        if (request.MinimumRecoveryGameTick is not long known || known < 0
            || request.ExpectedRecoveryGameTick is not long expected || expected < known)
            return "Fixed-AutoSave0 recovery requires the known progress floor and exact authorized candidate tick.";
        return null;
    }

    public static string? ValidateCandidate(PrepareOwnedWorldResumeRequest request,
        long ticketMinimumGameTick, DateTimeOffset ticketIssuedAtUtc,
        string sourceGameVersion, string currentGameVersion,
        OwnedSavePrefixEvidence evidence, DateTimeOffset writtenAtUtc, long? primaryGameTick)
    {
        if (!IsFixedAutosaveMode(request.RecoveryMode))
            return "AutoSave0 validation requires an exact supported fixed-slot recovery mode.";
        var invalid = ValidateRequest(request);
        if (invalid is not null) return invalid;

        if (!IsSupportedSameVersion(sourceGameVersion, currentGameVersion)
            || !string.Equals(evidence.GameVersion, currentGameVersion, StringComparison.Ordinal))
            return "Fixed-AutoSave0 recovery requires matching supported source, runtime and candidate game versions; migration is unavailable.";
        if (!evidence.MatchesExpectedIdentity || !evidence.Peaceful)
            return "Fixed-AutoSave0 recovery requires complete owned identity and peaceful evidence.";
        if (request.MinimumRecoveryGameTick < ticketMinimumGameTick
            || evidence.GameTick != request.ExpectedRecoveryGameTick
            || evidence.GameTick < request.MinimumRecoveryGameTick
            || evidence.GameTick <= ticketMinimumGameTick
            || !primaryGameTick.HasValue
            || primaryGameTick.Value != ticketMinimumGameTick)
            return "The fixed AutoSave0 and exact protected primary do not prove the approved newer candidate and ticket progress floor.";
        if (writtenAtUtc < ticketIssuedAtUtc - TimeSpan.FromSeconds(2)
            || evidence.SavedAtUtc < ticketIssuedAtUtc - TimeSpan.FromSeconds(2))
            return "The fixed AutoSave0 candidate predates its protected ticket.";
        return null;
    }

    public static bool HasMatchingEcho(PrepareOwnedWorldResumeRequest request,
        PreparedOwnedWorldResumePlan plan) =>
        IsFixedAutosaveMode(request.RecoveryMode)
        && request.MinimumRecoveryGameTick is long known && known >= 0
        && request.ExpectedRecoveryGameTick is long expected && expected >= known
        && plan.Prepared
        && plan.RecoveryMode == request.RecoveryMode
        && plan.RecoveryEvidenceVersion == EvidenceVersionFor(request.RecoveryMode)
        && plan.ExactEmbeddedIdentityVerified
        && plan.CandidateGameTick == expected
        && plan.MinimumGameTick == expected
        && plan.UserConfirmationRequired
        && !plan.CommitAllowedNow
        && plan.ConfirmationPrompt == ConfirmationPromptFor(request.RecoveryMode)
        && !string.IsNullOrWhiteSpace(plan.ConfirmationDigest)
        && IsSupportedSameVersion(plan.SourceGameVersion, plan.TargetGameVersion);

    private static bool IsSupportedSameVersion(string sourceGameVersion, string currentGameVersion) =>
        string.Equals(sourceGameVersion, currentGameVersion, StringComparison.Ordinal)
        && OwnedWorldVersionCompatibilityPolicy.IsResearchedTarget(currentGameVersion);
}
