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
public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Fact]
    public void ExpiredOriginalUtcNeedsANewExactDisclosureAndOneDurableNonRenewableAuthorization()
    {
        using var s = new Scenario(expiredJourney: true);
        var original = File.ReadAllBytes(Path.Combine(s.Handoff, "voyager-journey.json"));
        Assert.False(s.PrepareResult(new()).Success);
        var request = WindowRequest(); var p = s.Prepare(request);
        Assert.Equal(s.OriginalDeadline, p.OriginalJourneyDeadlineUtc);
        Assert.Equal(request.RecoveryDeadlineUtc, p.EffectiveRecoveryDeadlineUtc);
        Assert.Equal(request.RecoveryScope, p.RecoveryScope);
        Assert.Equal(request.MaximumChargeGameTicks, p.MaximumChargeGameTicks);
        Assert.Equal(request.MaximumChargeWallSeconds, p.MaximumChargeWallSeconds);
        Assert.True(p.NativeChargeFeasibilityRequiresFreshCapture);
        Assert.True(p.UserConfirmationRequired); Assert.False(p.CommitAllowedNow);
        Assert.False(s.Commit(p, confirmed: false).Success); Assert.Equal(0, DSPGame.LoadCalls);
        // A rejected commit consumes its preview token, never the credential.
        // A fresh disclosure of the same absolute proposal is still required.
        p = s.Prepare(request);
        var commit = new CommitOwnedWorldResumeRequest { PlanToken = p.PlanToken, IdempotencyKey = Guid.NewGuid().ToString(),
            UserConfirmedInConversation = true, ConfirmationDigest = p.ConfirmationDigest };
        var accepted = s.Commit(commit);
        Assert.True(accepted.Success, accepted.Error?.Message);
        var auth = s.Sessions.RecoveryAuthorization!;
        Assert.Equal(accepted.Value!.ActionId, auth.ResumeActionId);
        Assert.Equal(p.ConfirmationDigest, auth.ConfirmationDigest);
        Assert.Equal(s.Sessions.RemoteEvidence!.Fingerprint, auth.EvidenceFingerprint);
        Assert.Equal(request.RecoveryDeadlineUtc, auth.Proposal.DeadlineUtc);
        var tombstonePath = Assert.Single(Directory.GetFiles(s.Runtime, "owned-world-resume-consumed-*.json"));
        var tombstone = PluginJson.Deserialize<OwnedWorldResumeConsumptionTombstone>(File.ReadAllText(tombstonePath))!;
        Assert.Equal(auth.ResumeActionId, tombstone.RecoveryAuthorization!.ResumeActionId);
        Assert.Equal(auth.Proposal.DeadlineUtc, tombstone.RecoveryAuthorization.Proposal.DeadlineUtc);
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(s.Handoff, "voyager-journey.json")));
        var replay = s.Commit(commit);
        Assert.True(replay.Success); Assert.True(replay.Value!.IdempotentReplay);
        Assert.Equal(accepted.Value.ActionId, replay.Value.ActionId); Assert.Equal(1, DSPGame.LoadCalls);
        Assert.False(s.Commit(p).Success); // A new key cannot revive the consumed proposal.
        Assert.False(s.PrepareResult(WindowRequest()).Success);
        var coldStore = new OwnedWorldResumeTicketStore(s.Runtime, s.Handoff, "test-bridge", "0.10.34.28529", new ManualLogSource());
        Assert.False(coldStore.TryGetActiveTicket(s.Token, out _, out _));
        Assert.Equal(1, DSPGame.LoadCalls);
    }

    [Theory]
    [InlineData("deadline")] [InlineData("scope")] [InlineData("ticks")] [InlineData("wall")] [InlineData("mode")]
    public void APartialOrDifferentModeRecoveryWindowNeverDispatches(string fault)
    {
        using var s = new Scenario(expiredJourney: true); var request = WindowRequest();
        switch (fault)
        {
            case "deadline": request.RecoveryDeadlineUtc = null; break;
            case "scope": request.RecoveryScope = ""; break;
            case "ticks": request.MaximumChargeGameTicks = 0; break;
            case "wall": request.MaximumChargeWallSeconds = 0; break;
            case "mode": request.RecoveryMode = OwnedWorldResumeModes.VerifiedNewerLastExit; break;
        }
        Assert.False(s.PrepareResult(request).Success); Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void ChangedAbsoluteProposalCannotBorrowAnotherPreparedConfirmation()
    {
        using var s = new Scenario(expiredJourney: true);
        var first = s.Prepare(WindowRequest());
        var secondRequest = WindowRequest(); secondRequest.RecoveryDeadlineUtc = secondRequest.RecoveryDeadlineUtc!.Value.AddMinutes(-1);
        var second = s.Prepare(secondRequest);
        Assert.NotEqual(first.ConfirmationDigest, second.ConfirmationDigest);
        var result = s.Commit(new CommitOwnedWorldResumeRequest { PlanToken = second.PlanToken, IdempotencyKey = Guid.NewGuid().ToString(),
            UserConfirmedInConversation = true, ConfirmationDigest = first.ConfirmationDigest });
        Assert.False(result.Success); Assert.Equal(0, DSPGame.LoadCalls);
        Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
    }

    private static PrepareOwnedWorldResumeRequest WindowRequest() => new()
    {
        RecoveryDeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(30), RecoveryScope = VoyagerRecoveryWindowProposal.TaskScope,
        MaximumChargeGameTicks = 9000, MaximumChargeWallSeconds = 240,
    };

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

    private sealed partial class Scenario : IDisposable
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
        internal string Token { get; private set; }
        internal DateTimeOffset OriginalDeadline { get; private set; }
        private string CheckpointSaveName { get; }
        internal IEnumerable<string> ProtectedEvidencePaths => new[] { GameSave.SavePath(GameSave.LastExit), GameSave.SavePath(Identity),
            GameSave.SavePath(CheckpointSaveName), JournalPath, Path.Combine(Handoff, "flight-checkpoint.json"), Path.Combine(Handoff, "voyager-journey.json") };

        internal Scenario(bool expiredJourney = false)
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
                ReturnCoreEnergyMinimumJoules = 1_280_000_000,
                IssuedAtUtc = DateTimeOffset.UtcNow.AddHours(expiredJourney ? -2 : 0),
                DeadlineUtc = DateTimeOffset.UtcNow.AddHours(expiredJourney ? -1 : 1), ExtraWarperReserve = 1,
            };
            OriginalDeadline = journey.DeadlineUtc;
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
        internal GameCallResult<PreparedOwnedWorldResumePlan> PrepareResult(PrepareOwnedWorldResumeRequest request)
        {
            request.ResumeToken = Token; return Coordinator.PrepareOnMainThread(request);
        }
        internal PreparedOwnedWorldResumePlan Prepare(PrepareOwnedWorldResumeRequest? request = null)
        {
            var result = PrepareResult(request ?? new());
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
