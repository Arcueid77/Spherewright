using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Journals;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class OwnedWorldVersionCompatibilityPolicyTests
{
    private const long EntryCount = 91;
    private const long AdoptedTick = 73573789;
    private const string RecordedAt = "2026-09-24T00:48:28.0000000+00:00";

    [Fact]
    public void OnlyTheResearchedDirectionalVersionPairsAreSupported()
    {
        Assert.True(OwnedWorldVersionCompatibilityPolicy.IsSupportedMigration(Source, Target));
        Assert.True(OwnedWorldVersionCompatibilityPolicy.IsSupportedMigration(Source, TargetPatch));
        Assert.True(OwnedWorldVersionCompatibilityPolicy.IsSupportedMigration(TargetPatch, CurrentPatch));
        Assert.True(OwnedWorldVersionCompatibilityPolicy.IsSupportedMigration(CurrentPatch, LatestPatch));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.IsSupportedMigration(TargetPatch, LatestPatch));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.IsSupportedMigration(Source, LatestPatch));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.IsSupportedMigration(LatestPatch, CurrentPatch));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.IsSupportedMigration(Source, CurrentPatch));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.IsSupportedMigration(Target, CurrentPatch));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.IsSupportedMigration(CurrentPatch, TargetPatch));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.IsSupportedMigration(Target, Source));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.IsSupportedMigration(TargetPatch, Source));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.IsSupportedMigration(Target, TargetPatch));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.IsSupportedMigration(TargetPatch, Target));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.IsSupportedMigration(Source, "0.10.35.29058"));
    }

    [Theory]
    [InlineData("0.10.34.28529", "0.10.34.28529", true)]
    [InlineData("0.10.35.29057", "0.10.35.29057", true)]
    [InlineData("0.10.35.29088", "0.10.35.29088", true)]
    [InlineData("0.10.35.29104", "0.10.35.29104", true)]
    [InlineData("0.10.34.28529", "0.10.35.29057", true)]
    [InlineData("0.10.34.28529", "0.10.35.29088", true)]
    [InlineData("0.10.35.29088", "0.10.35.29104", true)]
    [InlineData("0.10.34.28529", "0.10.35.29104", false)]
    [InlineData("0.10.35.29057", "0.10.35.29104", false)]
    [InlineData("0.10.35.29104", "0.10.35.29088", false)]
    [InlineData("0.10.35.29057", "0.10.34.28529", false)]
    [InlineData("0.10.35.29088", "0.10.34.28529", false)]
    [InlineData("0.10.35.29057", "0.10.35.29088", false)]
    [InlineData("0.10.35.29088", "0.10.35.29057", false)]
    [InlineData("0.10.33.00000", "0.10.35.29057", false)]
    [InlineData("0.10.34.28529", "0.10.36.00000", false)]
    [InlineData("", "0.10.35.29057", false)]
    public void ReauthorizationAllowsOnlySameVersionOrTheExactSupportedPair(
        string source, string target, bool allowed) =>
        Assert.Equal(allowed, OwnedWorldVersionCompatibilityPolicy.AllowsReauthorization(source, target));

    [Fact]
    public void ThirdTransitionPreservesTheExactExistingHistoryAndRejectsAFourth()
    {
        var first = ValidTransition(to: TargetPatch);
        var second = ValidTransition(from: TargetPatch, to: CurrentPatch, adoptedAt: AdoptedTick + 1);
        var third = ValidTransition(from: CurrentPatch, to: LatestPatch, adoptedAt: AdoptedTick + 2);
        Assert.True(OwnedWorldVersionCompatibilityPolicy.CanAppendTransition(
            Source, new[] { first, second }, EntryCount, CurrentPatch, LatestPatch, third));
        Assert.True(OwnedWorldVersionCompatibilityPolicy.JournalMatches(
            Source, new[] { first, second, third }, EntryCount, LatestPatch));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.JournalMatches(
            Source, new[] { first, third }, EntryCount, LatestPatch));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.JournalMatches(
            Source, new[] { first, second, third, third }, EntryCount, LatestPatch));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.CanAppendTransition(
            Source, new[] { first, second, third }, EntryCount, CurrentPatch, LatestPatch, third));
    }

    [Fact]
    public void NullVersionsFailClosed()
    {
        Assert.False(OwnedWorldVersionCompatibilityPolicy.AllowsReauthorization(null!, Target));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.AllowsReauthorization(Source, null!));
    }

    [Fact]
    public void SameVersionJournalNeedsNoTransition()
    {
        Assert.True(OwnedWorldVersionCompatibilityPolicy.JournalMatches(
            Source, Array.Empty<GameplayJournalVersionTransition>(), EntryCount, Source));
    }

    [Fact]
    public void ExactDurableTransitionAllowsFutureNormalResumeOnTheTarget()
    {
        Assert.True(OwnedWorldVersionCompatibilityPolicy.JournalMatches(
            Source, new[] { ValidTransition() }, EntryCount, Target));
    }

    [Fact]
    public void ExactDurableTransitionAllowsFutureNormalResumeOnThePatchTarget()
    {
        Assert.True(OwnedWorldVersionCompatibilityPolicy.JournalMatches(
            Source, new[] { ValidTransition(to: TargetPatch) }, EntryCount, TargetPatch));
    }

    [Fact]
    public void ExistingJournalCanProveTheExactSecondPatchTransition()
    {
        var first = ValidTransition(to: TargetPatch);
        var second = ValidTransition(from: TargetPatch, to: CurrentPatch,
            adoptedAt: AdoptedTick + 1, recordedAt: "2026-09-29T00:48:28.0000000+00:00");

        Assert.True(OwnedWorldVersionCompatibilityPolicy.JournalMatches(
            Source, new[] { first }, EntryCount, TargetPatch));
        Assert.True(OwnedWorldVersionCompatibilityPolicy.JournalMatches(
            Source, new[] { first, second }, EntryCount, CurrentPatch));
        Assert.True(OwnedWorldVersionCompatibilityPolicy.JournalMatches(
            TargetPatch, new[] { second }, EntryCount, CurrentPatch));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.JournalMatches(
            Source, new[] { first }, EntryCount, CurrentPatch));
        Assert.True(OwnedWorldVersionCompatibilityPolicy.CanAppendTransition(
            Source, new[] { first }, EntryCount, TargetPatch, CurrentPatch, second));
    }

    [Theory]
    [InlineData("same-tick")]
    [InlineData("sequence-backwards")]
    [InlineData("wrong-source")]
    [InlineData("unsupported-pair")]
    [InlineData("already-complete")]
    public void PrePersistenceTransitionValidationRejectsUnsafeAppend(string fault)
    {
        var first = ValidTransition(to: TargetPatch);
        var candidate = ValidTransition(from: TargetPatch, to: CurrentPatch,
            adoptedAt: AdoptedTick + 1);
        IReadOnlyList<GameplayJournalVersionTransition> existing = new[] { first };
        var source = TargetPatch;
        if (fault == "same-tick") candidate.AdoptedAtGameTick = AdoptedTick;
        if (fault == "sequence-backwards") candidate.DurableThroughSequence = EntryCount - 1;
        if (fault == "wrong-source") candidate.FromGameVersion = Target;
        if (fault == "unsupported-pair") source = Target;
        if (fault == "already-complete") existing = new[] { first, candidate };

        Assert.False(OwnedWorldVersionCompatibilityPolicy.CanAppendTransition(
            Source, existing, EntryCount, source, CurrentPatch, candidate));
    }

    [Theory]
    [InlineData("skip-first")]
    [InlineData("wrong-second-source")]
    [InlineData("reversed-tick")]
    [InlineData("reversed-sequence")]
    [InlineData("third-transition")]
    public void MalformedSecondPatchTransitionFailsClosed(string fault)
    {
        var first = ValidTransition(to: TargetPatch);
        var second = ValidTransition(from: TargetPatch, to: CurrentPatch,
            adoptedAt: AdoptedTick + 1, recordedAt: "2026-09-29T00:48:28.0000000+00:00");
        IReadOnlyList<GameplayJournalVersionTransition> transitions = fault switch
        {
            "skip-first" => new[] { second },
            "wrong-second-source" => new[] { first, ValidTransition(from: Target, to: CurrentPatch, adoptedAt: AdoptedTick + 1) },
            "reversed-tick" => new[] { first, ValidTransition(from: TargetPatch, to: CurrentPatch, adoptedAt: AdoptedTick) },
            "reversed-sequence" => new[] { first, ValidTransition(from: TargetPatch, to: CurrentPatch, adoptedAt: AdoptedTick + 1, sequence: EntryCount - 1) },
            _ => new[] { first, second, second },
        };

        Assert.False(OwnedWorldVersionCompatibilityPolicy.JournalMatches(
            Source, transitions, EntryCount, CurrentPatch));
    }

    [Fact]
    public void UnsupportedContinuous29057To29104ChainFailsClosed()
    {
        var first = ValidTransition(to: Target);
        var second = ValidTransition(from: Target, to: CurrentPatch, adoptedAt: AdoptedTick + 1);

        Assert.False(OwnedWorldVersionCompatibilityPolicy.JournalMatches(
            Source, new[] { first, second }, EntryCount, CurrentPatch));
        Assert.False(OwnedWorldVersionCompatibilityPolicy.CanAppendTransition(
            Source, new[] { first }, EntryCount, Target, CurrentPatch, second));
    }

    [Theory]
    [InlineData("null-transitions")]
    [InlineData("duplicate-transitions")]
    [InlineData("malformed-recorded-at")]
    [InlineData("future-sequence")]
    [InlineData("negative-adopted-tick")]
    [InlineData("wrong-source")]
    [InlineData("wrong-target")]
    [InlineData("reverse")]
    [InlineData("cross-researched-targets")]
    [InlineData("unexpected-expected-version")]
    public void InvalidOrNonDirectionalJournalTransitionsFailClosed(string change)
    {
        IReadOnlyList<GameplayJournalVersionTransition>? transitions = new[] { ValidTransition() };
        var expected = Target;

        switch (change)
        {
            case "null-transitions": transitions = null; break;
            case "duplicate-transitions": transitions = new[] { ValidTransition(), ValidTransition() }; break;
            case "malformed-recorded-at": transitions = new[] { ValidTransition(recordedAt: "not-a-date") }; break;
            case "future-sequence": transitions = new[] { ValidTransition(sequence: EntryCount + 1) }; break;
            case "negative-adopted-tick": transitions = new[] { ValidTransition(adoptedAt: -1) }; break;
            case "wrong-source": transitions = new[] { ValidTransition(from: "0.10.33.00000") }; break;
            case "wrong-target": transitions = new[] { ValidTransition(to: "0.10.35.29058") }; break;
            case "reverse": transitions = new[] { ValidTransition(from: Target, to: Source) }; break;
            case "cross-researched-targets":
                transitions = new[] { ValidTransition(from: Target, to: TargetPatch) };
                expected = TargetPatch;
                break;
            case "unexpected-expected-version": expected = Source; break;
        }

        Assert.False(OwnedWorldVersionCompatibilityPolicy.JournalMatches(Source, transitions, EntryCount, expected));
    }

    [Fact]
    public void NullTransitionEntryFailsClosed()
    {
        IReadOnlyList<GameplayJournalVersionTransition> transitions = new GameplayJournalVersionTransition[] { null! };

        Assert.False(OwnedWorldVersionCompatibilityPolicy.JournalMatches(Source, transitions, EntryCount, Target));
    }

    private const string Source = OwnedWorldVersionCompatibilityPolicy.SourceVersion;
    private const string Target = OwnedWorldVersionCompatibilityPolicy.TargetVersion;
    private const string TargetPatch = OwnedWorldVersionCompatibilityPolicy.TargetPatchVersion;
    private const string CurrentPatch = OwnedWorldVersionCompatibilityPolicy.CurrentPatchVersion;
    private const string LatestPatch = OwnedWorldVersionCompatibilityPolicy.LatestPatchVersion;

    private static GameplayJournalVersionTransition ValidTransition(
        string from = Source,
        string to = Target,
        long adoptedAt = AdoptedTick,
        long sequence = EntryCount,
        string recordedAt = RecordedAt) => new()
    {
        FromGameVersion = from,
        ToGameVersion = to,
        AdoptedAtGameTick = adoptedAt,
        DurableThroughSequence = sequence,
        RecordedAtUtc = recordedAt,
    };
}
