namespace Spherewright.Bridge.Core.Safety;

public static class GameplayJournalContinuityPolicy
{
    // This is a read-only quiescence check. It never scans, persists, resumes,
    // or treats an unknown pending write as a completed Journal update.
    public static bool QuiescentCheckpointReady(bool owned, bool sameSession, bool nativePaused, bool gameRunning,
        long currentGameTick, long lastScannedGameTick, bool persistencePending, string? persistenceError,
        long durableThroughSequence, IReadOnlyList<long>? sequences)
    {
        if (!owned || !sameSession || !nativePaused || !gameRunning || currentGameTick < 0
            || lastScannedGameTick != currentGameTick || persistencePending || !string.IsNullOrWhiteSpace(persistenceError)
            || durableThroughSequence < 0 || !HasContinuousSequence(sequences)) return false;
        var last = sequences is { Count: > 0 } ? sequences[sequences.Count - 1] : 0L;
        return last == durableThroughSequence;
    }

    public static bool MatchesCheckpoint(
        string? expectedJournalId,
        string? expectedTrackingMode,
        bool expectedHistoricalCoverageComplete,
        long expectedTrackingStartedAtGameTick,
        long minimumDurableThroughSequence,
        string? actualJournalId,
        string? actualTrackingMode,
        bool actualHistoricalCoverageComplete,
        long actualTrackingStartedAtGameTick,
        IReadOnlyList<long>? actualSequences)
    {
        if (string.IsNullOrWhiteSpace(expectedJournalId)
            || string.IsNullOrWhiteSpace(expectedTrackingMode)
            || expectedTrackingStartedAtGameTick < 0
            || minimumDurableThroughSequence < 0
            || !string.Equals(expectedJournalId, actualJournalId, StringComparison.Ordinal)
            || !string.Equals(expectedTrackingMode, actualTrackingMode, StringComparison.Ordinal)
            || expectedHistoricalCoverageComplete != actualHistoricalCoverageComplete
            || expectedTrackingStartedAtGameTick != actualTrackingStartedAtGameTick
            || !HasContinuousSequence(actualSequences))
        {
            return false;
        }

        var durableThroughSequence = actualSequences is { Count: > 0 }
            ? actualSequences[actualSequences.Count - 1]
            : 0L;
        return durableThroughSequence >= minimumDurableThroughSequence;
    }

    public static bool HasContinuousSequence(IReadOnlyList<long>? sequences)
    {
        if (sequences is null)
        {
            return false;
        }

        for (var index = 0; index < sequences.Count; index++)
        {
            if (sequences[index] != index + 1L)
            {
                return false;
            }
        }

        return true;
    }
}
