using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Journals;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class BlueprintProgressVersionPolicyTests
{
    private const string Stored = "0.10.35.29131";
    private const string Previous = "0.10.35.29145";
    private const string Current = "0.10.35.29151";
    private const string Session = "synthetic-owned-session";
    private const string Journal = "synthetic-owned-journal";

    [Theory]
    [InlineData(Stored, Previous)]
    [InlineData(Previous, Current)]
    [InlineData(Stored, Current)]
    public void CompletedReceiptsSurviveOnlyTheProvenJournalSuffix(string stored, string current)
    {
        var journal = CompleteJournal(current);
        var builds = CompletedBuilds();
        Assert.True(BlueprintProgressVersionPolicy.CanRetainCompletedHistory(
            stored, current, Session, Journal, journal, builds));
        Assert.Equal(5, builds[0].Objects.Count);
        Assert.All(builds[0].Objects, item => Assert.Equal(BlueprintObjectStates.Completed, item.State));
        Assert.Equal(19, builds[0].Generation);
    }

    [Theory]
    [InlineData("missing-fourth")]
    [InlineData("missing-fifth")]
    [InlineData("reversed-tick")]
    [InlineData("pending-journal")]
    [InlineData("journal-error")]
    [InlineData("undurable-entry")]
    [InlineData("sequence-gap")]
    [InlineData("another-session")]
    [InlineData("another-journal")]
    [InlineData("unfinished-build")]
    [InlineData("unfinished-object")]
    [InlineData("future-receipt")]
    [InlineData("negative-receipt-tick")]
    [InlineData("uncaptured-transition")]
    [InlineData("null-transition")]
    public void RejectedHistoryNeverBecomesPermissionToResumeOrRewrite(string fault)
    {
        var journal = CompleteJournal(Current);
        var builds = CompletedBuilds();
        switch (fault)
        {
            case "missing-fourth": journal.VersionTransitions.RemoveAt(3); break;
            case "missing-fifth": journal.VersionTransitions.RemoveAt(4); break;
            case "reversed-tick": journal.VersionTransitions[4].AdoptedAtGameTick = 1003; break;
            case "pending-journal": journal.PersistencePending = true; break;
            case "journal-error": journal.PersistenceError = "synthetic storage failure"; break;
            case "undurable-entry": journal.DurableThroughSequence = 1; break;
            case "sequence-gap": journal.Entries[1].Sequence = 3; break;
            case "another-session": journal.SessionId = "another-session"; break;
            case "another-journal": journal.JournalId = "another-journal"; break;
            case "unfinished-build": builds[0].Phase = "prepared"; break;
            case "unfinished-object": builds[0].Objects[0].State = BlueprintObjectStates.PendingConstruction; break;
            case "future-receipt": builds[0].LastEvidenceGameTick = 1004; break;
            case "negative-receipt-tick": builds[0].LastEvidenceGameTick = -1; break;
            case "uncaptured-transition": journal.CapturedAtGameTick = 1003; break;
            case "null-transition": journal.VersionTransitions[4] = null!; break;
        }
        Assert.False(BlueprintProgressVersionPolicy.CanRetainCompletedHistory(
            Stored, Current, Session, Journal, journal, builds));
        Assert.Equal(5, builds[0].Objects.Count);
        Assert.Equal(19, builds[0].Generation);
    }

    [Theory]
    [InlineData("0.10.35.29104", Current)]
    [InlineData(Current, Previous)]
    [InlineData(Stored, "0.10.35.29152")]
    public void OtherStoredVersionsDowngradesAndFutureTargetsStayRejected(string stored, string current) =>
        Assert.False(BlueprintProgressVersionPolicy.CanRetainCompletedHistory(
            stored, current, Session, Journal, CompleteJournal(Current), CompletedBuilds()));

    private static List<BlueprintBuildState> CompletedBuilds() => new()
    {
        new BlueprintBuildState
        {
            Phase = "completed", LastEvidenceGameTick = 1002, Generation = 19,
            Objects = Enumerable.Range(0, 5).Select(index => new BlueprintObjectProgress
            { Index = index, State = BlueprintObjectStates.Completed }).ToList(),
        },
    };

    private static GameplayJournalSnapshot CompleteJournal(string current)
    {
        var versions = new[] { "0.10.34.28529", "0.10.35.29088", "0.10.35.29104", Stored, Previous, Current };
        var count = current == Previous ? 4 : 5;
        return new GameplayJournalSnapshot
        {
            OriginGameVersion = versions[0], CurrentGameVersion = current,
            SessionId = Session, JournalId = Journal, CapturedAtGameTick = 1010, DurableThroughSequence = 2,
            Entries = new() { new GameplayJournalEntry { Sequence = 1 }, new GameplayJournalEntry { Sequence = 2 } },
            VersionTransitions = Enumerable.Range(0, count).Select(index => new GameplayJournalVersionTransition
            {
                FromGameVersion = versions[index], ToGameVersion = versions[index + 1],
                AdoptedAtGameTick = 1000 + index, DurableThroughSequence = 2, RecordedAtUtc = "2026-10-17T00:00:00Z",
            }).ToList(),
        };
    }
}
