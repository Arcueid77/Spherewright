using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Errors;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    internal const string CheckpointReturnPendingMessage = "The exact return checkpoint is adopted but awaits its Native501 idle/energy handoff; normal writes and resave remain blocked.";

    internal NativeResumeFailureSnapshot? CheckpointReturnTakeoverFailure(string actionId)
    {
        var e = new NativeResumeDecisionEvidence("checkpoint_idle_takeover", GameMain.gameTick, DateTimeOffset.UtcNow, true);
        var a = _checkpointReturnAdoption;
        var peaceful = GameMain.data?.gameDesc is null ? PeacefulModeStates.Unknown
            : GameMain.data.gameDesc.isPeaceMode ? PeacefulModeStates.ConfirmedPeaceful : PeacefulModeStates.ConfirmedCombat;
        var blockers = CreateWriteBlockers(peaceful).Where(b =>
            !(b.Code == VoyagerReturnOnlyRecoveryPolicy.ScopeBlocker && b.Message == VoyagerReturnOnlyRecoveryPolicy.ScopeMessage)
            && !(b.Code == BridgeErrorCodes.BridgeNotReady && b.Message == CheckpointReturnPendingMessage)).ToList();
        e.Number("remaining_write_blocker_count", blockers.Count);
        e.NotReached("exact_adoption_witness", "healthy_write_subsystem", "remaining_write_blockers");
        return e.Require("exact_adoption_witness", a is not null && a.ActionId == actionId && a.SessionId == _sessionId
                && ReferenceEquals(a.World, GameMain.data) && ReferenceEquals(a.World, _ownedData))
            && e.Require("healthy_write_subsystem", _writeHealth == WriteHealthStates.Healthy)
            && e.Require("remaining_write_blockers", blockers.Count == 0)
                ? null : e.Failure("return_only_checkpoint_write_blocked");
    }

    private CheckpointReturnAdoption? _checkpointReturnAdoption;
    private bool CheckpointReturnHandoffPending => _checkpointReturnAdoption is { Finished: false } a
        && a.SessionId == _sessionId && ReferenceEquals(a.World, GameMain.data);

    internal bool RegisterCheckpointReturnAdoption(FlightCheckpointTicket t, string actionId, out string rejection)
    {
        rejection = "checkpoint_return_adoption_witness_unproved";
        var s = CaptureOnMainThread(); var j = _flightCheckpoints.VoyagerJourney;
        if (!s.OwnedBySpherewright || !s.CurrentSessionLoadedFromFlightCheckpoint || s.FlightCheckpointId != t.CheckpointId
            || s.SaveName != t.OwnedSaveName || s.GameVersion != t.GameVersion || s.LocalPlanetId != t.OriginPlanetId
            || !ReferenceEquals(_ownedData, GameMain.data) || GameMain.data?.gameName != t.OwnedSaveName
            || t.ReloadActionId != actionId || t.LifecycleState != FlightCheckpointLifecycleStates.Recovered
            || j?.ReturnOnlyTask is not { } task || task.TaskId != actionId || task.SessionId != s.SessionId
            || task.CheckpointId != t.CheckpointId || task.CheckpointFingerprint != t.CheckpointFileFingerprint
            || _flightCheckpoints.HasUnreconciledReload) return false;
        if (_checkpointReturnAdoption is { } previous)
            return previous.ActionId == actionId && previous.SessionId == s.SessionId && ReferenceEquals(previous.World, GameMain.data);
        _checkpointReturnAdoption = new()
        {
            World = GameMain.data!,
            SessionId = s.SessionId!,
            ActionId = actionId,
            OwnedSaveName = t.OwnedSaveName,
            NativeVersion = t.GameVersion,
            CheckpointId = t.CheckpointId,
            CheckpointFingerprint = t.CheckpointFileFingerprint
        };
        rejection = ""; return true;
    }

    internal void FinishCheckpointReturnHandoff(string actionId, NativeResumeFailureSnapshot? failure)
    {
        var a = _checkpointReturnAdoption;
        if (a is null || a.ActionId != actionId || a.SessionId != _sessionId || !ReferenceEquals(a.World, GameMain.data)) return;
        a.Finished = true;
        if (failure is not null) a.FirstFailure ??= failure.Copy();
        else a.NativeAdmitted = true;
    }

    private bool CheckpointReturnPauseWitnessMatches()
    {
        var a = _checkpointReturnAdoption; var t = _flightCheckpoints.CurrentTicket;
        return a is { Finished: true } && a.ActionId == _writeQuarantineActionId && a.SessionId == _sessionId
            && ReferenceEquals(a.World, GameMain.data) && ReferenceEquals(a.World, _ownedData)
            && a.OwnedSaveName == _ownedSaveName && a.NativeVersion == _gameVersion
            && t is not null && t.CheckpointId == a.CheckpointId && t.CheckpointFileFingerprint == a.CheckpointFingerprint
            && t.ReloadActionId == a.ActionId && t.LifecycleState == FlightCheckpointLifecycleStates.Recovered
            && _flightCheckpoints.VoyagerJourney?.ReturnOnlyTask is { } task && task.TaskId == a.ActionId
            && task.SessionId == _sessionId;
    }

    private sealed class CheckpointReturnAdoption
    {
        internal object World = null!;
        internal string SessionId = "", ActionId = "", OwnedSaveName = "", NativeVersion = "", CheckpointId = "", CheckpointFingerprint = "";
        internal bool Finished, NativeAdmitted;
        internal NativeResumeFailureSnapshot? FirstFailure;
    }
}
