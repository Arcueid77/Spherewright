using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    // Unity import/callback shell only. Source capture, registration, loaded
    // binding, terminal persistence and lease completion are production code.
    internal bool AdoptFailedLandingNativeShellForTest(FlightCheckpointStore store)
    {
        var source = RemoteEvidence!.FailedLandingSource!;
        Snapshot.SessionId = SessionId = Guid.NewGuid().ToString();
        Snapshot.GameLoaded = Snapshot.OwnedBySpherewright = true;
        Snapshot.SaveName = source.Journey.OwnedSaveName; Snapshot.GameVersion = source.Journey.GameVersion;
        Snapshot.LocalPlanetId = source.Journey.RemotePlanetId; Snapshot.GameTick = source.SavedAccountingAnchorGameTick;
        Snapshot.WriteHealth = WriteHealthStates.Healthy;
        BindCheckpointNativeWorldFromSnapshotForTest(store);
        _ownedSaveState = OwnedSaveStates.WaitingToSave; _lastOwnedSaveGameTick = null;
        _pendingRemoteResumeEvidence = RemoteEvidence; _pendingRemoteResumeActionId = RemoteResumeActionId;
        _remoteResumeLoadCompleteObserved = _remoteResumeJournalConfirmed = true;
        _remoteResumeCompletedWorld = GameMain.data; _pendingJournalResumeTicket = null;
        _remoteResumeAdoption = new() { World = GameMain.data, Player = GameMain.mainPlayer!,
            SessionId = SessionId!, ResumeActionId = RemoteResumeActionId!, Evidence = RemoteEvidence };
        return BindLoadedFailedLandingAdmission();
    }
    internal int FailedLandingExpectedPlanetForTest()
    {
        _pendingRemoteResumeEvidence = RemoteEvidence;
        return ExpectedResumePlanet(RemoteEvidence!.FailedLandingSource!.Ticket);
    }
    internal void PreserveFailedLandingForTest(string code)
    {
        _remoteResumeAdoption!.RetainFailure(new() { Code = code, Stage = "synthetic_native_contact", FirstFailedPredicate = "actual_contact" });
        GameMain.Pause();
        PreserveFailedLandingAdmission(code);
    }
}
