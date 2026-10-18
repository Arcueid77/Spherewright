using BepInEx.Logging;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

// Links the actual Voyager/same-star start, ensure, bound-launch and store code.
// Native saves/launch/adoption are substitutes: this does not prove live flight,
// a real failed-flight recovery, Unity callbacks or Windows ACLs.
[Collection("Owned resume native substitutes")]
public sealed class FlightStartCoordinatorIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshVoyagerLegSavesExactlyOnceAndBindsJourneyActionAndNativeEntry(bool returning)
    {
        using var f = new Fixture(returning);
        f.Coordinator.BeforeNativeFlightEntry = () =>
        {
            var ticket = f.Store.CurrentTicket!;
            Assert.Equal(f.Action.FlightCheckpointId, ticket.CheckpointId);
            Assert.Equal(f.Action.ActionId, ticket.LastAttemptActionId);
            Assert.Equal(ticket.CheckpointId, f.Store.VoyagerJourney!.ActiveCheckpointId);
            Assert.Equal(f.Action.ActionId, f.Store.VoyagerJourney!.ActiveActionId);
            Assert.True(ticket.NativeLaunchArmed);
            var durable = f.ReadDurableTicket();
            Assert.Equal(ticket.CheckpointId, durable.CheckpointId);
            Assert.Equal(f.Action.ActionId, durable.LastAttemptActionId);
            Assert.True(durable.NativeLaunchArmed);
            Assert.Equal(ticket.SourceRevision, f.Sessions.Snapshot.Revision);
            Assert.False(f.Sessions.FlightStartLoadedFromCheckpoint);
            Assert.False(f.Sessions.CanReuseFlightCheckpointForCurrentSession(ticket));
        };

        f.Coordinator.StartVoyagerForTest(f.Action);

        Assert.Equal(1, GameSave.FlightStartSaveCalls);
        Assert.Equal(1, f.Coordinator.NativeFlightEntries);
        Assert.Equal(NormalActionStates.WaitingForGame, f.Action.State);
        Assert.False(f.Action.Terminal);
        Assert.Equal(f.Action.FlightCheckpointId, f.Store.VoyagerJourney!.ActiveCheckpointId);
        Assert.Single(Directory.GetFiles(f.Root, "Spherewright_PreFlight_*.dsv"),
            path => !f.SeedCheckpointPaths.Contains(path));
        if (returning)
        {
            Assert.Equal(f.OriginalDeadline, f.Store.VoyagerJourney.DeadlineUtc);
            Assert.Equal(f.OriginalReturnFloor, f.Store.VoyagerJourney.ReturnEnergyFloorJoules);
            Assert.Equal(110, f.Store.VoyagerJourney.SpentGameTicks);
        }
    }

    [Fact]
    public void FreshSameStarStartStillEnsuresOneCheckpoint()
    {
        using var f = new Fixture(sameStar: true);
        f.Coordinator.StartSameStarForTest(f.Action);
        Assert.Equal(1, GameSave.FlightStartSaveCalls);
        Assert.Equal(1, f.Coordinator.NativeFlightEntries);
        Assert.Null(f.Store.VoyagerJourney);
        Assert.Equal(f.Action.FlightCheckpointId, f.Store.CurrentTicket!.CheckpointId);
        Assert.Equal(f.Action.ActionId, f.Store.CurrentTicket.LastAttemptActionId);
    }

    [Fact]
    public void RecoveredSameStarTicketKeepsItsExistingReuseRuleAndDoesNotSaveAgain()
    {
        using var f = new Fixture(sameStar: true);
        var ticket = f.CreateCompletedTicket();
        Assert.True(f.Store.TryMarkAttemptStarted(ticket.CheckpointId, "synthetic-previous-flight", 200, out _));
        Assert.True(f.Store.TryMarkRecoveryRequired(ticket.CheckpointId, "synthetic-previous-flight", 205, out _));
        Assert.True(f.Store.TryMarkReloadStarted(ticket.CheckpointId, "synthetic-reload", out _));
        Assert.True(f.Store.TryMarkReloadReconciled(ticket.CheckpointId, "synthetic-reload", out _));
        f.Sessions.Snapshot.SessionId = f.Sessions.SessionId = "synthetic-adopted-session";
        f.Sessions.Snapshot.Revision = 1;
        f.Sessions.FlightStartCheckpointId = ticket.CheckpointId;
        f.Sessions.FlightStartLoadedFromCheckpoint = true;
        f.Action.SessionId = f.Sessions.SessionId;
        GameMain.gameTick = 206;

        f.Coordinator.StartSameStarForTest(f.Action);

        Assert.Equal(0, GameSave.FlightStartSaveCalls);
        Assert.Equal(1, f.Coordinator.NativeFlightEntries);
        Assert.Equal(ticket.CheckpointId, f.Action.FlightCheckpointId);
        Assert.Equal(f.Action.ActionId, f.Store.CurrentTicket!.LastAttemptActionId);
    }

    [Theory]
    [InlineData("save_refused")]
    [InlineData("wrong_header_tick")]
    [InlineData("checkpoint_write_failed")]
    [InlineData("journey_write_failed")]
    [InlineData("attempt_write_failed")]
    [InlineData("ownership_lost")]
    public void CheckpointFailureNeverEntersNativeFlight(string failure)
    {
        using var f = new Fixture();
        if (failure == "save_refused") GameSave.FlightStartSave = _ => false;
        if (failure == "wrong_header_tick") GameSave.FlightStartSave = name => { f.WriteSave(name, 199); return true; };
        if (failure == "checkpoint_write_failed")
            File.WriteAllText(Path.Combine(f.Handoff, ".test-only-fail-secure-write"), "synthetic failure");
        if (failure == "journey_write_failed")
            f.Sessions.AfterFlightStartCheckpointMarked = _ => File.WriteAllText(
                Path.Combine(f.Handoff, ".test-only-fail-secure-write"), "synthetic journey failure");
        if (failure == "attempt_write_failed")
            f.Sessions.AfterFlightStartCheckpointMarked = _ => File.WriteAllText(
                Path.Combine(f.Handoff, ".test-only-fail-second-secure-write"), "synthetic attempt failure");
        if (failure == "ownership_lost") f.Sessions.Snapshot.OwnedBySpherewright = false;

        f.Coordinator.StartVoyagerForTest(f.Action);

        Assert.Equal(0, f.Coordinator.NativeFlightEntries);
        Assert.False(f.Action.VoyagerNativeLaunchStarted);
        Assert.True(f.Action.Terminal);
    }

    [Theory]
    [InlineData("checkpoint_body")]
    [InlineData("newer_primary")]
    [InlineData("wrong_session")]
    [InlineData("revision_advanced_twice")]
    public void BoundLaunchFreshlyRejectsChangedProofWithoutCreatingAnotherCheckpoint(string failure)
    {
        using var f = new Fixture();
        f.Sessions.AfterFlightStartCheckpointMarked = ticket =>
        {
            switch (failure)
            {
                case "checkpoint_body":
                    var path = GameSave.SavePath(ticket.CheckpointSaveName);
                    var timestamp = File.GetLastWriteTimeUtc(path);
                    var bytes = File.ReadAllBytes(path); bytes[^1] ^= 1;
                    File.WriteAllBytes(path, bytes); File.SetLastWriteTimeUtc(path, timestamp);
                    break;
                case "newer_primary": f.WriteSave(Fixture.Identity, 201); break;
                case "wrong_session": f.Sessions.Snapshot.SessionId = "synthetic-other-session"; break;
                case "revision_advanced_twice": f.Sessions.Snapshot.Revision += 2; break;
            }
        };

        f.Coordinator.StartVoyagerForTest(f.Action);

        Assert.Equal(1, GameSave.FlightStartSaveCalls);
        Assert.Equal(0, f.Coordinator.NativeFlightEntries);
        Assert.False(f.Action.VoyagerNativeLaunchStarted);
        Assert.True(f.Action.Terminal);
    }

    [Theory]
    [InlineData("checkpoint_id")]
    [InlineData("reload_token")]
    [InlineData("saved_tick")]
    [InlineData("destination")]
    [InlineData("journey_checkpoint")]
    [InlineData("journey_action")]
    public void SharedNativeEntryRequiresExactBoundTicketAndDurableJourney(string mismatch)
    {
        using var f = new Fixture();
        f.Coordinator.StartVoyagerForTest(f.Action);
        Assert.Equal(1, f.Coordinator.NativeFlightEntries);
        switch (mismatch)
        {
            case "checkpoint_id": f.Action.FlightCheckpointId = "synthetic-other-checkpoint"; break;
            case "reload_token": f.Action.FlightCheckpointReloadToken = "synthetic-other-token"; break;
            case "saved_tick": f.Action.FlightCheckpointGameTick++; break;
            case "destination": f.Action.Plan.DestinationPlanetId++; break;
            case "journey_checkpoint":
            case "journey_action":
                var journey = f.Store.VoyagerJourney!;
                if (mismatch == "journey_checkpoint") journey.ActiveCheckpointId = "synthetic-other-checkpoint";
                else journey.ActiveActionId = "synthetic-other-action";
                Assert.True(f.Store.TryPersistVoyagerJourney(journey, out var rejection), rejection);
                break;
        }

        f.Coordinator.LaunchBoundForTest(f.Action);

        Assert.Equal(1, f.Coordinator.NativeFlightEntries);
        Assert.Equal(1, GameSave.FlightStartSaveCalls);
        Assert.True(f.Action.Terminal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartedVoyagerLegRecoversThroughOriginalCheckpointAndLinkedReloadCoordinator(bool returning)
    {
        using var f = new Fixture(returning);
        f.Coordinator.StartVoyagerForTest(f.Action);
        var ticket = f.Store.CurrentTicket!;
        Assert.Equal(f.Action.FlightCheckpointId, ticket.CheckpointId);
        Assert.True(f.Store.TryMarkRecoveryRequired(ticket.CheckpointId, f.Action.ActionId, 220, out _));
        var journey = f.Store.VoyagerJourney!;
        journey.Phase = "recovery_required";
        journey.SpentGameTicks += 20;
        journey.LastAccountedGameTick = 220;
        journey.LastAccountedFlightActionId = f.Action.ActionId;
        Assert.True(f.Store.TryPersistVoyagerJourney(journey, out var rejection), rejection);
        f.Sessions.Snapshot.GameTick = 225;
        f.Coordinator.FailedFlightCheckpointId = ticket.CheckpointId;
        var reload = new FlightCheckpointReloadCoordinator(true, 60, 30, 32, f.Sessions, f.Store, f.Coordinator);
        var prepared = reload.PrepareOnMainThread(new() { ReloadToken = ticket.ReloadToken });
        Assert.True(prepared.Success, prepared.Error?.Message);
        var committed = reload.CommitOnMainThread(new() { PlanToken = prepared.Value!.PlanToken,
            IdempotencyKey = Guid.NewGuid().ToString("D") });
        Assert.True(committed.Success, committed.Error?.Message);
        Assert.True(committed.Value!.Accepted);
        f.Sessions.SessionId = f.Sessions.Snapshot.SessionId = "synthetic-adopted-session";
        f.Sessions.Snapshot.CurrentSessionLoadedFromFlightCheckpoint = true;
        f.Sessions.Snapshot.FlightCheckpointId = ticket.CheckpointId;
        f.Sessions.Snapshot.GameTick = ticket.SavedGameTick + 1;
        f.Sessions.FinishExpectedFlightCheckpointAdoptionForTest();

        Assert.True(reload.TryGetActionResultOnMainThread(committed.Value.ActionId, out var completed));
        Assert.True(completed!.Succeeded, completed.Message);
        Assert.True(completed.Terminal);
        Assert.Equal(returning ? "remote_landed" : "recovered_closed", f.Store.VoyagerJourney!.Phase);
        Assert.Equal(journey.SpentGameTicks + 5, f.Store.VoyagerJourney.SpentGameTicks);
        Assert.Equal(ticket.CheckpointId, f.Store.VoyagerJourney.ActiveCheckpointId);
        Assert.Equal(1, GameSave.FlightStartSaveCalls);
    }

    private sealed class Fixture : IDisposable
    {
        internal const string Identity = "synthetic-flight-start-primary";
        private readonly string _previousRoot = GameSave.Root;
        private readonly Func<string, bool>? _previousSave = GameSave.FlightStartSave;
        private readonly int _previousSaveCalls = GameSave.FlightStartSaveCalls;
        private readonly Action? _previousLoad = DSPGame.BeforeLoad;
        private readonly string? _previousLoadedName = DSPGame.LoadedName;
        private readonly int _previousLoads = DSPGame.LoadCalls;
        private readonly int _previousDispatches = DSPGame.DispatchCalls;
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "spherewright-flight-start-" + Guid.NewGuid().ToString("N"));
        internal string Handoff => Path.Combine(Root, "handoff");
        internal FlightCheckpointStore Store { get; }
        internal GameSessionTracker Sessions { get; } = new();
        internal NormalGameActionCoordinator Coordinator { get; }
        internal NormalGameActionCoordinator.ActionRecord Action { get; }
        internal HashSet<string> SeedCheckpointPaths { get; } = new();
        internal DateTimeOffset? OriginalDeadline;
        internal double? OriginalReturnFloor;

        internal Fixture(bool returning = false, bool sameStar = false)
        {
            Directory.CreateDirectory(Root);
            GameSave.Root = Root;
            GameSave.FlightStartSaveCalls = 0;
            GameSave.FlightStartSave = name => { WriteSave(name, GameMain.gameTick); return true; };
            DSPGame.BeforeLoad = null;
            GameMain.gameTick = 200;
            GameMain.localPlanet = new() { id = returning ? 201 : 101 };
            GameMain.mainPlayer = new();
            Store = new("synthetic-bridge", "0.10.35.29151", new ManualLogSource(), Handoff);
            Sessions.IsCurrentSessionOwned = true;
            Sessions.SessionId = Sessions.Snapshot.SessionId = "synthetic-start-session";
            Sessions.OwnedSaveName = Sessions.Snapshot.SaveName = Identity;
            Sessions.Snapshot.Revision = 7;
            Sessions.Snapshot.OwnedBySpherewright = Sessions.Snapshot.GameLoaded = Sessions.Snapshot.WritesAllowed = true;
            Sessions.Snapshot.LocalPlanetId = GameMain.localPlanet.id;
            Sessions.Snapshot.GameVersion = "0.10.35.29151";
            Sessions.Snapshot.GameTick = 200;
            WriteSave(Identity, 80);
            var journeyId = sameStar ? null : Guid.NewGuid().ToString("D");
            if (returning) SeedRemoteJourney(journeyId!);
            Action = new() { SessionId = Sessions.SessionId, PlanetId = GameMain.localPlanet.id,
                Plan = new() { PlanetId = GameMain.localPlanet.id, DestinationPlanetId = returning ? 101 : 201,
                    VoyagerJourneyId = journeyId, VoyagerReturnLeg = returning,
                    VoyagerRequest = sameStar ? null : new() { ExtraWarperReserve = 1 },
                    VoyagerBudget = sameStar ? null : new() { OriginStarId = returning ? 2 : 1,
                        DestinationStarId = returning ? 1 : 2, MaximumFlightGameTicks = 108000,
                        OutboundMaximumGameTicks = 54000, ReturnReserveGameTicks = 54000,
                        ReturnEnergyBudgetJoules = 10_000_000d, WithdrawalReserveJoules = 10_000_000d,
                        ReturnCoreEnergyMinimumJoules = 8_000_000d } } };
            Coordinator = new(Store, Sessions);
        }

        internal FlightCheckpointTicket CreateCompletedTicket()
        {
            var t = Store.CreateDraft(Identity, Sessions.Snapshot.SessionId!, Sessions.Snapshot.Revision,
                Action.PlanetId, Action.Plan.DestinationPlanetId, "synthetic-player", "synthetic-catalogue");
            WriteSave(t.CheckpointSaveName, 200);
            Store.PersistCompletedCheckpoint(t, 200);
            return t;
        }

        private void SeedRemoteJourney(string journeyId)
        {
            var ticket = Store.CreateDraft(Identity, "synthetic-outbound-session", 1, 101, 201,
                "synthetic-player", "synthetic-catalogue", 1, 2, journeyId);
            WriteSave(ticket.CheckpointSaveName, 90);
            Store.PersistCompletedCheckpoint(ticket, 90);
            var issued = DateTimeOffset.UtcNow;
            var journey = new VoyagerJourneyRecord { JourneyId = journeyId, OwnedSaveName = Identity,
                GameVersion = "0.10.35.29151", HomePlanetId = 101, HomeStarId = 1, DestinationStarId = 2,
                ActiveActionId = "synthetic-outbound-action", ActiveCheckpointId = ticket.CheckpointId,
                StartedAtGameTick = 90, MaximumGameTicks = 108000, ReturnReserveGameTicks = 54000,
                ReturnEnergyFloorJoules = 20_000_000d, WithdrawalReserveJoules = 10_000_000d,
                ReturnCoreEnergyMinimumJoules = 8_000_000d, ExtraWarperReserve = 1,
                IssuedAtUtc = issued, DeadlineUtc = issued.AddMinutes(30) };
            Assert.True(Store.TryPersistVoyagerJourney(journey, out var rejection), rejection);
            Assert.True(Store.TryMarkAttemptStarted(ticket.CheckpointId, journey.ActiveActionId, 90, out _));
            Assert.True(Store.TryMarkFlightSucceeded(ticket.CheckpointId, journey.ActiveActionId, 100, out _));
            WriteSave(Identity, 100);
            Assert.True(Store.TryRetireAfterPrimarySave(Identity, 100, out _, out _));
            journey.Phase = "remote_landed"; journey.RemotePlanetId = 201;
            journey.LastLandedGameTick = 100; journey.SpentGameTicks = 10;
            Assert.True(Store.TryPersistVoyagerJourney(journey, out rejection), rejection);
            OriginalDeadline = journey.DeadlineUtc; OriginalReturnFloor = journey.ReturnEnergyFloorJoules;
            SeedCheckpointPaths.Add(GameSave.SavePath(ticket.CheckpointSaveName));
        }

        internal FlightCheckpointTicket ReadDurableTicket() => PluginJson.Deserialize<FlightCheckpointTicket>(
            File.ReadAllText(Path.Combine(Handoff, "flight-checkpoint.json")))!;

        internal void WriteSave(string name, long tick)
        {
            using var prefix = OwnedSavePrefixReaderTests.Fixture(identity: Identity, patch: 23,
                versionPatch: 35, versionBuild: 29151, gameDescVersion: 10);
            File.WriteAllBytes(GameSave.SavePath(name), prefix.ToArray());
            using var stream = new FileStream(GameSave.SavePath(name), FileMode.Open, FileAccess.Write);
            stream.Position = 36; using var writer = new BinaryWriter(stream); writer.Write(tick);
        }

        public void Dispose()
        {
            Sessions.Dispose(); GameSave.Root = _previousRoot; GameSave.FlightStartSave = _previousSave;
            GameSave.FlightStartSaveCalls = _previousSaveCalls; DSPGame.BeforeLoad = _previousLoad;
            DSPGame.LoadedName = _previousLoadedName; DSPGame.LoadCalls = _previousLoads;
            DSPGame.DispatchCalls = _previousDispatches;
            var absolute = Path.GetFullPath(Root);
            var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(absolute), expectedParent, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(absolute).StartsWith("spherewright-flight-start-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to delete a fixture outside its exact temporary parent.");
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }
    }
}
