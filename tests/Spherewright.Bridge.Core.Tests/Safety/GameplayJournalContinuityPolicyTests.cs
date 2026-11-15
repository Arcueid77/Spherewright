using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests.Safety;

public sealed class GameplayJournalContinuityPolicyTests
{
    [Fact]
    public void QuiescentCaptureRequiresThePausedTickAndAllJournalEntriesToBeDurable()
    {
        Assert.True(GameplayJournalContinuityPolicy.QuiescentCheckpointReady(true, true, true, true, 100, 100, false, null, 3, new long[] { 1, 2, 3 }));
        Assert.True(GameplayJournalContinuityPolicy.QuiescentCheckpointReady(true, true, true, true, 100, 100, false, null, 0, Array.Empty<long>()));
        Assert.False(GameplayJournalContinuityPolicy.QuiescentCheckpointReady(true, true, true, true, 100, 99, false, null, 3, new long[] { 1, 2, 3 }));
        Assert.False(GameplayJournalContinuityPolicy.QuiescentCheckpointReady(true, true, true, true, 100, 101, false, null, 3, new long[] { 1, 2, 3 }));
        Assert.False(GameplayJournalContinuityPolicy.QuiescentCheckpointReady(true, true, true, true, 100, 100, true, null, 3, new long[] { 1, 2, 3 }));
        Assert.False(GameplayJournalContinuityPolicy.QuiescentCheckpointReady(true, true, true, true, 100, 100, false, "failure", 3, new long[] { 1, 2, 3 }));
        Assert.False(GameplayJournalContinuityPolicy.QuiescentCheckpointReady(true, true, false, true, 100, 100, false, null, 3, new long[] { 1, 2, 3 }));
        Assert.False(GameplayJournalContinuityPolicy.QuiescentCheckpointReady(true, false, true, true, 100, 100, false, null, 3, new long[] { 1, 2, 3 }));
        Assert.False(GameplayJournalContinuityPolicy.QuiescentCheckpointReady(false, true, true, true, 100, 100, false, null, 3, new long[] { 1, 2, 3 }));
        Assert.False(GameplayJournalContinuityPolicy.QuiescentCheckpointReady(true, true, true, false, 100, 100, false, null, 3, new long[] { 1, 2, 3 }));
        Assert.False(GameplayJournalContinuityPolicy.QuiescentCheckpointReady(true, true, true, true, 100, 100, false, null, 2, new long[] { 1, 2, 3 }));
        Assert.False(GameplayJournalContinuityPolicy.QuiescentCheckpointReady(true, true, true, true, 100, 100, false, null, 3, new long[] { 1, 3, 4 }));
        Assert.False(GameplayJournalContinuityPolicy.QuiescentCheckpointReady(true, true, true, true, 100, 100, false, null, 0, null));
    }
    [Fact]
    public void ExactDurableCheckpoint_IsAccepted()
    {
        Assert.True(GameplayJournalContinuityPolicy.MatchesCheckpoint(
            "journal-a",
            "attached_existing_save",
            false,
            4428079,
            49,
            "journal-a",
            "attached_existing_save",
            false,
            4428079,
            Enumerable.Range(1, 49).Select(value => (long)value).ToArray()));
    }

    [Fact]
    public void NewerContinuousJournal_IsAccepted()
    {
        Assert.True(GameplayJournalContinuityPolicy.MatchesCheckpoint(
            "journal-a",
            "from_new_game",
            true,
            38,
            2,
            "journal-a",
            "from_new_game",
            true,
            38,
            new long[] { 1, 2, 3 }));
    }

    [Fact]
    public void EmptyNewWorldJournal_IsAcceptedAtZeroWatermark()
    {
        Assert.True(GameplayJournalContinuityPolicy.MatchesCheckpoint(
            "journal-a",
            "from_new_game",
            true,
            38,
            0,
            "journal-a",
            "from_new_game",
            true,
            38,
            Array.Empty<long>()));
    }

    [Theory]
    [MemberData(nameof(InvalidCheckpoints))]
    public void MissingTruncatedOrRecreatedJournal_IsRejected(
        string? actualJournalId,
        string actualTrackingMode,
        bool actualHistoricalCoverageComplete,
        long actualTrackingStartedAtGameTick,
        long[]? actualSequences)
    {
        Assert.False(GameplayJournalContinuityPolicy.MatchesCheckpoint(
            "journal-a",
            "attached_existing_save",
            false,
            4428079,
            49,
            actualJournalId,
            actualTrackingMode,
            actualHistoricalCoverageComplete,
            actualTrackingStartedAtGameTick,
            actualSequences));
    }

    public static IEnumerable<object?[]> InvalidCheckpoints()
    {
        yield return new object?[] { null, "attached_existing_save", false, 4428079L, null };
        yield return new object?[]
        {
            "journal-a",
            "attached_existing_save",
            false,
            4428079L,
            Enumerable.Range(1, 48).Select(value => (long)value).ToArray(),
        };
        yield return new object?[]
        {
            "journal-a",
            "attached_existing_save",
            false,
            18143541L,
            Enumerable.Range(1, 49).Select(value => (long)value).ToArray(),
        };
        yield return new object?[]
        {
            "journal-a",
            "attached_existing_save",
            false,
            4428079L,
            Enumerable.Range(1, 49).Select(value => value == 25 ? 26L : (long)value).ToArray(),
        };
        yield return new object?[]
        {
            "journal-a",
            "from_new_game",
            true,
            4428079L,
            Enumerable.Range(1, 49).Select(value => (long)value).ToArray(),
        };
    }
}
