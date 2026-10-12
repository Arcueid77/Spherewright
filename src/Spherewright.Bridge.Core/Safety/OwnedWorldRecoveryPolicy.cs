using Spherewright.Contracts.Sessions;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>Explicit recovery is not an automatic fallback or an arbitrary save picker.</summary>
public static class OwnedWorldRecoveryPolicy
{
    public const int EvidenceVersion = 1;
    public const long MaximumAdoptionAdvanceTicks = 3600;

    public static string? ValidateRequest(PrepareOwnedWorldResumeRequest request)
    {
        if (request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary)
            return OwnedWorldPrimaryMigrationPolicy.ValidateRequest(request);
        if (OwnedWorldAutosaveRecoveryPolicy.IsFixedAutosaveMode(request.RecoveryMode))
            return OwnedWorldAutosaveRecoveryPolicy.ValidateRequest(request);
        if (request.RecoveryMode == OwnedWorldResumeModes.ReauthorizeExpiredPrimary)
            return request.UserConfirmedInConversation || request.MinimumRecoveryGameTick.HasValue
                || request.ExpectedRecoveryGameTick.HasValue
                ? "Expired-primary prepare is read-only disclosure; confirmation belongs to a subsequent commit, not prepare."
                : null;
        if (request.RecoveryMode == OwnedWorldResumeModes.Default)
            return request.UserConfirmedInConversation || request.MinimumRecoveryGameTick.HasValue
                || request.ExpectedRecoveryGameTick.HasValue
                ? "Recovery confirmation and tick bounds require the explicit verified-newer-LastExit mode." : null;
        if (request.RecoveryMode != OwnedWorldResumeModes.VerifiedNewerLastExit)
            return "Unsupported recovery mode; arbitrary saves and autosave selection are unavailable.";
        if (!request.UserConfirmedInConversation)
            return "Explicit conversation confirmation is required for newer fixed LastExit recovery.";
        if (request.MinimumRecoveryGameTick is not long minimum || minimum < 0
            || request.ExpectedRecoveryGameTick is not long expected || expected < minimum)
            return "The known progress floor and exact approved candidate tick are required.";
        return null;
    }

    public static string? ValidateCandidate(PrepareOwnedWorldResumeRequest request,
        bool healthyTicket, bool journalCheckpointVerified, long ticketMinimumGameTick,
        DateTimeOffset ticketIssuedAtUtc, string currentGameVersion,
        OwnedSavePrefixEvidence evidence, DateTimeOffset writtenAtUtc, long? primaryGameTick)
    {
        var invalid = ValidateRequest(request);
        if (invalid is not null) return invalid;
        if (request.RecoveryMode != OwnedWorldResumeModes.VerifiedNewerLastExit
            || !healthyTicket || !journalCheckpointVerified)
            return "Verified recovery requires a healthy protected ticket and its verified durable Journal checkpoint.";
        if (request.MinimumRecoveryGameTick < ticketMinimumGameTick
            || evidence.GameTick != request.ExpectedRecoveryGameTick
            || evidence.GameTick < request.MinimumRecoveryGameTick
            || evidence.GameTick <= ticketMinimumGameTick
            || primaryGameTick >= evidence.GameTick)
            return "The fixed LastExit is not the exact approved newer candidate covering all known progress.";
        if (!evidence.MatchesExpectedIdentity || !evidence.Peaceful
            || !string.Equals(evidence.GameVersion, currentGameVersion, StringComparison.Ordinal))
            return "The candidate must prove the complete protected identity, current game version and peaceful mode.";
        if (writtenAtUtc < ticketIssuedAtUtc - TimeSpan.FromSeconds(2)
            || evidence.SavedAtUtc < ticketIssuedAtUtc - TimeSpan.FromSeconds(2))
            return "The candidate predates its protected ticket.";
        return null;
    }

    public static bool AllowsAdoption(long loadedGameTick, long minimumGameTick, bool verifiedRecovery) =>
        loadedGameTick >= minimumGameTick
        && (!verifiedRecovery || loadedGameTick - minimumGameTick <= MaximumAdoptionAdvanceTicks);

    public static bool HasMatchingEcho(PrepareOwnedWorldResumeRequest request, PreparedOwnedWorldResumePlan plan) =>
        plan.RecoveryMode == OwnedWorldResumeModes.VerifiedNewerLastExit
        && plan.RecoveryEvidenceVersion == EvidenceVersion
        && plan.ExactEmbeddedIdentityVerified
        && plan.CandidateGameTick == request.ExpectedRecoveryGameTick
        && plan.MinimumGameTick == request.ExpectedRecoveryGameTick;
}
