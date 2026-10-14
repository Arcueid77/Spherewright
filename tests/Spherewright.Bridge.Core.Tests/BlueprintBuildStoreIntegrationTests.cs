using BepInEx.Logging;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Journals;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

// Linked production store with test-only session, logging and ACL/I/O substitutes.
// These tests do not load DSP or prove native construction, adoption or Windows ACLs.
public sealed class BlueprintBuildStoreIntegrationTests
{
    private const string SourceVersion = "0.10.35.29131";
    private const string TargetVersion = "0.10.35.29145";
    private const string Identity = "synthetic-completed-build-identity";
    private const string Session = "synthetic-current-29145-session";

    [Fact]
    public void ExactDurableAdoptionReadsCompletedHistoryWithoutRewritingOrRestoringWriteAuthority()
    {
        using var fixture = new Fixture();
        var completed = CreateBuild(completed: true);
        fixture.Seed(completed);
        var original = File.ReadAllBytes(fixture.Path);
        var store = fixture.Store(TargetVersion, Journal());
        Assert.True(store.TryRead(out var builds));
        Assert.Equal(PluginJson.Serialize(completed), PluginJson.Serialize(Assert.Single(builds)));
        Assert.Throws<InvalidOperationException>(() => builds[0].Begin(Session, Guid.NewGuid().ToString(), 1, 200));
        builds[0].Objects[0].EntityId = 999; // Returned clone cannot change durable history.
        Assert.True(store.TryRead(out var reread));
        Assert.Equal(100, Assert.Single(reread).Objects[0].EntityId);
        Assert.Equal(original, File.ReadAllBytes(fixture.Path));
    }

    [Fact]
    public void NextOrdinaryUpdateStampsTargetVersionAndPreservesEveryOriginalReceipt()
    {
        using var fixture = new Fixture();
        var completed = CreateBuild(completed: true);
        fixture.Seed(completed);
        var store = fixture.Store(TargetVersion, Journal());
        var next = CreateBuild(completed: false, session: Session);
        Assert.True(store.TryPut(next));
        var document = PluginJson.DeserializeReplacingCollections<BlueprintBuildDocument>(File.ReadAllText(fixture.Path))!;
        Assert.Equal(TargetVersion, document.GameVersion);
        Assert.Equal(2, document.Builds.Count);
        Assert.Equal(PluginJson.Serialize(completed), PluginJson.Serialize(document.Builds[0]));
        Assert.Equal(PluginJson.Serialize(next), PluginJson.Serialize(document.Builds[1]));
        Assert.True(fixture.Store(TargetVersion, null).TryRead(out var restored));
        Assert.Equal(2, restored.Count);
        Assert.Equal(100, restored[0].Objects[0].EntityId);
    }

    [Theory]
    [InlineData("missing-journal")]
    [InlineData("pending")]
    [InlineData("persistence-error")]
    [InlineData("wrong-session")]
    [InlineData("wrong-identity")]
    [InlineData("missing-fourth")]
    [InlineData("skipped-link")]
    [InlineData("noncontinuous-sequence")]
    [InlineData("undurable-entries")]
    [InlineData("future-adoption-tick")]
    [InlineData("progress-ahead-of-load")]
    [InlineData("unfinished-build")]
    [InlineData("corrupt-plan")]
    [InlineData("unknown-runtime")]
    public void MissingUnsafeOrUnfinishedEvidenceNeverClearsOrOverwritesOriginalProgress(string fault)
    {
        using var fixture = new Fixture();
        fixture.Seed(CreateBuild(completed: fault != "unfinished-build"));
        if (fault == "corrupt-plan")
        {
            var document = PluginJson.DeserializeReplacingCollections<BlueprintBuildDocument>(File.ReadAllText(fixture.Path))!;
            document.Builds[0].PlanHash = "modified-plan";
            File.WriteAllText(fixture.Path, PluginJson.Serialize(document));
        }
        var original = File.ReadAllBytes(fixture.Path);
        GameplayJournalSnapshot? journal = Journal();
        switch (fault)
        {
            case "missing-journal": journal = null; break;
            case "pending": journal.PersistencePending = true; break;
            case "persistence-error": journal.PersistenceError = "test-only persistence failure"; break;
            case "wrong-session": journal.SessionId = "another-session"; break;
            case "wrong-identity": journal.JournalId = "another-owned-identity"; break;
            case "missing-fourth": journal.VersionTransitions.RemoveAt(3); break;
            case "skipped-link": journal.VersionTransitions.RemoveAt(1); break;
            case "noncontinuous-sequence": journal.Entries[1].Sequence = 3; break;
            case "undurable-entries": journal.DurableThroughSequence = 1; break;
            case "future-adoption-tick": journal.VersionTransitions[3].AdoptedAtGameTick = 201; break;
            case "progress-ahead-of-load": journal.CapturedAtGameTick = 100; break;
        }
        var store = fixture.Store(fault == "unknown-runtime" ? "0.10.35.29146" : TargetVersion, journal);
        Assert.False(store.TryRead(out _));
        Assert.False(store.TryPut(CreateBuild(completed: false, session: Session)));
        Assert.Equal(original, File.ReadAllBytes(fixture.Path));
    }

    [Fact]
    public void FailedDurableUpdateLeavesOriginalSourceVersionAndReceiptsIntact()
    {
        using var fixture = new Fixture();
        fixture.Seed(CreateBuild(completed: true));
        var original = File.ReadAllBytes(fixture.Path);
        File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(fixture.Path)!, ".test-only-fail-secure-write"), "fail");
        Assert.False(fixture.Store(TargetVersion, Journal()).TryPut(CreateBuild(completed: false, session: Session)));
        Assert.Equal(original, File.ReadAllBytes(fixture.Path));
        Assert.True(fixture.Store(TargetVersion, Journal()).TryRead(out var retained));
        Assert.Equal(100, Assert.Single(retained).Objects[0].EntityId);
    }

    private static BlueprintBuildState CreateBuild(bool completed, string session = "synthetic-source-session")
    {
        var state = BlueprintBuildState.Create(new BlueprintSiteSnapshot
        {
            SessionId = session, PlanetId = 104, CapturedAtGameTick = 100, BlueprintHash = "synthetic-approved-shape",
            NativeCheckPerformed = true, NativeCheckPassed = true, InventorySufficient = true, TechnologySatisfied = true,
            Objects = new() { new() { Index = 0, ItemId = 2001 } },
        });
        if (completed)
        {
            state.Begin(session, Guid.NewGuid().ToString(), 1, 100);
            state.BeforeSubmit(0, 101, 2, "synthetic-before");
            state.ConfirmSubmission(0, 8, 1, "synthetic-after");
            state.ConfirmCompletion(0, 100, 110);
            state.Stop("completed");
        }
        return state;
    }

    private static GameplayJournalSnapshot Journal() => new()
    {
        SessionId = Session, JournalId = GameplayJournalIdentity.HashOwnedSaveIdentity(Identity),
        OriginGameVersion = "0.10.34.28529", CurrentGameVersion = TargetVersion,
        CapturedAtGameTick = 200, DurableThroughSequence = 2,
        Entries = new() { new() { Sequence = 1 }, new() { Sequence = 2 } },
        VersionTransitions = new()
        {
            Transition("0.10.34.28529", "0.10.35.29088", 10),
            Transition("0.10.35.29088", "0.10.35.29104", 20),
            Transition("0.10.35.29104", SourceVersion, 30),
            Transition(SourceVersion, TargetVersion, 120),
        },
    };

    private static GameplayJournalVersionTransition Transition(string source, string target, long tick) => new()
    {
        FromGameVersion = source, ToGameVersion = target, AdoptedAtGameTick = tick,
        DurableThroughSequence = 2, RecordedAtUtc = "2026-10-15T00:00:00Z",
    };

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Spherewright", "blueprint-store", Guid.NewGuid().ToString("N"));
        private readonly GameSessionTracker _sessions = new() { IsCurrentSessionOwned = true, OwnedSaveName = Identity, SessionId = Session };
        internal string Path => System.IO.Path.Combine(_root, "foundry", "blueprints-"
            + CanonicalStateHash.Combine("spherewright-blueprint-progress-owned-v1", Identity).Substring(7) + ".json");
        internal BlueprintBuildStore Store(string version, GameplayJournalSnapshot? journal) =>
            new(_root, version, _sessions, new ManualLogSource(), () => journal);
        internal void Seed(BlueprintBuildState state) => Assert.True(Store(SourceVersion, null).TryPut(state));
        public void Dispose()
        {
            _sessions.Dispose();
            Directory.Delete(_root, recursive: true); // Only the newly created GUID-owned test tree.
        }
    }
}
