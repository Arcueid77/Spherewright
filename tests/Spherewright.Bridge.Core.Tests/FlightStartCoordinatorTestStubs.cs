using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

// TEST ONLY. The production start methods/store are linked. These native and
// session substitutes prove ordering/identity, never Unity flight or real saves.
internal static class GameMain
{
    internal static long gameTick;
    internal static string? gameName;
    internal static FlightStartTestPlanet localPlanet = new();
    internal static FlightStartTestPlayer mainPlayer = new();
}

internal sealed class FlightStartTestPlanet { internal int id; }
internal sealed class FlightStartTestPlayer { internal FlightStartTestController controller = new(); }
internal sealed class FlightStartTestController { internal FlightStartTestBuild actionBuild = new(); }
internal sealed class FlightStartTestBuild { internal EBlueprintMode blueprintMode; }
internal enum EBlueprintMode { None, Build }

internal static partial class GameSave
{
    internal static int FlightStartSaveCalls;
    internal static Func<string, bool>? FlightStartSave;
    internal static bool SaveCurrentGame(string name)
    {
        FlightStartSaveCalls++;
        return FlightStartSave?.Invoke(name) ?? false;
    }
}

internal sealed partial class GameSessionTracker
{
    internal string? FlightStartCheckpointId;
    internal bool FlightStartLoadedFromCheckpoint;
    internal Action<FlightCheckpointTicket>? AfterFlightStartCheckpointMarked;

    internal void MarkCurrentSessionFlightCheckpoint(FlightCheckpointTicket ticket)
    {
        if (!IsCurrentSessionOwned || Snapshot.SessionId != ticket.SourceSessionId
            || Snapshot.SaveName != ticket.OwnedSaveName || Snapshot.Revision != ticket.SourceRevision
            || GameMain.localPlanet.id != ticket.OriginPlanetId || GameMain.gameTick < ticket.SavedGameTick)
            throw new InvalidOperationException("Synthetic session does not match the completed checkpoint.");
        FlightStartCheckpointId = ticket.CheckpointId;
        FlightStartLoadedFromCheckpoint = false;
        AfterFlightStartCheckpointMarked?.Invoke(ticket);
    }

    // Preserve the production distinction which triggered the bug: a newly
    // marked, non-recovered ticket is not reusable until revision advances.
    internal bool CanReuseFlightCheckpointForCurrentSession(FlightCheckpointTicket ticket)
    {
        if (!IsCurrentSessionOwned || Snapshot.SaveName != ticket.OwnedSaveName
            || FlightStartCheckpointId != ticket.CheckpointId || GameMain.localPlanet.id != ticket.OriginPlanetId)
            return false;
        return FlightStartLoadedFromCheckpoint
            ? Snapshot.Revision == 1 && GameMain.gameTick >= ticket.SavedGameTick
            : Snapshot.SessionId == ticket.SourceSessionId && Snapshot.Revision == ticket.SourceRevision + 1
                && GameMain.gameTick >= ticket.SavedGameTick;
    }
}

internal sealed partial class NormalGameActionCoordinator
{
    private readonly FlightCheckpointStore _flightCheckpoints = null!;
    private readonly GameSessionTracker _sessions = null!;
    internal int NativeFlightEntries;
    internal Action? BeforeNativeFlightEntry;
    internal NormalGameActionCoordinator() { }
    internal NormalGameActionCoordinator(FlightCheckpointStore store, GameSessionTracker sessions)
    { _flightCheckpoints = store; _sessions = sessions; }
    internal void StartVoyagerForTest(ActionRecord action) => StartVoyagerFlightOnMainThread(action);
    internal void StartSameStarForTest(ActionRecord action) => StartInterplanetaryFlightOnMainThread(action);
    internal void LaunchBoundForTest(ActionRecord action) => LaunchInterplanetaryFlightFromBoundCheckpointOnMainThread(action);
    private void InitializeVoyagerFlightNavigationOnMainThread(ActionRecord action) => action.VoyagerPhase = "checkpoint";
    private void CaptureVoyagerReadback(ActionRecord action) { }
    private void RequestVoyagerStop(ActionRecord action, string reason) => Fail(action, reason);
    private void Fail(ActionRecord action, string message)
    { action.State = NormalActionStates.ActionFailed; action.Message = message; action.Terminal = true; }
    private void EnterNativeFlight(FlightStartTestPlayer player)
    { BeforeNativeFlightEntry?.Invoke(); NativeFlightEntries++; }

    internal sealed class ActionRecord
    {
        internal string ActionId = Guid.NewGuid().ToString("D");
        internal string SessionId = string.Empty;
        internal int PlanetId;
        internal string State = NormalActionStates.Executing;
        internal string Message = string.Empty;
        internal bool Terminal;
        internal string? FlightCheckpointId;
        internal string? FlightCheckpointReloadToken;
        internal long? FlightCheckpointGameTick;
        internal double FlightBestDistance;
        internal long FlightBestDistanceAtGameTick;
        internal long FlightLastControlGameTick;
        internal long FlightDestinationContactAtGameTick;
        internal long FlightStableLandingAtGameTick;
        internal bool VoyagerNativeLaunchStarted;
        internal long VoyagerDeadlineGameTick;
        internal string VoyagerPhase = string.Empty;
        internal VoyagerJourneyRecord? VoyagerJourney;
        internal NormalActionPlanPayload Plan = new();
    }

    internal sealed class NormalActionPlanPayload
    {
        internal int PlanetId;
        internal int DestinationPlanetId;
        internal double EstimatedDistance = 1_000_000d;
        internal string PlayerStateHash = "synthetic-player";
        internal string StarSystemStateHash = "synthetic-catalogue";
        internal VoyagerRouteBudgetSnapshot? VoyagerBudget;
        internal PreviewVoyagerRouteRequest? VoyagerRequest;
        internal string? VoyagerJourneyId;
        internal bool VoyagerReturnLeg;
    }
}
