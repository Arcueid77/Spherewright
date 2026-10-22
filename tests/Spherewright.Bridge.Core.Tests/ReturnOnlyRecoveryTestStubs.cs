using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

// Only the documented Pause effect and copied owned-session identity are
// substituted. Store/coordinator/commit scope/deadline decisions are production.
internal static partial class GameMain
{
    internal static int ReturnOnlyPauseCalls;
    internal static void Pause() { ReturnOnlyPauseCalls++; isPaused = true; }
}

internal sealed partial class GameSessionTracker
{
    internal void BindReturnOnlyGuardForTest(FlightCheckpointStore store)
    {
        _flightCheckpoints = store; _sessionId = Snapshot.SessionId;
        _ownedSaveName = Snapshot.SaveName; _gameVersion = Snapshot.GameVersion;
        _writeHealth = Snapshot.WriteHealth = WriteHealthStates.Healthy;
        _ownedSaveState = OwnedSaveStates.Saved;
        _ownedData = GameMain.data = new() { gameName = _ownedSaveName!, localLoadedPlanetFactory = new object() };
        GameMain.localPlanet = new() { id = Snapshot.LocalPlanetId!.Value, factory = _ownedData.localLoadedPlanetFactory };
        GameMain.gameTick = Snapshot.GameTick!.Value; GameMain.isPaused = false;
    }

    internal void RefreshReturnOnlyScopeForTest()
    {
        Snapshot.WriteBlockers = CreateWriteBlockers(PeacefulModeStates.ConfirmedPeaceful);
        Snapshot.WritesAllowed = Snapshot.WriteBlockers.Count == 0;
        Snapshot.GameTick = GameMain.gameTick;
        ApplyReturnOnlyTaskState(Snapshot);
    }
}

internal sealed partial class NormalGameActionCoordinator
{
    internal static BridgeError? ReturnOnlyCommitAccessForTest(SessionState session, string kind, bool returnLeg)
        => ValidateCommitCommon(session, new NormalActionPlanPayload {
            SessionId = session.SessionId!, PlanetId = session.LocalPlanetId!.Value,
            ActionKind = kind, VoyagerReturnLeg = returnLeg,
            VoyagerBudget = returnLeg ? new() : null,
        }, new CommitNormalActionRequest { SessionId = session.SessionId!, PlanetId = session.LocalPlanetId!.Value });
}
