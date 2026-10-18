using BepInEx.Logging;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

// Executes the linked production coordinator and store against synthetic save
// prefixes and native/session substitutes. This is NOT Unity, real-game recovery,
// an actual native dispatch, a normal-save acceptance or a Windows ACL proof.
[Collection("Owned resume native substitutes")]
public sealed class FlightCheckpointReloadCoordinatorIntegrationTests
{
    [Fact]
    public void OriginalActionAndKeyAndPendingAreRegisteredBeforeNativeEntry()
    {
        using var fixture = new Fixture();
        var request = fixture.CommitRequest(fixture.Prepare());
        FlightCheckpointReloadResult? replayInsideNativeEntry = null;
        DSPGame.BeforeLoad = () =>
        {
            var durable = fixture.ReadDurableTicket();
            Assert.True(durable.ReloadPending);
            Assert.False(string.IsNullOrWhiteSpace(durable.ReloadActionId));
            Assert.NotNull(fixture.Sessions.ExpectedFlightCheckpoint);
            Assert.NotNull(fixture.Sessions.SourceLease);
            Assert.True(fixture.Coordinator.TryGetActionResultOnMainThread(durable.ReloadActionId, out var original));
            Assert.Equal(request.IdempotencyKey, original!.IdempotencyKey);
            Assert.False(original.Terminal);
            var replay = fixture.Coordinator.CommitOnMainThread(request);
            Assert.True(replay.Success, replay.Error?.Message);
            replayInsideNativeEntry = replay.Value;
            Assert.True(replayInsideNativeEntry!.IdempotentReplay);
            Assert.Equal(durable.ReloadActionId, replayInsideNativeEntry.ActionId);
        };

        var accepted = fixture.CommitAccepted(request);

        Assert.Equal(NormalActionStates.WaitingForGame, accepted.State);
        Assert.NotNull(replayInsideNativeEntry);
        Assert.Equal(accepted.ActionId, replayInsideNativeEntry!.ActionId);
        Assert.Equal(1, DSPGame.DispatchCalls);
        Assert.Equal(1, DSPGame.LoadCalls);
        Assert.Equal(fixture.Ticket.CheckpointSaveName, DSPGame.LoadedName);
        Assert.Equal(1, fixture.NormalActions.ReloadStartingCalls);
    }

    [Fact]
    public void PendingReloadRejectsRekeyingSecondPreparedTokenAndFreshPrepare()
    {
        using var fixture = new Fixture();
        var first = fixture.CommitRequest(fixture.Prepare());
        var second = fixture.CommitRequest(fixture.Prepare());
        var accepted = fixture.CommitAccepted(first);

        var sameKey = fixture.Coordinator.CommitOnMainThread(first);
        Assert.True(sameKey.Success);
        Assert.True(sameKey.Value!.IdempotentReplay);
        Assert.Equal(accepted.ActionId, sameKey.Value.ActionId);
        var rekeyed = fixture.Coordinator.CommitOnMainThread(new()
        {
            PlanToken = first.PlanToken, IdempotencyKey = Guid.NewGuid().ToString("D"),
        });
        Assert.False(rekeyed.Success);
        Assert.Equal(BridgeErrorCodes.PlanNotFound, rekeyed.Error!.Code);
        var alreadyPrepared = fixture.Coordinator.CommitOnMainThread(second);
        Assert.False(alreadyPrepared.Success);
        Assert.Equal(BridgeErrorCodes.StaleState, alreadyPrepared.Error!.Code);
        Assert.False(fixture.Coordinator.PrepareOnMainThread(fixture.PrepareRequest()).Success);
        Assert.True(fixture.ReadDurableTicket().ReloadPending);
        Assert.False(fixture.Store.TryMarkAttemptStarted(fixture.Ticket.CheckpointId, "synthetic-new-flight", 130, out _));
        Assert.False(fixture.Store.TryMarkRecoveryRequired(fixture.Ticket.CheckpointId, "synthetic-original-flight", 130, out _));
        Assert.False(fixture.Store.TryMarkFlightSucceeded(fixture.Ticket.CheckpointId, "synthetic-original-flight", 130, out _));
        Assert.Equal(FlightCheckpointLifecycleStates.RecoveryRequired, fixture.Ticket.LifecycleState);
        Assert.Equal(accepted.ActionId, fixture.ReadDurableTicket().ReloadActionId);
        Assert.Equal(1, DSPGame.DispatchCalls);
        Assert.Equal(1, DSPGame.LoadCalls);
    }

    [Fact]
    public void NativeEntryThrowPreservesOriginalActionKeyLeaseExpectationAndDurablePending()
    {
        using var fixture = new Fixture();
        var request = fixture.CommitRequest(fixture.Prepare());
        DSPGame.BeforeLoad = () => throw new IOException("Synthetic failure after native entry.");

        var accepted = fixture.CommitAccepted(request);

        Assert.Equal(NormalActionStates.OutcomeUnknown, accepted.State);
        Assert.Equal(1, DSPGame.DispatchCalls);
        Assert.Equal(0, DSPGame.LoadCalls); // The substitute entered but did not finish.
        Assert.NotNull(fixture.Sessions.SourceLease);
        Assert.NotNull(fixture.Sessions.ExpectedFlightCheckpoint);
        Assert.Equal(0, fixture.Sessions.FlightExpectationCancellationCalls);
        var durable = fixture.ReadDurableTicket();
        Assert.True(durable.ReloadPending);
        Assert.Equal(accepted.ActionId, durable.ReloadActionId);
        Assert.Equal(WriteHealthStates.Quarantined, fixture.Sessions.Snapshot.WriteHealth);
        Assert.Equal(accepted.ActionId, fixture.Sessions.Snapshot.WriteQuarantineActionId);
        var original = fixture.ReadAction(accepted.ActionId);
        Assert.Equal(accepted.ActionId, original.ActionId);
        Assert.Equal(request.IdempotencyKey, original.IdempotencyKey);
        Assert.Equal(NormalActionStates.OutcomeUnknown, original.State);
        Assert.False(original.Terminal);
        Assert.False(original.Succeeded);
        var replay = fixture.Coordinator.CommitOnMainThread(request);
        Assert.True(replay.Success);
        Assert.True(replay.Value!.IdempotentReplay);
        Assert.Equal(accepted.ActionId, replay.Value.ActionId);
        Assert.Equal(NormalActionStates.OutcomeUnknown, replay.Value.State);
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestartReadsDurablePendingAndCannotPrepareAnotherLoad(bool entryThrows)
    {
        using var fixture = new Fixture();
        var request = fixture.CommitRequest(fixture.Prepare());
        if (entryThrows) DSPGame.BeforeLoad = () => throw new IOException("Synthetic native entry failure.");
        var accepted = fixture.CommitAccepted(request);

        var reopened = fixture.ReopenStore();
        Assert.True(reopened.HasUnreconciledReload);
        var afterRestart = fixture.NewCoordinator(reopened);
        Assert.False(afterRestart.PrepareOnMainThread(fixture.PrepareRequest()).Success);
        Assert.False(afterRestart.TryGetActionResultOnMainThread(accepted.ActionId, out _));
        Assert.Equal(accepted.ActionId, fixture.ReadDurableTicket().ReloadActionId);
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    [Fact]
    public void AlreadyCheckpointLoadedSourceSessionCannotPretendThrownLoadCompleted()
    {
        using var fixture = new Fixture();
        fixture.SetCheckpointSnapshot(Fixture.SourceSessionId);
        var request = fixture.CommitRequest(fixture.Prepare());
        DSPGame.BeforeLoad = () => throw new IOException("Synthetic entry failure with old checkpoint session still alive.");
        var accepted = fixture.CommitAccepted(request);

        var original = fixture.ReadAction(accepted.ActionId);

        Assert.Equal(NormalActionStates.OutcomeUnknown, original.State);
        Assert.False(original.Terminal);
        Assert.False(original.Succeeded);
        Assert.True(fixture.ReadDurableTicket().ReloadPending);
        Assert.Equal(0, fixture.Sessions.ClearQuarantineCalls);
        Assert.NotNull(fixture.Sessions.SourceLease);
    }

    [Fact]
    public void ExactNewSessionConvergesOriginalUnknownAndCompletedSnapshotStaysStable()
    {
        using var fixture = new Fixture();
        var request = fixture.CommitRequest(fixture.Prepare());
        DSPGame.BeforeLoad = () => throw new IOException("Synthetic entry failure before eventual exact adoption.");
        var accepted = fixture.CommitAccepted(request);
        fixture.AdoptExactCheckpoint();

        var completed = fixture.ReadAction(accepted.ActionId);

        Assert.Equal(NormalActionStates.Completed, completed.State);
        Assert.True(completed.Terminal);
        Assert.True(completed.Succeeded);
        Assert.True(completed.ReconciledFromOutcomeUnknown);
        Assert.Equal(request.IdempotencyKey, completed.IdempotencyKey);
        Assert.Equal(Fixture.AdoptedSessionId, completed.SessionId);
        Assert.Equal(fixture.Ticket.OriginPlanetId, completed.PlanetId);
        Assert.Equal(FlightCheckpointLifecycleStates.Recovered, fixture.ReadDurableTicket().LifecycleState);
        Assert.False(fixture.ReadDurableTicket().ReloadPending);
        Assert.Equal(WriteHealthStates.Healthy, fixture.Sessions.Snapshot.WriteHealth);
        Assert.Null(fixture.Sessions.Snapshot.WriteQuarantineActionId);
        Assert.Equal(1, fixture.Sessions.ClearQuarantineCalls);

        fixture.Sessions.Snapshot.OwnedBySpherewright = false;
        fixture.Sessions.Snapshot.GameLoaded = false;
        fixture.Sessions.Snapshot.SessionId = "synthetic-later-unrelated-session";
        fixture.Sessions.Snapshot.FlightCheckpointId = "synthetic-other-checkpoint";
        fixture.Sessions.Snapshot.LocalPlanetId = 999;
        fixture.Sessions.Snapshot.GameTick = 9999;
        fixture.Sessions.FlightCheckpointAdoptionError = "Synthetic later error cannot rewrite accepted completion.";
        var later = fixture.ReadAction(accepted.ActionId);
        Assert.Same(completed, later);
        Assert.Equal(Fixture.AdoptedSessionId, later.SessionId);
        Assert.Equal(NormalActionStates.Completed, later.State);
        Assert.True(later.Succeeded);
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    [Theory]
    [InlineData("same-session")]
    [InlineData("no-session")]
    [InlineData("not-owned")]
    [InlineData("wrong-owned-save")]
    [InlineData("not-loaded")]
    [InlineData("wrong-checkpoint")]
    [InlineData("wrong-origin")]
    [InlineData("older-tick")]
    [InlineData("not-from-checkpoint")]
    public void AdoptionMustProveEveryExactNewSessionBinding(string fault)
    {
        using var fixture = new Fixture();
        var accepted = fixture.CommitAccepted(fixture.CommitRequest(fixture.Prepare()));
        fixture.AdoptExactCheckpoint();
        var snapshot = fixture.Sessions.Snapshot;
        switch (fault)
        {
            case "same-session": snapshot.SessionId = Fixture.SourceSessionId; break;
            case "no-session": snapshot.SessionId = null; break;
            case "not-owned": snapshot.OwnedBySpherewright = false; break;
            case "wrong-owned-save": snapshot.SaveName = "synthetic-different-owned-primary"; break;
            case "not-loaded": snapshot.GameLoaded = false; break;
            case "wrong-checkpoint": snapshot.FlightCheckpointId = "synthetic-other-checkpoint"; break;
            case "wrong-origin": snapshot.LocalPlanetId = fixture.Ticket.DestinationPlanetId; break;
            case "older-tick": snapshot.GameTick = fixture.Ticket.SavedGameTick - 1; break;
            case "not-from-checkpoint": snapshot.CurrentSessionLoadedFromFlightCheckpoint = false; break;
        }

        var original = fixture.ReadAction(accepted.ActionId);

        Assert.False(original.Terminal);
        Assert.False(original.Succeeded);
        Assert.NotEqual(NormalActionStates.Completed, original.State);
        Assert.True(fixture.ReadDurableTicket().ReloadPending);
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    [Fact]
    public void DifferentReloadMarkerCannotBeClearedByTheOriginalAction()
    {
        using var fixture = new Fixture();
        DSPGame.BeforeLoad = () => throw new IOException("Synthetic unknown load.");
        var accepted = fixture.CommitAccepted(fixture.CommitRequest(fixture.Prepare()));
        fixture.Ticket.ReloadActionId = "synthetic-different-reload-action";
        fixture.AdoptExactCheckpoint();

        var original = fixture.ReadAction(accepted.ActionId);

        Assert.Equal(NormalActionStates.OutcomeUnknown, original.State);
        Assert.False(original.Terminal);
        Assert.True(fixture.Store.HasUnreconciledReload);
        Assert.True(fixture.ReadDurableTicket().ReloadPending);
        Assert.Equal(accepted.ActionId, fixture.Sessions.Snapshot.WriteQuarantineActionId);
        Assert.Equal(0, fixture.Sessions.ClearQuarantineCalls);
        Assert.False(fixture.Store.TryMarkReloadReconciled(fixture.Ticket.CheckpointId, accepted.ActionId, out _));
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    [Fact]
    public void StaleActionCannotReconcileTheDurableOriginalMarker()
    {
        using var fixture = new Fixture();
        var accepted = fixture.CommitAccepted(fixture.CommitRequest(fixture.Prepare()));
        Assert.False(fixture.Store.TryMarkReloadReconciled(fixture.Ticket.CheckpointId, "synthetic-stale-action", out _));
        var durable = fixture.ReadDurableTicket();
        Assert.True(durable.ReloadPending);
        Assert.Equal(accepted.ActionId, durable.ReloadActionId);
    }

    [Fact]
    public void ExactAdoptionDoesNotClearAnotherActionsQuarantine()
    {
        using var fixture = new Fixture();
        DSPGame.BeforeLoad = () => throw new IOException("Synthetic unknown load.");
        var accepted = fixture.CommitAccepted(fixture.CommitRequest(fixture.Prepare()));
        fixture.Sessions.Snapshot.WriteQuarantineActionId = "synthetic-unrelated-action";
        fixture.AdoptExactCheckpoint();

        var completed = fixture.ReadAction(accepted.ActionId);

        Assert.True(completed.Succeeded);
        Assert.Equal(WriteHealthStates.Quarantined, fixture.Sessions.Snapshot.WriteHealth);
        Assert.Equal("synthetic-unrelated-action", fixture.Sessions.Snapshot.WriteQuarantineActionId);
        Assert.Equal(0, fixture.Sessions.ClearQuarantineCalls);
        Assert.False(fixture.ReadDurableTicket().ReloadPending);
    }

    [Fact]
    public void FinalStrongFileCheckRejectsSameHeaderBodyReplacementBeforeAnyNativeEntry()
    {
        using var fixture = new Fixture();
        var request = fixture.CommitRequest(fixture.Prepare());
        fixture.NormalActions.BeforeCanReload = fixture.ReplaceCheckpointBodyKeepingHeaderAndTimestamp;

        var rejected = fixture.Coordinator.CommitOnMainThread(request);

        Assert.False(rejected.Success);
        Assert.Equal(BridgeErrorCodes.ActionFailed, rejected.Error!.Code);
        Assert.Equal(0, DSPGame.DispatchCalls);
        Assert.Equal(0, DSPGame.LoadCalls);
        Assert.Equal(0, fixture.NormalActions.ReloadStartingCalls);
        Assert.False(fixture.ReadDurableTicket().ReloadPending);
        Assert.Null(fixture.Sessions.SourceLease);
        Assert.Null(fixture.Sessions.ExpectedFlightCheckpoint);
    }

    [Fact]
    public void PendingPersistenceFailureFailsOriginalActionWithoutEnteringNativeLoad()
    {
        using var fixture = new Fixture();
        var request = fixture.CommitRequest(fixture.Prepare());
        fixture.FailSecureWrites();

        var reply = fixture.Coordinator.CommitOnMainThread(request);

        Assert.True(reply.Success, reply.Error?.Message);
        Assert.False(reply.Value!.Accepted);
        Assert.Equal(NormalActionStates.ActionFailed, reply.Value.State);
        Assert.Equal(0, DSPGame.DispatchCalls);
        Assert.Equal(0, DSPGame.LoadCalls);
        Assert.Null(fixture.Sessions.SourceLease);
        Assert.Null(fixture.Sessions.ExpectedFlightCheckpoint);
        Assert.False(fixture.ReadDurableTicket().ReloadPending);
        var original = fixture.ReadAction(reply.Value.ActionId);
        Assert.Equal(NormalActionStates.ActionFailed, original.State);
        Assert.True(original.Terminal);
        Assert.False(original.Succeeded);
        var replay = fixture.Coordinator.CommitOnMainThread(request);
        Assert.True(replay.Success);
        Assert.True(replay.Value!.IdempotentReplay);
        Assert.False(replay.Value.Accepted);
        Assert.Equal(reply.Value.ActionId, replay.Value.ActionId);
        Assert.Equal(0, DSPGame.DispatchCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReconciliationPersistenceFailureKeepsOriginalUnknownUntilDurableClosure(bool entryThrows)
    {
        using var fixture = new Fixture();
        if (entryThrows) DSPGame.BeforeLoad = () => throw new IOException("Synthetic unknown load.");
        var accepted = fixture.CommitAccepted(fixture.CommitRequest(fixture.Prepare()));
        fixture.AdoptExactCheckpoint();
        fixture.FailSecureWrites();
        var pending = fixture.ReadAction(accepted.ActionId);
        Assert.Equal(NormalActionStates.OutcomeUnknown, pending.State);
        Assert.False(pending.Terminal);
        Assert.True(fixture.ReadDurableTicket().ReloadPending);
        Assert.Equal(0, fixture.Sessions.ClearQuarantineCalls);

        fixture.AllowSecureWrites();
        var completed = fixture.ReadAction(accepted.ActionId);
        Assert.True(completed.Succeeded);
        Assert.True(completed.ReconciledFromOutcomeUnknown);
        Assert.False(fixture.ReadDurableTicket().ReloadPending);
        Assert.Equal(WriteHealthStates.Healthy, fixture.Sessions.Snapshot.WriteHealth);
        Assert.Null(fixture.Sessions.Snapshot.WriteQuarantineActionId);
        Assert.Equal(1, fixture.Sessions.ClearQuarantineCalls);
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    [Fact]
    public void RecoveredCheckpointCannotReloadAgainWithoutANewFailedFlight()
    {
        using var fixture = new Fixture();
        var accepted = fixture.CommitAccepted(fixture.CommitRequest(fixture.Prepare()));
        fixture.AdoptExactCheckpoint();
        Assert.True(fixture.ReadAction(accepted.ActionId).Succeeded);
        Assert.Equal(FlightCheckpointLifecycleStates.Recovered, fixture.ReadDurableTicket().LifecycleState);
        Assert.False(fixture.Coordinator.PrepareOnMainThread(fixture.PrepareRequest()).Success);
        Assert.False(fixture.NewCoordinator(fixture.ReopenStore()).PrepareOnMainThread(fixture.PrepareRequest()).Success);
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdoptionErrorNeverRemovesTheDurablePendingBarrierOrPermitsRekeying(bool entryThrows)
    {
        using var fixture = new Fixture();
        if (entryThrows) DSPGame.BeforeLoad = () => throw new IOException("Synthetic unknown native load.");
        var accepted = fixture.CommitAccepted(fixture.CommitRequest(fixture.Prepare()));
        fixture.Sessions.FlightCheckpointAdoptionError = "Synthetic protected adoption wait expired; no verified native rejection.";
        var pending = fixture.ReadAction(accepted.ActionId);
        Assert.Equal(NormalActionStates.OutcomeUnknown, pending.State);
        Assert.False(pending.Terminal);
        Assert.False(pending.Succeeded);
        Assert.Equal(WriteHealthStates.Quarantined, fixture.Sessions.Snapshot.WriteHealth);
        Assert.Equal(accepted.ActionId, fixture.Sessions.Snapshot.WriteQuarantineActionId);
        Assert.True(fixture.ReadDurableTicket().ReloadPending);
        Assert.False(fixture.Coordinator.PrepareOnMainThread(fixture.PrepareRequest()).Success);
        Assert.False(fixture.NewCoordinator(fixture.ReopenStore()).PrepareOnMainThread(fixture.PrepareRequest()).Success);
        Assert.Equal(1, DSPGame.DispatchCalls);
        fixture.AdoptExactCheckpoint();
        var completed = fixture.ReadAction(accepted.ActionId);
        Assert.True(completed.Succeeded);
        Assert.True(completed.Terminal);
        Assert.True(completed.ReconciledFromOutcomeUnknown);
        Assert.False(fixture.ReadDurableTicket().ReloadPending);
        Assert.Equal(WriteHealthStates.Healthy, fixture.Sessions.Snapshot.WriteHealth);
        Assert.Equal(1, fixture.Sessions.ClearQuarantineCalls);
        Assert.Equal(1, DSPGame.DispatchCalls);
    }

    private sealed class Fixture : IDisposable
    {
        internal const string SourceSessionId = "synthetic-flight-source-session";
        internal const string AdoptedSessionId = "synthetic-new-checkpoint-session";
        private const string Identity = "synthetic-flight-owned-primary";
        private const string Version = "0.10.35.29151";
        private readonly string _root = Path.Combine(Path.GetTempPath(), "spherewright-checkpoint-reload-" + Guid.NewGuid().ToString("N"));
        private readonly string _previousRoot = GameSave.Root;
        private readonly BridgeError? _previousReadiness = TestWorldCoordinator.ReadinessError;
        private readonly int _previousLoads = DSPGame.LoadCalls;
        private readonly int _previousDispatches = DSPGame.DispatchCalls;
        private readonly string? _previousLoadedName = DSPGame.LoadedName;
        private readonly Action? _previousBeforeLoad = DSPGame.BeforeLoad;
        internal GameSessionTracker Sessions { get; } = new();
        internal NormalGameActionCoordinator NormalActions { get; } = new();
        internal FlightCheckpointStore Store { get; }
        internal FlightCheckpointTicket Ticket { get; }
        internal FlightCheckpointReloadCoordinator Coordinator { get; }
        private string Handoff => Path.Combine(_root, "handoff");
        private string TicketPath => Path.Combine(Handoff, "flight-checkpoint.json");

        internal Fixture()
        {
            Directory.CreateDirectory(_root);
            GameSave.Root = _root;
            TestWorldCoordinator.ReadinessError = null;
            DSPGame.LoadCalls = 0;
            DSPGame.DispatchCalls = 0;
            DSPGame.LoadedName = null;
            DSPGame.BeforeLoad = null;
            try
            {
                Store = ReopenStore();
                Ticket = Store.CreateDraft(Identity, SourceSessionId, 1, 101, 201, "synthetic-player", "synthetic-star");
                WriteSave(Ticket.OwnedSaveName, 90);
                WriteSave(Ticket.CheckpointSaveName, 100);
                Store.PersistCompletedCheckpoint(Ticket, 100);
                Assert.True(Store.TryMarkAttemptStarted(Ticket.CheckpointId, "synthetic-original-flight", 101, out _));
                Assert.True(Store.TryMarkRecoveryRequired(Ticket.CheckpointId, "synthetic-original-flight", 120, out _));
                Sessions.IsCurrentSessionOwned = true;
                Sessions.OwnedSaveName = Identity;
                Sessions.SessionId = SourceSessionId;
                Sessions.Snapshot.GameLoaded = true;
                Sessions.Snapshot.OwnedBySpherewright = true;
                Sessions.Snapshot.SessionId = SourceSessionId;
                Sessions.Snapshot.SaveName = Identity;
                Sessions.Snapshot.LocalPlanetId = Ticket.OriginPlanetId;
                Sessions.Snapshot.GameTick = 120;
                Sessions.Snapshot.GameVersion = Version;
                Sessions.Snapshot.WritesAllowed = true;
                NormalActions.FailedFlightCheckpointId = Ticket.CheckpointId;
                Coordinator = NewCoordinator(Store);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal FlightCheckpointStore ReopenStore() => new("synthetic-bridge", Version, new ManualLogSource(), Handoff);
        internal FlightCheckpointReloadCoordinator NewCoordinator(FlightCheckpointStore store) =>
            new(true, 60, 30, 32, Sessions, store, NormalActions);
        internal PrepareFlightCheckpointReloadRequest PrepareRequest() => new() { ReloadToken = Ticket.ReloadToken };
        internal PreparedFlightCheckpointReloadPlan Prepare()
        {
            var prepared = Coordinator.PrepareOnMainThread(PrepareRequest());
            Assert.True(prepared.Success, prepared.Error?.Message);
            Assert.True(prepared.Value!.Prepared);
            Assert.True(prepared.Value.CommitAllowedNow);
            return prepared.Value;
        }
        internal CommitFlightCheckpointReloadRequest CommitRequest(PreparedFlightCheckpointReloadPlan plan) => new()
        {
            PlanToken = plan.PlanToken, IdempotencyKey = Guid.NewGuid().ToString("D"),
        };
        internal FlightCheckpointReloadResult CommitAccepted(CommitFlightCheckpointReloadRequest request)
        {
            var committed = Coordinator.CommitOnMainThread(request);
            Assert.True(committed.Success, committed.Error?.Message);
            Assert.True(committed.Value!.Accepted);
            return committed.Value;
        }
        internal ActionResultSnapshot ReadAction(string actionId)
        {
            Assert.True(Coordinator.TryGetActionResultOnMainThread(actionId, out var action));
            Assert.NotNull(action);
            return action!;
        }
        internal FlightCheckpointTicket ReadDurableTicket() =>
            PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(TicketPath))!;
        internal void SetCheckpointSnapshot(string sessionId)
        {
            Sessions.Snapshot.GameLoaded = true;
            Sessions.Snapshot.OwnedBySpherewright = true;
            Sessions.Snapshot.SessionId = sessionId;
            Sessions.Snapshot.SaveName = Ticket.OwnedSaveName;
            Sessions.Snapshot.LocalPlanetId = Ticket.OriginPlanetId;
            Sessions.Snapshot.GameTick = Ticket.SavedGameTick + 1;
            Sessions.Snapshot.CurrentSessionLoadedFromFlightCheckpoint = true;
            Sessions.Snapshot.FlightCheckpointId = Ticket.CheckpointId;
        }
        internal void AdoptExactCheckpoint()
        {
            SetCheckpointSnapshot(AdoptedSessionId);
            Sessions.SessionId = AdoptedSessionId;
            Sessions.FinishExpectedFlightCheckpointAdoptionForTest();
        }
        internal void ReplaceCheckpointBodyKeepingHeaderAndTimestamp()
        {
            var path = GameSave.SavePath(Ticket.CheckpointSaveName);
            var written = File.GetLastWriteTimeUtc(path);
            var bytes = File.ReadAllBytes(path);
            bytes[^1] ^= 1;
            File.WriteAllBytes(path, bytes);
            File.SetLastWriteTimeUtc(path, written);
        }
        internal void FailSecureWrites() => File.WriteAllText(Path.Combine(Handoff, ".test-only-fail-secure-write"), "synthetic failure");
        internal void AllowSecureWrites() => File.Delete(Path.Combine(Handoff, ".test-only-fail-secure-write"));
        private static void WriteSave(string name, long tick)
        {
            using var fixture = OwnedSavePrefixReaderTests.Fixture(identity: Identity, patch: 23,
                versionPatch: 35, versionBuild: 29151, gameDescVersion: 10);
            var path = GameSave.SavePath(name);
            File.WriteAllBytes(path, fixture.ToArray());
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write);
            stream.Position = 36;
            using var writer = new BinaryWriter(stream);
            writer.Write(tick);
        }
        public void Dispose()
        {
            Sessions.Dispose();
            DSPGame.BeforeLoad = null;
            GameSave.Root = _previousRoot;
            TestWorldCoordinator.ReadinessError = _previousReadiness;
            DSPGame.LoadCalls = _previousLoads;
            DSPGame.DispatchCalls = _previousDispatches;
            DSPGame.LoadedName = _previousLoadedName;
            DSPGame.BeforeLoad = _previousBeforeLoad;
            var full = Path.GetFullPath(_root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(full), temp, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(full).StartsWith("spherewright-checkpoint-reload-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to delete a test directory outside this fixture's exact temporary parent.");
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }
}
