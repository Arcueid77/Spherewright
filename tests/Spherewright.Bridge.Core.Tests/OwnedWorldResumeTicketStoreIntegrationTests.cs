using System.Security.Cryptography;
using System.Text;
using BepInEx.Logging;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Journals;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class OwnedWorldResumeTicketStoreIntegrationTests
{
    private const string GameVersion = "0.10.34.28529";
    private const string TargetGameVersion = "0.10.35.29057";
    private const string TargetPatchGameVersion = "0.10.35.29088";
    private const string CurrentPatchGameVersion = "0.10.35.29104";
    private const string LatestPatchGameVersion = "0.10.35.29131";
    private const string NextPatchGameVersion = "0.10.35.29145";
    private const string CurrentNativeGameVersion = "0.10.35.29151";
    private const string OwnedSaveName = "synthetic-protected-world-identity";
    private const string SessionId = "synthetic-session";
    private const int PlanetId = 104;
    private const long MinimumTick = 12345;
    private const string ConfirmationDigest = "sha256:test-confirmation";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveVersionPrimaryUsesExactHistoryAndCannotBecomeDefaultOrAutosaveAuthority(bool latestMigration)
    {
        var source = latestMigration ? LatestPatchGameVersion : CurrentPatchGameVersion;
        var target = latestMigration ? NextPatchGameVersion : LatestPatchGameVersion;
        using var scenario = VersionTransitionScenario.CreateActive(
            source, GameVersion, latestMigration ? OriginalThreeTransitions() : OriginalTwoTransitions());
        var store = scenario.OpenStore(target);
        Assert.False(store.TryGetActiveTicket(scenario.Token, out _, out _));
        Assert.False(store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out _, out _));
        Assert.False(store.TryGetFixedAutosaveProvenance(scenario.Token, out _, out _, out _));
        Assert.True(store.TryGetVersionChangedPrimaryProvenance(scenario.Token,
            out var ticket, out var fingerprint, out var rejection));
        Assert.Equal(source, ticket!.GameVersion);
        Assert.Equal(string.Empty, rejection);
        Assert.False(store.TryConsumeReauthorization(scenario.Token, fingerprint,
            Guid.NewGuid().ToString(), ConfirmationDigest, requireActiveCredential: true));
        using (store.OpenReauthorizationJournalLease(ticket, fingerprint,
            requireActiveCredential: true, versionChangedPrimary: true)) { }
        Assert.True(store.TryConsumeReauthorization(scenario.Token, fingerprint,
            Guid.NewGuid().ToString(), ConfirmationDigest, requireActiveCredential: true, versionChangedPrimary: true));
        Assert.False(scenario.OpenStore(target).TryGetVersionChangedPrimaryProvenance(
            scenario.Token, out _, out _, out _));
    }

    [Theory]
    [InlineData("missing-link")]
    [InlineData("reversed-tick")]
    [InlineData("future-sequence")]
    [InlineData("unknown-pair")]
    public void NewActivePrimaryRequiresTheUnbrokenOriginalThreeTransitionHistory(string fault)
    {
        var transitions = OriginalThreeTransitions();
        switch (fault)
        {
            case "missing-link": transitions = new[] { transitions[0], transitions[2] }; break;
            case "reversed-tick": transitions[2].AdoptedAtGameTick = transitions[1].AdoptedAtGameTick; break;
            case "future-sequence": transitions[2].DurableThroughSequence = 3; break;
            case "unknown-pair": transitions[1].FromGameVersion = "0.10.35.29057"; break;
        }
        using var scenario = VersionTransitionScenario.CreateActive(LatestPatchGameVersion, GameVersion, transitions);
        Assert.False(scenario.OpenStore(NextPatchGameVersion).TryGetVersionChangedPrimaryProvenance(
            scenario.Token, out _, out _, out _));
        Assert.True(File.Exists(scenario.RuntimeTicketPath));
        Assert.True(File.Exists(scenario.HandoffTicketPath));
        Assert.False(File.Exists(scenario.RuntimeTombstonePath));
    }

    [Fact]
    public void FutureNormal29145ResumeNeedsTheExactDurableFourthTransition()
    {
        var fourth = new GameplayJournalVersionTransition
        {
            FromGameVersion = LatestPatchGameVersion, ToGameVersion = NextPatchGameVersion,
            AdoptedAtGameTick = MinimumTick, DurableThroughSequence = 2, RecordedAtUtc = "2026-10-15T00:00:00Z",
        };
        using var completed = VersionTransitionScenario.CreateActive(
            NextPatchGameVersion, GameVersion, OriginalThreeTransitions().Append(fourth).ToArray());
        Assert.True(completed.Store.TryGetActiveTicket(completed.Token, out _, out _));
        Assert.False(completed.Store.TryGetVersionChangedPrimaryProvenance(completed.Token, out _, out _, out _));
        using var missingFourth = VersionTransitionScenario.CreateActive(
            NextPatchGameVersion, GameVersion, OriginalThreeTransitions());
        Assert.False(missingFourth.Store.TryGetActiveTicket(missingFourth.Token, out _, out _));
    }

    [Fact]
    public void ActiveVersionPrimaryCannotAcceptExpiredProvenance()
    {
        using var scenario = VersionTransitionScenario.CreateExpired(
            CurrentPatchGameVersion, LatestPatchGameVersion, GameVersion, OriginalTwoTransitions());
        Assert.False(scenario.Store.TryGetVersionChangedPrimaryProvenance(scenario.Token, out _, out _, out _));
        Assert.True(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out _, out _));
    }

    [Theory]
    [InlineData("0.10.35.29104")]
    [InlineData("0.10.35.29132")]
    [InlineData("0.10.35.29088")]
    public void ActiveVersionPrimaryRejectsSameUnknownAndDowngradedRuntime(string runtime)
    {
        using var scenario = VersionTransitionScenario.CreateActive(
            CurrentPatchGameVersion, GameVersion, OriginalTwoTransitions());
        Assert.False(scenario.OpenStore(runtime).TryGetVersionChangedPrimaryProvenance(
            scenario.Token, out _, out _, out _));
    }

    private static GameplayJournalVersionTransition[] OriginalTwoTransitions() => new[]
    {
        new GameplayJournalVersionTransition { FromGameVersion = GameVersion, ToGameVersion = TargetPatchGameVersion,
            AdoptedAtGameTick = MinimumTick - 2, DurableThroughSequence = 2, RecordedAtUtc = "2026-09-24T00:00:00Z" },
        new GameplayJournalVersionTransition { FromGameVersion = TargetPatchGameVersion, ToGameVersion = CurrentPatchGameVersion,
            AdoptedAtGameTick = MinimumTick - 1, DurableThroughSequence = 2, RecordedAtUtc = "2026-09-29T00:00:00Z" },
    };

    private static GameplayJournalVersionTransition[] OriginalThreeTransitions() => new[]
    {
        new GameplayJournalVersionTransition { FromGameVersion = GameVersion, ToGameVersion = TargetPatchGameVersion,
            AdoptedAtGameTick = MinimumTick - 3, DurableThroughSequence = 2, RecordedAtUtc = "2026-09-24T00:00:00Z" },
        new GameplayJournalVersionTransition { FromGameVersion = TargetPatchGameVersion, ToGameVersion = CurrentPatchGameVersion,
            AdoptedAtGameTick = MinimumTick - 2, DurableThroughSequence = 2, RecordedAtUtc = "2026-09-29T00:00:00Z" },
        new GameplayJournalVersionTransition { FromGameVersion = CurrentPatchGameVersion, ToGameVersion = LatestPatchGameVersion,
            AdoptedAtGameTick = MinimumTick - 1, DurableThroughSequence = 2, RecordedAtUtc = "2026-10-13T00:00:00Z" },
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Expired29145PrimaryPreviews29151OnlyWithItsCompleteOriginalHistory(bool missingFourth)
    {
        var history = OriginalFourTransitions();
        if (missingFourth) history = history.Take(3).ToArray();
        using var scenario = VersionTransitionScenario.CreateExpired(
            NextPatchGameVersion, CurrentNativeGameVersion, GameVersion, history);
        var beforeTicket = File.ReadAllBytes(scenario.RuntimeTicketPath);
        var beforeHandoff = File.ReadAllBytes(scenario.HandoffTicketPath);
        Assert.False(scenario.Store.TryGetActiveTicket(scenario.Token, out _, out _));
        Assert.Equal(!missingFourth, scenario.Store.TryGetExpiredPrimaryProvenance(
            scenario.Token, out var ticket, out _, out _));
        if (!missingFourth) Assert.Equal(NextPatchGameVersion, ticket!.GameVersion);
        Assert.Equal(beforeTicket, File.ReadAllBytes(scenario.RuntimeTicketPath));
        Assert.Equal(beforeHandoff, File.ReadAllBytes(scenario.HandoffTicketPath));
        Assert.False(File.Exists(scenario.RuntimeAttemptPath));
        Assert.False(File.Exists(scenario.RuntimeTombstonePath));
    }

    [Fact]
    public void FifthMigrationDoesNotExpandActiveCredentialAuthority()
    {
        using var scenario = VersionTransitionScenario.CreateActive(
            NextPatchGameVersion, GameVersion, OriginalFourTransitions());
        var store = scenario.OpenStore(CurrentNativeGameVersion);
        Assert.False(store.TryGetActiveTicket(scenario.Token, out _, out _));
        Assert.False(store.TryGetVersionChangedPrimaryProvenance(scenario.Token, out _, out _, out _));
        Assert.False(store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out _, out _));
        Assert.False(store.TryGetFixedAutosaveProvenance(scenario.Token, out _, out _, out _));
    }

    [Fact]
    public void Normal29151ResumeNeedsItsDurableFifthTransitionAndKeepsDefaultSameVersionOnly()
    {
        var history = OriginalFourTransitions().Append(new GameplayJournalVersionTransition
        {
            FromGameVersion = NextPatchGameVersion, ToGameVersion = CurrentNativeGameVersion,
            AdoptedAtGameTick = MinimumTick, DurableThroughSequence = 2, RecordedAtUtc = "2026-10-17T00:00:00Z",
        }).ToArray();
        using var complete = VersionTransitionScenario.CreateActive(CurrentNativeGameVersion, GameVersion, history);
        Assert.True(complete.Store.TryGetActiveTicket(complete.Token, out _, out _));
        using var missingFifth = VersionTransitionScenario.CreateActive(CurrentNativeGameVersion, GameVersion, OriginalFourTransitions());
        Assert.False(missingFifth.Store.TryGetActiveTicket(missingFifth.Token, out _, out _));
    }

    private static GameplayJournalVersionTransition[] OriginalFourTransitions()
    {
        var history = OriginalThreeTransitions();
        foreach (var transition in history) transition.AdoptedAtGameTick--;
        return history.Append(new GameplayJournalVersionTransition
        {
            FromGameVersion = LatestPatchGameVersion, ToGameVersion = NextPatchGameVersion,
            AdoptedAtGameTick = MinimumTick - 1, DurableThroughSequence = 2, RecordedAtUtc = "2026-10-15T00:00:00Z",
        }).ToArray();
    }

    [Fact]
    public void ExpiredTicketIsRejectedByDefaultButHealthyCompleteProvenanceCanBePrepared()
    {
        using var scenario = ExpiredScenario.Create();

        Assert.False(scenario.Store.TryGetActiveTicket(scenario.Token, out _, out _));
        Assert.True(scenario.Store.TryGetExpiredPrimaryProvenance(
            scenario.Token, out var ticket, out var fingerprint, out var rejection));
        Assert.NotNull(ticket);
        Assert.False(string.IsNullOrWhiteSpace(fingerprint));
        Assert.Equal(string.Empty, rejection);
    }

    [Fact]
    public void ReauthorizationRejectsInconsistentReplicas()
    {
        using var scenario = ExpiredScenario.Create();
        scenario.MutateReplica(scenario.HandoffTicketPath, ticket => ticket.SourceSessionId = "different-session");
        scenario.ReopenStore();

        Assert.False(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out _, out _));
    }

    [Fact]
    public void ReauthorizationRejectsConsumedOrCorruptTombstone()
    {
        using (var consumed = ExpiredScenario.Create())
        {
            consumed.Store.Consume(consumed.Token);
            consumed.ReopenStore();
            Assert.False(consumed.Store.TryGetExpiredPrimaryProvenance(consumed.Token, out _, out _, out _));
        }

        using var corrupt = ExpiredScenario.Create();
        File.WriteAllText(corrupt.RuntimeTombstonePath, "{not-json", Encoding.UTF8);
        corrupt.ReopenStore();
        Assert.False(corrupt.Store.TryGetExpiredPrimaryProvenance(corrupt.Token, out _, out _, out _));
    }

    [Theory]
    [InlineData("missing-journal")]
    [InlineData("missing-checkpoint")]
    [InlineData("extra-sequence")]
    public void ReauthorizationRejectsIncompleteOrChangedJournalContinuity(string change)
    {
        using var scenario = ExpiredScenario.Create();
        switch (change)
        {
            case "missing-journal": File.Delete(scenario.JournalPath); break;
            case "missing-checkpoint": scenario.MutateBothReplicas(ticket => ticket.GameplayJournalCheckpoint = null); break;
            case "extra-sequence": scenario.WriteJournal(1, 2, 3); break;
        }
        scenario.ReopenStore();

        Assert.False(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out _, out _));
    }

    [Fact]
    public void ChangedJournalContentInvalidatesThePreviouslyPreparedFingerprint()
    {
        using var scenario = ExpiredScenario.Create();
        Assert.True(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out var firstFingerprint, out _));

        scenario.WriteJournal(1, 2, createdAt: "changed-with-same-continuity");
        Assert.True(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out var changedFingerprint, out _));
        Assert.NotEqual(firstFingerprint, changedFingerprint);
        Assert.False(scenario.Store.TryConsumeReauthorization(
            scenario.Token, firstFingerprint, Guid.NewGuid().ToString(), ConfirmationDigest));
    }

    [Fact]
    public void ReauthorizationWritesAttemptAndTombstoneAndCannotReviveAfterReopen()
    {
        using var scenario = ExpiredScenario.Create();
        Assert.True(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out var fingerprint, out _));

        Assert.True(scenario.Store.TryConsumeReauthorization(
            scenario.Token, fingerprint, Guid.NewGuid().ToString(), ConfirmationDigest));
        Assert.True(File.Exists(scenario.RuntimeAttemptPath));
        Assert.True(File.Exists(scenario.HandoffAttemptPath));
        Assert.True(File.Exists(scenario.RuntimeTombstonePath));

        scenario.ReopenStore();
        Assert.False(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out _, out _));
        Assert.False(scenario.Store.TryGetActiveTicket(scenario.Token, out _, out _));
    }

    [Fact]
    public void ReauthorizationDoesNotPassWhenProtectedAttemptPersistenceFails()
    {
        using var scenario = ExpiredScenario.Create();
        Assert.True(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out var fingerprint, out _));
        scenario.EnableAttemptWriteFailure();

        Assert.False(scenario.Store.TryConsumeReauthorization(
            scenario.Token, fingerprint, Guid.NewGuid().ToString(), ConfirmationDigest));
        Assert.False(File.Exists(scenario.RuntimeTombstonePath));
        Assert.True(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out _, out _));
    }

    [Fact]
    public void SingleReplicaAttemptFailureLeavesAnAttemptFenceAfterReopen()
    {
        using var scenario = ExpiredScenario.Create();
        Assert.True(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out var fingerprint, out _));
        scenario.EnableAttemptWriteFailureOnHandoffOnly();

        Assert.False(scenario.Store.TryConsumeReauthorization(
            scenario.Token, fingerprint, Guid.NewGuid().ToString(), ConfirmationDigest));
        Assert.True(File.Exists(scenario.RuntimeAttemptPath));
        scenario.ReopenStore();
        Assert.False(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out _, out _));
    }

    [Fact]
    public void TombstonePersistenceFailureDoesNotPassAndAttemptFenceSurvivesReopen()
    {
        using var scenario = ExpiredScenario.Create();
        Assert.True(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out var fingerprint, out _));
        scenario.EnableTombstoneWriteFailure();

        Assert.False(scenario.Store.TryConsumeReauthorization(
            scenario.Token, fingerprint, Guid.NewGuid().ToString(), ConfirmationDigest));
        Assert.True(File.Exists(scenario.RuntimeAttemptPath));
        Assert.False(File.Exists(scenario.RuntimeTombstonePath));
        scenario.ReopenStore();
        Assert.False(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out _, out _));
    }

    [Fact]
    public void ReauthorizationJournalLeaseBlocksWritersUntilReleased()
    {
        using var scenario = ExpiredScenario.Create();
        Assert.True(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out var ticket, out var fingerprint, out _));

        using (scenario.Store.OpenReauthorizationJournalLease(ticket!, fingerprint))
        {
            Assert.Throws<IOException>(() =>
            {
                using var writer = new FileStream(scenario.JournalPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                writer.WriteByte(0x20);
            });
        }

        using (var writer = new FileStream(scenario.JournalPath, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            writer.WriteByte(0x20);
        }
    }

    [Theory]
    [InlineData(TargetGameVersion)]
    [InlineData(TargetPatchGameVersion)]
    public void OldExpiredTicketAndJournalCanOnlyPrepareReauthorizationOnEachResearchedTargetRuntime(
        string runtimeGameVersion)
    {
        using var scenario = VersionTransitionScenario.CreateExpired(
            GameVersion, runtimeGameVersion, GameVersion, Array.Empty<GameplayJournalVersionTransition>());

        Assert.False(scenario.Store.TryGetActiveTicket(scenario.Token, out _, out _));
        Assert.True(scenario.Store.TryGetExpiredPrimaryProvenance(
            scenario.Token, out var ticket, out _, out var rejection));
        Assert.NotNull(ticket);
        Assert.Equal(GameVersion, ticket!.GameVersion);
        Assert.Equal(string.Empty, rejection);
    }

    [Fact]
    public void Expired29088PrimaryWithExistingJournalHistoryCanPreviewExact29104Migration()
    {
        var prior = new GameplayJournalVersionTransition
        {
            FromGameVersion = GameVersion,
            ToGameVersion = TargetPatchGameVersion,
            AdoptedAtGameTick = MinimumTick - 2,
            DurableThroughSequence = 2,
            RecordedAtUtc = "2026-09-24T00:48:28.0000000+00:00",
        };
        using var scenario = VersionTransitionScenario.CreateExpired(
            TargetPatchGameVersion, CurrentPatchGameVersion, GameVersion, new[] { prior });

        Assert.False(scenario.Store.TryGetActiveTicket(scenario.Token, out _, out _));
        Assert.True(scenario.Store.TryGetExpiredPrimaryProvenance(
            scenario.Token, out var ticket, out _, out var rejection));
        Assert.Equal(TargetPatchGameVersion, ticket!.GameVersion);
        Assert.Equal(string.Empty, rejection);
    }

    [Theory]
    [InlineData("0.10.33.00000", "0.10.35.29057")]
    [InlineData("0.10.35.29057", "0.10.34.28529")]
    [InlineData("0.10.35.29057", "0.10.35.29088")]
    [InlineData("0.10.35.29088", "0.10.35.29057")]
    [InlineData("0.10.34.28529", "0.10.35.29104")]
    [InlineData("0.10.35.29057", "0.10.35.29104")]
    [InlineData("0.10.35.29104", "0.10.35.29088")]
    public void UnknownOrReverseRuntimeVersionCannotPrepareExpiredProvenance(
        string ticketVersion, string runtimeVersion)
    {
        using var scenario = VersionTransitionScenario.CreateExpired(
            ticketVersion, runtimeVersion, ticketVersion, Array.Empty<GameplayJournalVersionTransition>());

        Assert.False(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out _, out _));
    }

    [Fact]
    public void ReauthorizationFingerprintIsBoundToTheTargetRuntimeVersion()
    {
        using var scenario = VersionTransitionScenario.CreateExpired(
            GameVersion, TargetGameVersion, GameVersion, Array.Empty<GameplayJournalVersionTransition>());

        Assert.True(scenario.Store.TryGetExpiredPrimaryProvenance(
            scenario.Token, out _, out var targetFingerprint, out _));
        var sourceRuntimeStore = scenario.OpenStore(GameVersion);
        Assert.True(sourceRuntimeStore.TryGetExpiredPrimaryProvenance(
            scenario.Token, out _, out var sourceFingerprint, out _));

        Assert.NotEqual(sourceFingerprint, targetFingerprint);
    }

    [Fact]
    public void TargetRuntimeConsumptionWritesSourceVersionTombstoneAndRemainsConsumedOnSourceRuntime()
    {
        using var scenario = VersionTransitionScenario.CreateExpired(
            GameVersion, TargetGameVersion, GameVersion, Array.Empty<GameplayJournalVersionTransition>());
        Assert.True(scenario.Store.TryGetExpiredPrimaryProvenance(
            scenario.Token, out _, out var fingerprint, out _));

        Assert.True(scenario.Store.TryConsumeReauthorization(
            scenario.Token, fingerprint, Guid.NewGuid().ToString(), ConfirmationDigest));
        var tombstone = Assert.IsType<OwnedWorldResumeConsumptionTombstone>(
            PluginJson.Deserialize<OwnedWorldResumeConsumptionTombstone>(
                File.ReadAllText(scenario.RuntimeTombstonePath)));
        Assert.Equal(GameVersion, tombstone.GameVersion);

        scenario.Reopen(GameVersion);
        Assert.False(scenario.Store.TryGetActiveTicket(scenario.Token, out _, out _));
    }

    [Theory]
    [InlineData(TargetGameVersion)]
    [InlineData(TargetPatchGameVersion)]
    public void RetainedSourceTicketReplicasCannotReviveAfterTargetRuntimeConsumption(
        string runtimeGameVersion)
    {
        using var scenario = VersionTransitionScenario.CreateExpired(
            GameVersion, runtimeGameVersion, GameVersion, Array.Empty<GameplayJournalVersionTransition>());
        var runtimeReplica = File.ReadAllText(scenario.RuntimeTicketPath);
        var handoffReplica = File.ReadAllText(scenario.HandoffTicketPath);
        Assert.True(scenario.Store.TryGetExpiredPrimaryProvenance(
            scenario.Token, out _, out var fingerprint, out _));
        Assert.True(scenario.Store.TryConsumeReauthorization(
            scenario.Token, fingerprint, Guid.NewGuid().ToString(), ConfirmationDigest));

        File.WriteAllText(scenario.RuntimeTicketPath, runtimeReplica, Encoding.UTF8);
        File.WriteAllText(scenario.HandoffTicketPath, handoffReplica, Encoding.UTF8);
        scenario.Reopen(GameVersion);

        Assert.False(scenario.Store.TryGetActiveTicket(scenario.Token, out _, out _));
        Assert.False(scenario.Store.TryGetExpiredPrimaryProvenance(scenario.Token, out _, out _, out _));
    }

    [Fact]
    public void LegacySourceVersionTombstoneFencesAProtectedTokenOnTheTargetRuntime()
    {
        using var scenario = VersionTransitionScenario.CreateActive(
            TargetGameVersion, TargetGameVersion, Array.Empty<GameplayJournalVersionTransition>());
        File.WriteAllText(scenario.RuntimeTombstonePath, PluginJson.Serialize(
            new OwnedWorldResumeConsumptionTombstone
            {
                Version = 1,
                ResumeTokenHash = scenario.TokenHash,
                GameVersion = GameVersion,
                ConsumedAtUtc = DateTimeOffset.UtcNow,
            }), Encoding.UTF8);
        scenario.Reopen(TargetGameVersion);

        Assert.False(scenario.Store.TryGetActiveTicket(scenario.Token, out _, out _));
    }

    [Fact]
    public void AttemptEvidenceAloneFencesDefaultResume()
    {
        using var scenario = VersionTransitionScenario.CreateActive(
            TargetGameVersion, TargetGameVersion, Array.Empty<GameplayJournalVersionTransition>());
        File.WriteAllText(scenario.RuntimeAttemptPath, "{}", Encoding.UTF8);
        scenario.Reopen(TargetGameVersion);

        Assert.False(scenario.Store.TryGetActiveTicket(scenario.Token, out _, out _));
    }

    [Theory]
    [InlineData(TargetGameVersion)]
    [InlineData(TargetPatchGameVersion)]
    public void DurableVersionTransitionPermitsFutureNormalResumeOnEitherResearchedTargetRuntime(
        string runtimeGameVersion)
    {
        var transition = new GameplayJournalVersionTransition
        {
            FromGameVersion = GameVersion,
            ToGameVersion = runtimeGameVersion,
            AdoptedAtGameTick = MinimumTick,
            DurableThroughSequence = 2,
            RecordedAtUtc = "2026-09-24T00:48:28.0000000+00:00",
        };
        using var scenario = VersionTransitionScenario.CreateActive(
            runtimeGameVersion, GameVersion, new[] { transition });

        Assert.True(scenario.Store.TryGetActiveTicket(scenario.Token, out var ticket, out var rejection));
        Assert.NotNull(ticket);
        Assert.Equal(runtimeGameVersion, ticket!.GameVersion);
        Assert.Equal(string.Empty, rejection);
    }

    [Fact]
    public void TwoExactDurableTransitionsPermitFutureNormalResumeOn29104()
    {
        var transitions = new[]
        {
            new GameplayJournalVersionTransition
            {
                FromGameVersion = GameVersion, ToGameVersion = TargetPatchGameVersion,
                AdoptedAtGameTick = MinimumTick - 2, DurableThroughSequence = 2,
                RecordedAtUtc = "2026-09-24T00:48:28.0000000+00:00",
            },
            new GameplayJournalVersionTransition
            {
                FromGameVersion = TargetPatchGameVersion, ToGameVersion = CurrentPatchGameVersion,
                AdoptedAtGameTick = MinimumTick - 1, DurableThroughSequence = 2,
                RecordedAtUtc = "2026-09-29T00:48:28.0000000+00:00",
            },
        };
        using var scenario = VersionTransitionScenario.CreateActive(
            CurrentPatchGameVersion, GameVersion, transitions);

        Assert.True(scenario.Store.TryGetActiveTicket(scenario.Token, out var ticket, out var rejection));
        Assert.Equal(CurrentPatchGameVersion, ticket!.GameVersion);
        Assert.Equal(string.Empty, rejection);
    }

    [Fact]
    public void Old29088TicketCannotReplayAfterJournalAlreadyHas29104Transition()
    {
        var transitions = new[]
        {
            new GameplayJournalVersionTransition
            {
                FromGameVersion = GameVersion, ToGameVersion = TargetPatchGameVersion,
                AdoptedAtGameTick = MinimumTick - 2, DurableThroughSequence = 2,
                RecordedAtUtc = "2026-09-24T00:48:28.0000000+00:00",
            },
            new GameplayJournalVersionTransition
            {
                FromGameVersion = TargetPatchGameVersion, ToGameVersion = CurrentPatchGameVersion,
                AdoptedAtGameTick = MinimumTick - 1, DurableThroughSequence = 2,
                RecordedAtUtc = "2026-09-29T00:48:28.0000000+00:00",
            },
        };
        using var scenario = VersionTransitionScenario.CreateExpired(
            TargetPatchGameVersion, CurrentPatchGameVersion, GameVersion, transitions);

        Assert.False(scenario.Store.TryGetExpiredPrimaryProvenance(
            scenario.Token, out _, out _, out _));
    }

    private sealed class ExpiredScenario : IDisposable
    {
        private readonly string _root;
        private readonly string _runtimeDirectory;
        private readonly string _handoffDirectory;

        private ExpiredScenario(string root)
        {
            _root = root;
            _runtimeDirectory = Path.Combine(root, "runtime");
            _handoffDirectory = Path.Combine(root, "handoff");
            Directory.CreateDirectory(_runtimeDirectory);
            Directory.CreateDirectory(_handoffDirectory);
            Store = NewStore();
        }

        public OwnedWorldResumeTicketStore Store { get; private set; }
        public string Token { get; private set; } = string.Empty;
        public string RuntimeTicketPath => Path.Combine(_runtimeDirectory, "owned-world-resume.json");
        public string HandoffTicketPath => Path.Combine(_handoffDirectory, "owned-world-resume.json");
        public string JournalPath => Path.Combine(_runtimeDirectory, "journals", $"gameplay-{JournalIdentity}.json");
        public string RuntimeTombstonePath => Path.Combine(_runtimeDirectory, $"owned-world-resume-consumed-{TokenHash}.json");
        public string RuntimeAttemptPath => Path.Combine(_runtimeDirectory, $"owned-world-reauthorization-attempt-{TokenHash}.json");
        public string HandoffAttemptPath => Path.Combine(_handoffDirectory, $"owned-world-reauthorization-attempt-{TokenHash}.json");
        private string JournalIdentity => GameplayJournalIdentity.HashOwnedSaveIdentity(OwnedSaveName);
        private string TokenHash => HashToken(Token);

        public static ExpiredScenario Create()
        {
            var scenario = new ExpiredScenario(Path.Combine(Path.GetTempPath(), "Spherewright", "owned-ticket-tests", Guid.NewGuid().ToString("N")));
            using var primary = OwnedSavePrefixReaderTests.Fixture(identity: OwnedSaveName);
            var evidence = OwnedSavePrefixReader.Read(primary, OwnedSaveName);
            Assert.Equal(MinimumTick, evidence.GameTick);
            Assert.Equal(GameVersion, evidence.GameVersion);
            var checkpoint = new OwnedWorldGameplayJournalCheckpoint
            {
                Version = OwnedWorldGameplayJournalCheckpoint.CurrentVersion,
                JournalId = scenario.JournalIdentity,
                TrackingMode = GameplayJournalTrackingModes.AttachedExistingSave,
                HistoricalCoverageComplete = true,
                TrackingStartedAtGameTick = MinimumTick,
                MinimumDurableThroughSequence = 2,
            };
            scenario.Store.ArmFromHealthySavedOwnedSession(OwnedSaveName, SessionId, PlanetId, MinimumTick, checkpoint);
            Assert.NotNull(scenario.Store.CurrentResumeToken);
            scenario.Token = scenario.Store.CurrentResumeToken!;
            scenario.WriteJournal(1, 2);
            var issued = DateTimeOffset.UtcNow.AddDays(-2);
            var expires = issued.AddDays(1);
            scenario.MutateBothReplicas(ticket =>
            {
                ticket.IssuedAtUtc = issued;
                ticket.ExpiresAtUtc = expires;
            });
            scenario.ReopenStore();
            return scenario;
        }

        public void ReopenStore() => Store = NewStore();

        public void MutateBothReplicas(Action<OwnedWorldResumeTicket> mutate)
        {
            MutateReplica(RuntimeTicketPath, mutate);
            MutateReplica(HandoffTicketPath, mutate);
        }

        public void MutateReplica(string path, Action<OwnedWorldResumeTicket> mutate)
        {
            var ticket = Assert.IsType<OwnedWorldResumeTicket>(PluginJson.Deserialize<OwnedWorldResumeTicket>(File.ReadAllText(path)));
            mutate(ticket);
            File.WriteAllText(path, PluginJson.Serialize(ticket), Encoding.UTF8);
        }

        public void WriteJournal(params long[] sequences) => WriteJournal(sequences, "synthetic-original");

        public void WriteJournal(long first, long second, string createdAt) => WriteJournal(new[] { first, second }, createdAt);

        public void EnableAttemptWriteFailure()
        {
            File.WriteAllText(Path.Combine(_runtimeDirectory, ".test-only-fail-secure-write"), "test");
            File.WriteAllText(Path.Combine(_handoffDirectory, ".test-only-fail-secure-write"), "test");
        }

        public void EnableAttemptWriteFailureOnHandoffOnly() =>
            File.WriteAllText(Path.Combine(_handoffDirectory, ".test-only-fail-secure-write"), "test");

        public void EnableTombstoneWriteFailure()
        {
            File.WriteAllText(Path.Combine(_runtimeDirectory, ".test-only-fail-second-secure-write"), "test");
            File.WriteAllText(Path.Combine(_handoffDirectory, ".test-only-fail-second-secure-write"), "test");
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private OwnedWorldResumeTicketStore NewStore() => new(
            _runtimeDirectory, _handoffDirectory, "test-bridge", GameVersion, new ManualLogSource());

        private void WriteJournal(IReadOnlyList<long> sequences, string createdAt)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(JournalPath)!);
            var document = new GameplayJournalDocument
            {
                Version = 1,
                JournalId = JournalIdentity,
                OwnedSaveIdentityHash = JournalIdentity,
                GameVersion = GameVersion,
                TrackingMode = GameplayJournalTrackingModes.AttachedExistingSave,
                HistoricalCoverageComplete = true,
                CreatedAtActualTime = createdAt,
                TrackingStartedAtGameTick = MinimumTick,
                Entries = sequences.Select(sequence => new GameplayJournalEntry { Sequence = sequence }).ToList(),
            };
            File.WriteAllText(JournalPath, PluginJson.Serialize(document), Encoding.UTF8);
        }

        private static string HashToken(string token)
        {
            using var sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        }
    }

    private sealed class VersionTransitionScenario : IDisposable
    {
        private readonly string _root;
        private readonly string _runtimeDirectory;
        private readonly string _handoffDirectory;

        private VersionTransitionScenario(string ticketGameVersion)
        {
            _root = Path.Combine(Path.GetTempPath(), "Spherewright", "owned-ticket-version-tests", Guid.NewGuid().ToString("N"));
            _runtimeDirectory = Path.Combine(_root, "runtime");
            _handoffDirectory = Path.Combine(_root, "handoff");
            Directory.CreateDirectory(_runtimeDirectory);
            Directory.CreateDirectory(_handoffDirectory);
            Store = NewStore(ticketGameVersion);
        }

        public OwnedWorldResumeTicketStore Store { get; private set; }
        public string Token { get; private set; } = string.Empty;

        public static VersionTransitionScenario CreateExpired(
            string ticketGameVersion,
            string runtimeGameVersion,
            string journalOriginVersion,
            IReadOnlyList<GameplayJournalVersionTransition> transitions)
        {
            var scenario = CreateArmed(ticketGameVersion, journalOriginVersion, transitions);
            var issued = DateTimeOffset.UtcNow.AddDays(-2);
            var expires = issued.AddDays(1);
            scenario.MutateBoth(ticket =>
            {
                ticket.IssuedAtUtc = issued;
                ticket.ExpiresAtUtc = expires;
            });
            scenario.Reopen(runtimeGameVersion);
            return scenario;
        }

        public static VersionTransitionScenario CreateActive(
            string ticketGameVersion,
            string journalOriginVersion,
            IReadOnlyList<GameplayJournalVersionTransition> transitions)
        {
            var scenario = CreateArmed(ticketGameVersion, journalOriginVersion, transitions);
            scenario.Reopen(ticketGameVersion);
            return scenario;
        }

        public OwnedWorldResumeTicketStore OpenStore(string runtimeGameVersion) => NewStore(runtimeGameVersion);

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static VersionTransitionScenario CreateArmed(
            string ticketGameVersion,
            string journalOriginVersion,
            IReadOnlyList<GameplayJournalVersionTransition> transitions)
        {
            var scenario = new VersionTransitionScenario(ticketGameVersion);
            var checkpoint = new OwnedWorldGameplayJournalCheckpoint
            {
                Version = OwnedWorldGameplayJournalCheckpoint.CurrentVersion,
                JournalId = scenario.JournalIdentity,
                TrackingMode = GameplayJournalTrackingModes.AttachedExistingSave,
                HistoricalCoverageComplete = true,
                TrackingStartedAtGameTick = MinimumTick,
                MinimumDurableThroughSequence = 2,
            };
            scenario.Store.ArmFromHealthySavedOwnedSession(
                OwnedSaveName, SessionId, PlanetId, MinimumTick, checkpoint);
            scenario.Token = Assert.IsType<string>(scenario.Store.CurrentResumeToken);
            scenario.WriteJournal(journalOriginVersion, transitions);
            return scenario;
        }

        private string JournalIdentity => GameplayJournalIdentity.HashOwnedSaveIdentity(OwnedSaveName);
        public string RuntimeTicketPath => Path.Combine(_runtimeDirectory, "owned-world-resume.json");
        public string HandoffTicketPath => Path.Combine(_handoffDirectory, "owned-world-resume.json");
        public string RuntimeTombstonePath => Path.Combine(
            _runtimeDirectory, $"owned-world-resume-consumed-{TokenHash}.json");
        public string RuntimeAttemptPath => Path.Combine(
            _runtimeDirectory, $"owned-world-reauthorization-attempt-{TokenHash}.json");
        public string TokenHash
        {
            get
            {
                using var sha256 = SHA256.Create();
                return Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(Token))).ToLowerInvariant();
            }
        }

        public void Reopen(string runtimeGameVersion) => Store = NewStore(runtimeGameVersion);

        private OwnedWorldResumeTicketStore NewStore(string gameVersion) => new(
            _runtimeDirectory, _handoffDirectory, "version-transition-test-bridge", gameVersion, new ManualLogSource());

        private void MutateBoth(Action<OwnedWorldResumeTicket> mutate)
        {
            Mutate(RuntimeTicketPath, mutate);
            Mutate(HandoffTicketPath, mutate);
        }

        private static void Mutate(string path, Action<OwnedWorldResumeTicket> mutate)
        {
            var ticket = Assert.IsType<OwnedWorldResumeTicket>(PluginJson.Deserialize<OwnedWorldResumeTicket>(File.ReadAllText(path)));
            mutate(ticket);
            File.WriteAllText(path, PluginJson.Serialize(ticket), Encoding.UTF8);
        }

        private void WriteJournal(string originGameVersion, IReadOnlyList<GameplayJournalVersionTransition> transitions)
        {
            var directory = Path.Combine(_runtimeDirectory, "journals");
            Directory.CreateDirectory(directory);
            var document = new GameplayJournalDocument
            {
                Version = 1,
                JournalId = JournalIdentity,
                OwnedSaveIdentityHash = JournalIdentity,
                GameVersion = originGameVersion,
                VersionTransitions = transitions.ToList(),
                TrackingMode = GameplayJournalTrackingModes.AttachedExistingSave,
                HistoricalCoverageComplete = true,
                CreatedAtActualTime = "synthetic-version-transition",
                TrackingStartedAtGameTick = MinimumTick,
                Entries = new List<GameplayJournalEntry>
                {
                    new() { Sequence = 1 },
                    new() { Sequence = 2 },
                },
            };
            File.WriteAllText(Path.Combine(directory, $"gameplay-{JournalIdentity}.json"), PluginJson.Serialize(document), Encoding.UTF8);
        }
    }
}
