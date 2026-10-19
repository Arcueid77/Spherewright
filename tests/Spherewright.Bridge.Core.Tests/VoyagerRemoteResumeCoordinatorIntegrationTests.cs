using System.Text;
using BepInEx.Logging;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Journals;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

// Production coordinator/store, synthetic prefixes and native/session substitutes.
// This does not run Unity, adopt a real world or certify Windows ACLs.
[Collection("Owned resume native substitutes")]
public sealed class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Fact]
    public void FreshDisclosureRequiresNewExactConfirmationAndPrepareHoldsNoFileLocks()
    {
        using var s = new Scenario();
        var p = s.Prepare();
        Assert.True(p.UserConfirmationRequired); Assert.True(p.ExactEmbeddedIdentityVerified);
        Assert.Equal(VoyagerRemoteResumePolicy.EvidenceVersion, p.RecoveryEvidenceVersion);
        Assert.Equal(12510, p.CandidateGameTick); Assert.False(p.CommitAllowedNow);
        Assert.NotEmpty(p.ConfirmationDigest);
        foreach (var path in s.ProtectedEvidencePaths)
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        var refused = s.Commit(p, confirmed: false);
        Assert.False(refused.Success); Assert.Equal(BridgeErrorCodes.UserConfirmationRequired, refused.Error!.Code);
        Assert.Equal(0, DSPGame.LoadCalls); Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
    }

    [Fact]
    public void ExactFreshCommitConsumesBeforeDispatchAndTransfersOnlyFreshLiveLeases()
    {
        using var s = new Scenario(); var p = s.Prepare();
        DSPGame.BeforeLoad = () => Assert.False(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
        var result = s.Commit(p);
        Assert.True(result.Success); Assert.True(result.Value!.Accepted); Assert.Equal(1, DSPGame.LoadCalls);
        Assert.Equal(GameSave.LastExit, DSPGame.LoadedName);
        Assert.Equal(result.Value.ActionId, s.Sessions.RemoteResumeActionId);
        Assert.Equal(OwnedWorldResumeSourceKind.LastExit, s.Sessions.ResumeSource);
        Assert.NotNull(s.Sessions.RemoteEvidence); Assert.False(s.Sessions.RemoteEvidence!.IsDisposed);
        foreach (var path in s.ProtectedEvidencePaths)
            Assert.ThrowsAny<IOException>(() => { using var write = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
        s.Sessions.Dispose();
        foreach (var path in s.ProtectedEvidencePaths)
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        Assert.False(s.Checkpoints.CurrentTicket is not null); // Success is never a checkpoint-load capability.
    }

    [Fact]
    public void DurableConsumptionFailureDispatchesNoLoadAndReleasesEveryLease()
    {
        using var s = new Scenario(); var p = s.Prepare();
        File.WriteAllText(Path.Combine(s.Runtime, ".test-only-fail-secure-write"), "test");
        File.WriteAllText(Path.Combine(s.Handoff, ".test-only-fail-secure-write"), "test");
        var result = s.Commit(p);
        Assert.False(result.Success); Assert.Equal(0, DSPGame.LoadCalls);
        Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
        foreach (var path in s.ProtectedEvidencePaths)
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Fact]
    public void AnOwnedLoadWithFailedStrictHandoffIsNotReportedAsEndlessPending()
    {
        using var s = new Scenario(); var p = s.Prepare(); var accepted = s.Commit(p);
        Assert.True(accepted.Success);
        s.Sessions.Snapshot.OwnedBySpherewright = true;
        s.Sessions.Snapshot.LocalPlanetId = 201; s.Sessions.Snapshot.GameTick = 12511;
        s.Sessions.ResumeAdoptionError = "synthetic strict idle failure; Native Pause verified";
        Assert.True(s.Result(accepted.Value!.ActionId, out var result));
        Assert.True(result!.Terminal); Assert.False(result.Succeeded);
        Assert.Equal(NormalActionStates.ActionFailed, result.State);
        Assert.Equal(s.Sessions.ResumeAdoptionError, result.Message);
        Assert.Equal(1, DSPGame.LoadCalls);
    }

    [Theory]
    [InlineData("runtime_credential")]
    [InlineData("both_credentials")]
    [InlineData("source_bytes")]
    [InlineData("newer_primary")]
    [InlineData("original_journal")]
    [InlineData("durable_journey")]
    public void ChangedPreparedEvidenceCannotLoadOrLeaveLocksBehind(string fault)
    {
        using var s = new Scenario(); var p = s.Prepare();
        if (fault == "runtime_credential" || fault == "both_credentials")
        {
            s.MutateCredential(s.RuntimeTicket);
            if (fault == "both_credentials") s.MutateCredential(s.HandoffTicket);
        }
        if (fault == "source_bytes") File.AppendAllText(GameSave.SavePath(GameSave.LastExit), "changed");
        if (fault == "newer_primary") s.WriteSave(Scenario.Identity, 12511);
        if (fault == "original_journal") File.AppendAllText(s.JournalPath, " ");
        if (fault == "durable_journey")
        {
            var path = Path.Combine(s.Handoff, "voyager-journey.json");
            var journey = PluginJson.Deserialize<VoyagerJourneyRecord>(File.ReadAllText(path))!;
            journey.SpentGameTicks++; File.WriteAllText(path, PluginJson.Serialize(journey));
        }
        var result = s.Commit(p);
        Assert.False(result.Success); Assert.Equal(0, DSPGame.LoadCalls);
        foreach (var path in s.ProtectedEvidencePaths)
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Fact]
    public void EnteredNativeLoadExceptionKeepsTheOriginalAcceptedUnknownAndConsumedCredential()
    {
        using var s = new Scenario(); var p = s.Prepare();
        DSPGame.BeforeLoad = () => throw new InvalidOperationException("synthetic native refusal");
        var accepted = s.Commit(p);
        Assert.True(accepted.Success); Assert.True(accepted.Value!.Accepted);
        Assert.Equal(NormalActionStates.OutcomeUnknown, accepted.Value.State);
        Assert.Equal(0, DSPGame.LoadCalls); // Stub threw after entry, not proof of no Native dispatch.
        Assert.False(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
        Assert.NotNull(s.Sessions.RemoteEvidence); Assert.False(s.Sessions.RemoteEvidence!.IsDisposed);
        s.Sessions.Dispose();
        foreach (var path in s.ProtectedEvidencePaths)
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Fact]
    public void ObserverFailureAfterNativeDispatchRetainsTheAcceptedOriginalActionAndReplay()
    {
        using var s = new Scenario(); var p = s.Prepare();
        s.Sessions.BeforeRemoteLoaderBind = () => throw new InvalidOperationException("private synthetic observer detail");
        var request = new CommitOwnedWorldResumeRequest { PlanToken = p.PlanToken, IdempotencyKey = Guid.NewGuid().ToString(),
            UserConfirmedInConversation = true, ConfirmationDigest = p.ConfirmationDigest };
        var accepted = s.Commit(request);
        Assert.True(accepted.Success); Assert.True(accepted.Value!.Accepted);
        Assert.Equal(NormalActionStates.OutcomeUnknown, accepted.Value.State);
        Assert.True(s.Sessions.RemoteLoaderObserverPrepared); Assert.False(s.Sessions.RemoteLoaderObserverBound);
        Assert.Equal(1, DSPGame.LoadCalls);
        Assert.True(s.Result(accepted.Value.ActionId, out var result));
        Assert.False(result!.Terminal); Assert.Equal(NormalActionStates.OutcomeUnknown, result.State);
        Assert.DoesNotContain("private synthetic", result.Message);
        var replay = s.Commit(request);
        Assert.True(replay.Success); Assert.True(replay.Value!.IdempotentReplay);
        Assert.Equal(accepted.Value.ActionId, replay.Value.ActionId); Assert.Equal(1, DSPGame.LoadCalls);
        Assert.False(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
    }

    private sealed class Scenario : IDisposable
    {
        internal const string Identity = "synthetic-remote-resume-world";
        private const string Version = "0.10.34.28529";
        private readonly string _root = Path.Combine(Path.GetTempPath(), "spherewright-remote-resume-" + Guid.NewGuid().ToString("N"));
        private readonly string _oldRoot = GameSave.Root;
        private readonly Action? _oldBeforeLoad = DSPGame.BeforeLoad;
        private readonly int _oldLoadCalls = DSPGame.LoadCalls;
        private readonly string? _oldLoadedName = DSPGame.LoadedName;
        internal string Runtime => Path.Combine(_root, "runtime");
        internal string Handoff => Path.Combine(_root, "handoff");
        internal string RuntimeTicket => Path.Combine(Runtime, "owned-world-resume.json");
        internal string HandoffTicket => Path.Combine(Handoff, "owned-world-resume.json");
        internal string JournalPath => Path.Combine(Runtime, "journals", "gameplay-" + GameplayJournalIdentity.HashOwnedSaveIdentity(Identity) + ".json");
        internal OwnedWorldResumeTicketStore Tickets { get; }
        internal FlightCheckpointStore Checkpoints { get; }
        internal GameSessionTracker Sessions { get; }
        private OwnedWorldResumeCoordinator Coordinator { get; }
        internal string Token { get; }
        private string CheckpointSaveName { get; }
        internal IEnumerable<string> ProtectedEvidencePaths => new[] { GameSave.SavePath(GameSave.LastExit), GameSave.SavePath(Identity),
            GameSave.SavePath(CheckpointSaveName), JournalPath, Path.Combine(Handoff, "flight-checkpoint.json"), Path.Combine(Handoff, "voyager-journey.json") };

        internal Scenario()
        {
            Directory.CreateDirectory(_root); Directory.CreateDirectory(Runtime); Directory.CreateDirectory(Handoff);
            GameSave.Root = _root; DSPGame.BeforeLoad = null; DSPGame.LoadCalls = 0; DSPGame.LoadedName = null;
            TestWorldCoordinator.ReadinessError = null;
            WriteSave(Identity, 12345);
            var actionId = Guid.NewGuid().ToString(); var journeyId = Guid.NewGuid().ToString();
            Checkpoints = new FlightCheckpointStore("test-bridge", Version, new ManualLogSource(), Handoff);
            var cp = Checkpoints.CreateDraft(Identity, "synthetic-session", 1, 104, 201, "player", "stars", 1, 2, journeyId);
            CheckpointSaveName = cp.CheckpointSaveName; WriteSave(cp.CheckpointSaveName, 12345);
            Checkpoints.PersistCompletedCheckpoint(cp, 12345);
            Assert.True(Checkpoints.TryMarkAttemptStarted(cp.CheckpointId, actionId, 12345, out _));
            var journey = new VoyagerJourneyRecord
            {
                JourneyId = journeyId, OwnedSaveName = Identity, GameVersion = Version, HomePlanetId = 104, HomeStarId = 1,
                DestinationStarId = 2, ActiveActionId = actionId, ActiveCheckpointId = cp.CheckpointId,
                StartedAtGameTick = 12345, MaximumGameTicks = 108000, ReturnReserveGameTicks = 54000,
                ReturnEnergyFloorJoules = 5_000_000_000, WithdrawalReserveJoules = 1_600_000_000,
                ReturnCoreEnergyMinimumJoules = 1_280_000_000, IssuedAtUtc = DateTimeOffset.UtcNow,
                DeadlineUtc = DateTimeOffset.UtcNow.AddHours(1), ExtraWarperReserve = 1,
            };
            Assert.True(Checkpoints.TryPersistVoyagerJourney(journey, out _));
            Assert.True(Checkpoints.TryMarkFlightSucceeded(cp.CheckpointId, actionId, 12500, out _));
            journey.Phase = "remote_landed"; journey.RemotePlanetId = 201; journey.SpentGameTicks = 155;
            journey.LastAccountedGameTick = 12500; journey.LastAccountedFlightActionId = actionId; journey.LastLandedGameTick = 12500;
            Assert.True(Checkpoints.TryPersistVoyagerJourney(journey, out _));
            var journalId = GameplayJournalIdentity.HashOwnedSaveIdentity(Identity);
            var checkpoint = new OwnedWorldGameplayJournalCheckpoint { Version = 1, JournalId = journalId,
                TrackingMode = GameplayJournalTrackingModes.AttachedExistingSave, TrackingStartedAtGameTick = 12345,
                HistoricalCoverageComplete = true, MinimumDurableThroughSequence = 1 };
            Directory.CreateDirectory(Path.GetDirectoryName(JournalPath)!);
            File.WriteAllText(JournalPath, PluginJson.Serialize(new GameplayJournalDocument { Version = 1, JournalId = journalId,
                OwnedSaveIdentityHash = journalId, GameVersion = Version, TrackingMode = checkpoint.TrackingMode,
                TrackingStartedAtGameTick = checkpoint.TrackingStartedAtGameTick, HistoricalCoverageComplete = true,
                Entries = new List<GameplayJournalEntry> { new() { Sequence = 1 } } }));
            Tickets = new OwnedWorldResumeTicketStore(Runtime, Handoff, "test-bridge", Version, new ManualLogSource());
            Tickets.ArmFromQuarantinedOwnedSession(Identity, "synthetic-session", 201, 12500, actionId, checkpoint);
            Token = Tickets.CurrentResumeToken!; WriteSave(GameSave.LastExit, 12510);
            Sessions = new GameSessionTracker { RequiresRemoteResumeEvidence = true, RemoteResumeCheckpoints = Checkpoints };
            Coordinator = new OwnedWorldResumeCoordinator(true, 30, 30, 16, Sessions, Tickets);
        }
        internal PreparedOwnedWorldResumePlan Prepare()
        {
            var result = Coordinator.PrepareOnMainThread(new PrepareOwnedWorldResumeRequest { ResumeToken = Token });
            Assert.True(result.Success, result.Error?.Message); return result.Value!;
        }
        internal GameCallResult<OwnedWorldResumeResult> Commit(PreparedOwnedWorldResumePlan p, bool confirmed = true) =>
            Coordinator.CommitOnMainThread(new CommitOwnedWorldResumeRequest { PlanToken = p.PlanToken, IdempotencyKey = Guid.NewGuid().ToString(),
                UserConfirmedInConversation = confirmed, ConfirmationDigest = confirmed ? p.ConfirmationDigest : string.Empty });
        internal GameCallResult<OwnedWorldResumeResult> Commit(CommitOwnedWorldResumeRequest request) => Coordinator.CommitOnMainThread(request);
        internal bool Result(string actionId, out ActionResultSnapshot? result) => Coordinator.TryGetActionResultOnMainThread(actionId, out result);
        internal void WriteSave(string name, long tick)
        {
            using var data = OwnedSavePrefixReaderTests.Fixture(identity: Identity);
            data.Position = 36; using (var writer = new BinaryWriter(data, Encoding.UTF8, true)) writer.Write(tick);
            File.WriteAllBytes(GameSave.SavePath(name), data.ToArray());
        }
        internal void MutateCredential(string path)
        {
            var ticket = PluginJson.Deserialize<OwnedWorldResumeTicket>(File.ReadAllText(path))!;
            ticket.MinimumGameTick++; File.WriteAllText(path, PluginJson.Serialize(ticket));
        }
        public void Dispose()
        {
            Sessions.Dispose(); GameSave.Root = _oldRoot; DSPGame.BeforeLoad = _oldBeforeLoad;
            DSPGame.LoadCalls = _oldLoadCalls; DSPGame.LoadedName = _oldLoadedName; TestWorldCoordinator.ReadinessError = null;
            try { Directory.Delete(_root, true); } catch (IOException) { }
        }
    }
}
