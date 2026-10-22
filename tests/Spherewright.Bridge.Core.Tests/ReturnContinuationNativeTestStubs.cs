using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    // Unity/load shell only. Provenance, session rebind, quota, Native idle
    // policy, completion, Save/readback and ticket renewal remain production.
    internal bool AdoptReturnContinuationNativeShellForTest(FlightCheckpointStore store, OwnedWorldResumeTicketStore tickets)
    {
        var source = RemoteEvidence!.ReturnOnlySource!;
        Snapshot.SessionId = SessionId = Guid.NewGuid().ToString("D");
        Snapshot.GameLoaded = Snapshot.OwnedBySpherewright = true;
        Snapshot.SaveName = source.Journey.OwnedSaveName; Snapshot.GameVersion = source.Journey.GameVersion;
        Snapshot.LocalPlanetId = source.Journey.RemotePlanetId; Snapshot.GameTick = source.Source.Prefix.GameTick;
        Snapshot.CurrentSessionLoadedFromFlightCheckpoint = false; Snapshot.WriteHealth = WriteHealthStates.Healthy;
        BindCheckpointNativeWorldFromSnapshotForTest(store);
        _resumeTickets = tickets; _ownedSaveState = OwnedSaveStates.WaitingToSave; _lastOwnedSaveGameTick = null;
        _pendingRemoteResumeEvidence = RemoteEvidence; _pendingRemoteResumeActionId = RemoteResumeActionId;
        _remoteResumeAdoption = new() { World = GameMain.data, SessionId = SessionId!, ResumeActionId = RemoteResumeActionId! };
        return BindLoadedReturnOnlyContinuation();
    }

    internal void BindReturnContinuationResaveGuardForTest(NormalGameActionCoordinator nativeShell)
    {
        // Substitute the Unity capture callback, not its strict-idle policy.
        // Save must capture again; the prior successful decision is not reused.
        _remoteResumeAdmissionGuard = adoption =>
            ReferenceEquals(adoption.World, GameMain.data) && adoption.SessionId == SessionId
                ? nativeShell.ObserveRestoredReturnIdleForTest().BlockerCode
                : "remote_resume_resave_binding_unproved";
    }

    internal void RetainReturnContactFailureForTest(Spherewright.Contracts.Players.NativeResumeFailureSnapshot failure)
        => _remoteResumeAdoption!.RetainFailure(failure);
}

internal sealed partial class NormalGameActionCoordinator
{
    // Unity observation and idle-owner publication are shell substitutes.
    // Runtime references, cost calculation and strict idle/budget decisions
    // use the production guard and policies linked by this fixture.
    internal VoyagerRemoteIdleAdmissionDecision ObserveRestoredReturnIdleForTest()
    {
        ObserveBoundNative501ContractForTest();
        var j = _flightCheckpoints.VoyagerJourney!; var t = j.ReturnOnlyTask!;
        var owner = new VoyagerRemoteIdleOwner(new VoyagerNativeBinding(GameMain.data!, GameMain.mainPlayer),
            t.CurrentSessionId, j.JourneyId, t.CurrentActionId, t.OriginalFlightActionId);
        var decision = CaptureVoyagerRemoteIdleAdmission(owner, j, null, j.SpentGameTicks, j.LastLandedGameTick, out var scene);
        if (decision.Proved) { _voyagerRemoteIdleOwner = owner; _voyagerRemoteIdleScene = scene; }
        return decision;
    }
}
