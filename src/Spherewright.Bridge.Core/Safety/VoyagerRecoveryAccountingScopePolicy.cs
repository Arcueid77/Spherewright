namespace Spherewright.Bridge.Core.Safety;

/// <summary>Recovery debit belongs to one immutable journey, not to its whole loaded session.</summary>
public static class VoyagerRecoveryAccountingScopePolicy
{
    public static long? ConservedSpent(string? currentJourneyId, string? recoveredJourneyId,
        bool remotePrimaryRecovery, bool exactRecoveryScope, long projectedSpent,
        long recoveredSpent, long sourceGameTick, long? protectedProgressGameTick)
    {
        if (projectedSpent < 0) return null;
        if (!remotePrimaryRecovery) return projectedSpent;
        if (string.IsNullOrWhiteSpace(currentJourneyId) || string.IsNullOrWhiteSpace(recoveredJourneyId)) return null;
        // A later, normally started voyage retains its own projected spend.
        // This grants neither the old recovery window nor its extra ticks.
        if (!string.Equals(currentJourneyId, recoveredJourneyId, StringComparison.Ordinal)) return projectedSpent;
        // A corrupted/mixed binding for the SAME journey remains unknown.
        if (!exactRecoveryScope || !protectedProgressGameTick.HasValue) return null;
        return VoyagerRemotePrimaryRecoveryPolicy.ConservedSpent(projectedSpent, recoveredSpent,
            sourceGameTick, protectedProgressGameTick.Value);
    }
}
