using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private Action? _checkpointReturnRecoveryPump;
    internal void SetCheckpointReturnRecoveryPump(Action pump) => _checkpointReturnRecoveryPump = pump;
    internal void PumpCheckpointReturnRecoveryOnMainThread() => _checkpointReturnRecoveryPump?.Invoke();

    private void ReleaseCheckpointIdleToStartedReturn(ActionRecord action)
    {
        var owner = _voyagerRemoteIdleOwner; var j = _flightCheckpoints.VoyagerJourney;
        if (action.VoyagerNativeLaunchStarted && action.Plan.VoyagerReturnLeg && owner?.CheckpointHandoff?.Admitted == true
            && owner.SessionId == action.SessionId && owner.JourneyId == action.Plan.VoyagerJourneyId
            && j?.Phase == "return" && j.ActiveActionId == action.ActionId && j.JourneyId == owner.JourneyId
            && VoyagerRemoteIdleRuntimeMatches(owner))
        { _voyagerRemoteIdleOwner = null; _voyagerRemoteIdleScene = null; }
    }

    // Called only AFTER exact checkpoint adoption, durable old-ledger debit and
    // the separately confirmed grant. Binding references is not declaring idle.
    internal bool ObserveRecoveredReturnOnlyHealthOnMainThread(FlightCheckpointTicket ticket, string actionId,
        out NativeResumeFailureSnapshot? failure)
    {
        var now = DateTimeOffset.UtcNow;
        if (_voyagerRemoteIdleOwner?.CheckpointHandoff is { } existing && existing.ActionId == actionId)
        {
            if (_sessions.ReturnOnlyTaskStopped && _sessions.ReturnOnlyFailureForTask(actionId) is { } stopped)
                existing.Reject(stopped);
            if (!VoyagerRemoteIdleBindingMatches())
                existing.Reject(CaptureCheckpointBindingLoss(_voyagerRemoteIdleOwner!, now));
            existing.CheckObservationDeadline(GameMain.gameTick, now);
            failure = existing.FirstFailure;
            return existing.Admitted && failure is null;
        }
        // A render/poll can arrive before this world's first Native501. Wait;
        // it cannot manufacture availability or terminate a legitimate load.
        if (!VoyagerLocalPrePlayerTickAvailable || now < VoyagerLocalPrePlayerObservedAtUtc
            || now - VoyagerLocalPrePlayerObservedAtUtc > TimeSpan.FromSeconds(1))
        { failure = null; return false; }
        var j = _flightCheckpoints.VoyagerJourney;
        var task = j?.ReturnOnlyTask;
        var e = new NativeResumeDecisionEvidence("checkpoint_idle_bind", GameMain.gameTick, now, true);
        e.Number("original_spent_ticks", j?.SpentGameTicks); e.Number("new_tick_cap", task?.MaximumGameTicks);
        e.NotReached("exact_return_task", "exact_checkpoint", "exact_owned_session", "exact_native_world",
            "no_other_idle_owner", "native501_guard_available", "native501", "strict_idle", "healthy_takeover");
        var s = _sessions.CaptureOnMainThread();
        if (!e.Require("exact_return_task", j?.ReturnOnlyScopeActive == true && task?.TaskId == actionId && task.SessionId == s.SessionId)
            || !e.Require("exact_checkpoint", task!.CheckpointId == ticket.CheckpointId && task.CheckpointFingerprint == ticket.CheckpointFileFingerprint
                && ticket.ReloadActionId == actionId && ticket.LifecycleState == FlightCheckpointLifecycleStates.Recovered
                && j!.Phase == "remote_landed")
            || !e.Require("exact_owned_session", s.OwnedBySpherewright && s.CurrentSessionLoadedFromFlightCheckpoint
                && s.FlightCheckpointId == ticket.CheckpointId && s.LocalPlanetId == ticket.OriginPlanetId
                && s.SaveName == ticket.OwnedSaveName && s.GameVersion == ticket.GameVersion)
            || !e.Require("exact_native_world", GameMain.data is not null && GameMain.data.gameName == ticket.OwnedSaveName
                && GameMain.mainPlayer is not null && GameMain.mainPlayer.planetId == ticket.OriginPlanetId)
            || !e.Require("no_other_idle_owner", _voyagerRemoteIdleOwner is null)
            || !e.Require("native501_guard_available", VoyagerLocalPrePlayerTickAvailable && now >= VoyagerLocalPrePlayerObservedAtUtc
                && now - VoyagerLocalPrePlayerObservedAtUtc <= TimeSpan.FromSeconds(1)))
        { failure = e.Failure("return_only_checkpoint_idle_binding_unproved"); return false; }
        var pending = new VoyagerCheckpointIdleHandoff(actionId, GameMain.gameTick, now);
        _voyagerRemoteIdleOwner = new(new VoyagerNativeBinding(GameMain.data!, GameMain.mainPlayer!), s.SessionId!,
            j!.JourneyId, actionId, task!.OriginalFlightActionId, checkpointHandoff: pending);
        _voyagerRemoteIdleScene = null;
        // Fund the next complete601 and stop before allowing another frame.
        // An Update/poll preflight cannot publish Native healthy admission.
        if (!_observingVoyagerPostPhysics)
            CaptureCheckpointReturnIdleAdmission(_voyagerRemoteIdleOwner, j, postPhysics: false);
        failure = pending.FirstFailure;
        return false; // Only a later verified Native501 guard can complete it.
    }

    private NativeResumeFailureSnapshot CaptureCheckpointBindingLoss(VoyagerRemoteIdleOwner owner, DateTimeOffset now)
    {
        var sameWorld = ReferenceEquals(GameMain.data, owner.Binding.World);
        var e = new NativeResumeDecisionEvidence("checkpoint_idle_reference_loss", sameWorld ? GameMain.gameTick : null, now, true);
        e.Require("same_native_world", sameWorld);
        e.Require("same_owned_session", _sessions.IsCurrentSessionOwned && _sessions.SessionId == owner.SessionId);
        e.Require("same_native_player", ReferenceEquals(GameMain.mainPlayer, owner.Binding.Player));
        e.Require("same_native_controller", ReferenceEquals(owner.Binding.Player.controller, owner.Binding.Controller));
        e.Require("same_native_mecha", ReferenceEquals(owner.Binding.Player.mecha, owner.Binding.Mecha));
        e.Require("current_idle_binding", false);
        return e.Failure("remote_idle_binding_unproved");
    }

    private VoyagerRemoteIdleAdmissionDecision CaptureCheckpointReturnIdleAdmission(VoyagerRemoteIdleOwner owner, VoyagerJourneyRecord j, bool postPhysics = true)
    {
        var tick = GameMain.gameTick; var now = DateTimeOffset.UtcNow;
        if (_sessions.CheckpointReturnTakeoverFailure(owner.ActionId) is { } takeoverFailure)
        {
            owner.CheckpointHandoff!.Reject(takeoverFailure);
            _sessions.FinishCheckpointReturnHandoff(owner.ActionId, owner.CheckpointHandoff.FirstFailure);
            return VoyagerRemoteIdleAdmissionPolicy.Blocked(owner.CheckpointHandoff.FirstFailure!);
        }
        var e = new NativeResumeDecisionEvidence(postPhysics ? "checkpoint_native_post501" : "checkpoint_idle_preflight", tick, now, true);
        e.Number("original_spent_ticks", j.SpentGameTicks); e.Number("original_maximum_ticks", j.MaximumGameTicks);
        e.Number("new_remaining_ticks", j.RemainingReturnOnlyTicks(owner.SessionId, tick));
        e.Observe("native_paused", GameMain.isPaused);
        e.NotReached("native_capture", "native_idle", "strict_idle", "healthy_takeover");
        var decision = CaptureVoyagerRemoteIdleAdmission(owner, j, _voyagerRemoteIdleScene, j.SpentGameTicks, j.LastLandedGameTick,
            out var scene, captureFacts: f => RecordRemoteResumeFacts(e, f), captureGround: g => e.Observe("ground_sphere_hit", g.CheckSphereHit),
            groundStage: postPhysics ? "checkpoint_native_post501" : "checkpoint_idle_preflight");
        if (!decision.Proved && decision.FailureEvidence is null && decision.CaptureStage is { } captureStage)
        {
            // This is the first failing capture group. Its internal Native
            // leaves were not recorded; do not guess them or resample after Pause.
            e.Require("native_capture_" + captureStage, false);
            var group = e.Failure(decision.BlockerCode ?? "checkpoint_native_capture_unproved");
            group.Predicates["unrecorded_capture_leaf"] = "not_observed";
            decision = VoyagerRemoteIdleAdmissionPolicy.Blocked(group);
        }
        if (!postPhysics)
        {
            if (!decision.Proved)
            {
                e.Require("native_preflight_funding", false);
                owner.CheckpointHandoff!.Reject(decision.FailureEvidence ?? e.Failure(decision.BlockerCode ?? "checkpoint_idle_preflight_unproved", decision.CaptureStage));
            }
            _voyagerRemoteIdleScene = scene;
            return decision;
        }
        owner.CheckpointHandoff!.ObservePostPhysics(tick, now, _observingVoyagerPostPhysics && VoyagerRemoteIdleBindingMatches(owner, j), decision, e);
        if (owner.CheckpointHandoff.FirstFailure is { } failure)
        {
            _sessions.FinishCheckpointReturnHandoff(owner.ActionId, failure);
            return VoyagerRemoteIdleAdmissionPolicy.Blocked(failure);
        }
        if (owner.CheckpointHandoff.Admitted) _sessions.FinishCheckpointReturnHandoff(owner.ActionId, null);
        _voyagerRemoteIdleScene = scene;
        return decision;
    }
    private static void RecordRemoteResumeFacts(NativeResumeDecisionEvidence e, VoyagerRemoteIdleAdmissionFacts facts)
    {
        e.Require("native_capture", true);
        e.Observe("movement_reserve_proved", facts.MovementReserve?.Proved);
        e.Number("core_j", facts.CoreEnergyJoules); e.Number("per_tick_j", facts.MovementReserve?.PerNativeTickEnergyJoules);
        var j = facts.JourneyBudget;
        e.Observe("journey_binding", j?.ExactRemoteBinding); e.Observe("native_idle", j?.NativePlayerIdle);
        e.Number("loaded_j", j?.AvailableLoadedEnergyJoules); e.Number("return_floor_j", j?.LockedReturnFloorJoules);
        e.Number("spent_ticks", j?.SpentGameTicks); e.Number("last_landed_tick", j?.LastLandedGameTick);
        e.Number("maximum_ticks", j?.MaximumGameTicks); e.Number("return_ticks", j?.ReturnReserveGameTicks);
        e.Number("warpers", j?.NativeWarperCount);
        e.Number("return_wall_seconds", j is null ? null : (j.JourneyDeadlineUtc - j.NowUtc).TotalSeconds);
    }

}
