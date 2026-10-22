using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    private NativeResumeFailureSnapshot? _returnOnlyFirstFailure;
    private string _returnOnlyPauseOutcome = "not_requested";
    private string? _returnOnlyObservedTaskId;

    internal bool ReturnOnlyTaskStopped => _returnOnlyFirstFailure is not null
        && _returnOnlyObservedTaskId == _flightCheckpoints.VoyagerJourney?.ReturnOnlyTask?.TaskId;
    internal NativeResumeFailureSnapshot? ReturnOnlyFailureForTask(string taskId)
        => _returnOnlyObservedTaskId == taskId ? _returnOnlyFirstFailure?.Copy() : null;

    private void BindReturnOnlyGuard(string taskId)
    {
        if (_returnOnlyObservedTaskId == taskId) return;
        _returnOnlyObservedTaskId = taskId; _returnOnlyFirstFailure = null; _returnOnlyPauseOutcome = "not_requested";
    }

    internal bool CanStopIdleReturnOnlyTask(string taskId, string? sessionId, int planetId)
    {
        var j = _flightCheckpoints.VoyagerJourney;
        return j?.ReturnOnlyScopeActive == true && j.ReturnOnlyTask!.TaskId == taskId
            && j.ReturnOnlyTask.SessionId == sessionId && sessionId == _sessionId && IsCurrentSessionOwned
            && j.OwnedSaveName == _ownedSaveName && j.GameVersion == _gameVersion
            && GameMain.localPlanet?.id == planetId && (j.Phase == "remote_landed" || j.Phase == "home_awaiting_save");
    }

    internal bool StopIdleReturnOnlyTask(string taskId, string sessionId, int planetId)
    {
        if (!CanStopIdleReturnOnlyTask(taskId, sessionId, planetId)) return false;
        var j = _flightCheckpoints.VoyagerJourney!; var t = j.ReturnOnlyTask!;
        BindReturnOnlyGuard(t.TaskId);
        var e = new NativeResumeDecisionEvidence("return_only_task_cancel", GameMain.gameTick, DateTimeOffset.UtcNow, true);
        e.Number("new_remaining_ticks", j.RemainingReturnOnlyTicks(sessionId, GameMain.gameTick));
        e.Number("new_tick_cap", t.MaximumGameTicks);
        e.Observe("native_paused_before_decision", GameMain.isPaused);
        e.Require("task_continuation_authorized", false);
        _returnOnlyFirstFailure ??= e.Failure("return_only_task_stopped");
        GuardReturnOnlyTaskOnMainThread();
        return GameMain.isPaused;
    }

    // Runs after real Native501 and with owned-session refresh as a wall-clock
    // fallback. Multiple logic ticks per render still observe the exact cap.
    // Pause is the supported Native operation; no pose/energy/ground writes.
    private void GuardReturnOnlyTaskOnMainThread()
        => GuardReturnOnlyTaskOnMainThread(DateTimeOffset.UtcNow);

    internal void GuardReturnOnlyTaskOnMainThread(DateTimeOffset now)
    {
        var j = _flightCheckpoints.VoyagerJourney;
        if (j?.ReturnOnlyScopeActive != true) return;
        var t = j.ReturnOnlyTask!;
        BindReturnOnlyGuard(t.TaskId);
        var tick = GameMain.gameTick;
        var remaining = j.RemainingReturnOnlyTicks(_sessionId, tick);
        var e = new NativeResumeDecisionEvidence("return_only_task_guard", tick, now, true);
        e.Number("new_tick_cap", t.MaximumGameTicks);
        e.Number("source_tick", t.StartedAtGameTick);
        e.Number("new_remaining_ticks", remaining);
        e.Number("new_remaining_wall_seconds", (t.DeadlineUtc - now).TotalSeconds);
        e.Number("original_spent_ticks", j.SpentGameTicks);
        e.Number("original_maximum_ticks", j.MaximumGameTicks);
        e.Number("reserved_entered_601_tail_ticks", 1);
        e.Observe("native_paused_before_decision", GameMain.isPaused);
        e.NotReached("exact_owned_task_session", "tick_budget_observed", "tick_budget_remaining", "wall_budget_remaining");
        var exact = IsCurrentSessionOwned && _sessionId == t.SessionId && _ownedSaveName == j.OwnedSaveName && _gameVersion == j.GameVersion;
        var passed = e.Require("exact_owned_task_session", exact) && e.Require("tick_budget_observed", remaining.HasValue)
            && e.Require("tick_budget_remaining", remaining > 1) && e.Require("wall_budget_remaining", now < t.DeadlineUtc);
        if (passed && _returnOnlyFirstFailure is null) return;
        _returnOnlyFirstFailure ??= e.Failure(BridgeErrorCodes.StaleState);
        if (!exact) { _returnOnlyPauseOutcome = "not_requested_other_binding"; return; }
        try
        {
            if (!GameMain.isPaused) GameMain.Pause();
            _returnOnlyPauseOutcome = GameMain.isPaused ? "native_pause_verified" : "native_pause_unproved";
        }
        catch (Exception exception) { _returnOnlyPauseOutcome = "native_pause_exception:" + exception.GetType().Name; }
    }

    internal string PauseFailedReturnOnlyRecoveryOnMainThread(string adoptedSessionId, string ownedName, string checkpointId)
    {
        var s = CaptureOnMainThread();
        if (!s.GameLoaded || !s.OwnedBySpherewright || s.SessionId != adoptedSessionId || s.SaveName != ownedName
            || !s.CurrentSessionLoadedFromFlightCheckpoint || s.FlightCheckpointId != checkpointId)
            return "Native Pause not requested: exact adopted checkpoint binding is unproved.";
        try { if (!GameMain.isPaused) GameMain.Pause();
            return GameMain.isPaused ? "Native Pause verified." : "Native Pause remains unproved."; }
        catch (Exception exception) { return "Native Pause threw " + exception.GetType().Name + "."; }
    }

    private void ApplyReturnOnlyTaskState(SessionState session)
    {
        var j = _flightCheckpoints.VoyagerJourney;
        if (j?.ReturnOnlyTask is not { } t) return;
        var adoption = _checkpointReturnAdoption is { } a && a.ActionId == t.TaskId && a.SessionId == session.SessionId
            && ReferenceEquals(a.World, GameMain.data) && ReferenceEquals(a.World, _ownedData)
            && a.OwnedSaveName == session.SaveName && a.NativeVersion == session.GameVersion ? a : null;
        session.ReturnOnlyRecovery = new()
        {
            TaskId = t.TaskId,
            ScopeActive = j.ReturnOnlyScopeActive,
            DeadlineUtc = t.DeadlineUtc,
            MaximumGameTicks = t.MaximumGameTicks,
            RemainingGameTicks = j.RemainingReturnOnlyTicks(session.SessionId, session.GameTick),
            OriginalSpentGameTicks = j.SpentGameTicks,
            OriginalMaximumGameTicks = j.MaximumGameTicks,
            OriginalDeadlineUtc = j.DeadlineUtc,
            FirstFailure = adoption?.FirstFailure?.Copy() ?? ReturnOnlyFailureForTask(t.TaskId),
            PauseOutcome = _returnOnlyObservedTaskId == t.TaskId ? _returnOnlyPauseOutcome : "not_requested",
            NativeHandoffState = adoption is null ? "not_observed"
                : adoption.FirstFailure is not null ? "failed" : adoption.NativeAdmitted ? "proved" : "pending",
        };
    }
}
