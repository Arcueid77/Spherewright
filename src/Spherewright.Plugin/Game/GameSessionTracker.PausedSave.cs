using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    private readonly PausedOwnedSaveGuard _pausedSaveGuard = new();
    private string? _postAdmissionIdleQuarantineActionId;

    // Only this production idle-guard call site can establish provenance.
    // A completed resume remains completed; quarantine is never cleared here.
    internal void QuarantineRemoteIdleOnMainThread(string actionId, string reason)
    {
        QuarantineWritesOnMainThread(actionId, reason);
        if (_writeQuarantineActionId == actionId && _writeQuarantineReason == reason
            && _remoteResumeAdmitted && _remoteResumeAdoption?.ResumeActionId == actionId)
            _postAdmissionIdleQuarantineActionId = actionId;
    }

    internal void ObservePausedOwnedSaveOnMainThread() => _pausedSaveGuard.Observe(CapturePausedOwnedSave());

    internal bool TryBindPausedOwnedSave(out PausedOwnedSaveBinding? binding, out string rejection) =>
        _pausedSaveGuard.TryBind(CapturePausedOwnedSave(), out binding, out rejection);

    internal bool ValidatePausedOwnedSave(PausedOwnedSaveBinding binding, out string rejection) =>
        _pausedSaveGuard.Validate(binding, CapturePausedOwnedSave(), out rejection);

    internal Spherewright.Contracts.Players.NativeResumeFailureSnapshot? PausedSaveFirstFailure => _pausedSaveGuard.LastFailure;

    private PausedOwnedSaveObservation CapturePausedOwnedSave()
    {
        var world = GameMain.data;
        var adoption = _remoteResumeAdoption;
        var observation = new PausedOwnedSaveObservation
        {
            World = world, SessionId = _sessionId, OwnedSaveName = _ownedSaveName, NativeVersion = _gameVersion,
            PlanetId = GameMain.localPlanet?.id ?? 0, Revision = _revision, GameTick = GameMain.gameTick,
            RenderFrame = UnityEngine.Time.frameCount, NativePaused = GameMain.isPaused,
            QuarantineActionId = _writeQuarantineActionId, QuarantineReason = _writeQuarantineReason,
        };
        var names = new[] { "owned_session", "owned_world", "owned_embedded_identity", "quarantined", "idle_quarantine_witness",
            "idle_quarantine_action", "adoption_present", "admission_completed",
            "adopted_world", "adopted_session", "original_quarantine_action", "first_frame_failure_absent", "session_adoption_error_absent",
            "adoption_not_pending", "journal_not_pending", "source_lease_released", "primary_lease_released",
            "journal_lease_released", "checkpoint_lease_released", "reload_not_pending", "native_running",
            "factory_present", "factory_identity", "previous_normal_save", "checkpoint_adoption_finished", "checkpoint_preservation_witness",
            "checkpoint_saved_source", "saved_watermark_present", "saved_progress_not_newer", "other_blockers_absent" };
        observation.CapturedAtUtc = DateTimeOffset.UtcNow;
        observation.Predicates = names.ToDictionary(n => n, _ => "not_reached");
        bool Require(string name, bool? proved, string code)
        {
            observation.Predicates[name] = proved.HasValue ? proved.Value ? "true" : "false" : "unknown";
            if (proved == true) return true;
            observation.Blocker = code; observation.FirstFailedPredicate = name; return false;
        }
        if (!Require("owned_session", IsCurrentSessionOwned, "paused_save_owned_world_unproved")
            || !Require("owned_world", ReferenceEquals(world, _ownedData), "paused_save_owned_world_unproved")
            || !Require("owned_embedded_identity", world?.gameName == _ownedSaveName, "paused_save_owned_world_unproved")
            || !Require("quarantined", _writeHealth == WriteHealthStates.Quarantined, "paused_save_post_admission_quarantine_required")) return observation;
        // This role proves exact checkpoint identity/adoption, NOT healthy idle.
        // Saving the fully loaded, stably paused world grants no Load/Resume.
        var checkpointRole = _checkpointReturnAdoption is { } checkpoint && checkpoint.ActionId == _writeQuarantineActionId;
        var returnSourceRole=checkpointRole&&_checkpointReturnAdoption!.ReturnContinuation;
        var savedBoundary=_lastOwnedSaveGameTick??(returnSourceRole?_checkpointReturnAdoption!.SavedSourceGameTick:(long?)null);
        var savedSourceState=_ownedSaveState==OwnedSaveStates.Saved
            ||returnSourceRole&&_ownedSaveState==OwnedSaveStates.WaitingToSave;
        if (checkpointRole)
        {
            if (!Require("checkpoint_adoption_finished", _checkpointReturnAdoption!.Finished,
                    "paused_save_checkpoint_adoption_unproved")
                || !Require("checkpoint_preservation_witness", CheckpointReturnPauseWitnessMatches(),
                    "paused_save_checkpoint_adoption_unproved")
                || !Require("checkpoint_saved_source", savedSourceState,
                    "paused_save_checkpoint_adoption_unproved")) return observation;
        }
        else
        {
            if (!Require("idle_quarantine_witness", _postAdmissionIdleQuarantineActionId is not null, "paused_save_post_admission_quarantine_required")
                || !Require("idle_quarantine_action", _postAdmissionIdleQuarantineActionId == _writeQuarantineActionId, "paused_save_post_admission_quarantine_required")
                || !Require("adoption_present", adoption is not null, "paused_save_completed_admission_unproved")
                || !Require("admission_completed", _remoteResumeAdmitted, "paused_save_completed_admission_unproved")) return observation;
            if (!Require("adopted_world", ReferenceEquals(adoption!.World, world), "paused_save_completed_admission_unproved")
                || !Require("adopted_session", adoption.SessionId == _sessionId, "paused_save_completed_admission_unproved")
                || !Require("original_quarantine_action", adoption.ResumeActionId == _writeQuarantineActionId, "paused_save_completed_admission_unproved")
                || !Require("first_frame_failure_absent", adoption.FirstFrame.Failure is null, "paused_save_completed_admission_unproved")
                || !Require("session_adoption_error_absent", _resumeAdoptionError is null, "paused_save_completed_admission_unproved")) return observation;
        }
        if (!Require("adoption_not_pending", !HasPendingRemoteResumeAdoption, "paused_save_load_or_journal_pending")
            || !Require("journal_not_pending", _pendingJournalResumeTicket is null, "paused_save_load_or_journal_pending")
            || !Require("source_lease_released", _resumeSourceLease is null, "paused_save_load_or_journal_pending")
            || !Require("primary_lease_released", _resumePrimaryLease is null, "paused_save_load_or_journal_pending")
            || !Require("journal_lease_released", _reauthorizationJournalLease is null, "paused_save_load_or_journal_pending")
            || !Require("checkpoint_lease_released", _flightCheckpointSourceLease is null, "paused_save_load_or_journal_pending")
            || !Require("reload_not_pending", !_flightCheckpoints.HasUnreconciledReload, "paused_save_load_or_journal_pending")
            || !Require("native_running", GameMain.isRunning, "paused_save_current_factory_or_saved_progress_unproved")
            || !Require("factory_present", world?.localLoadedPlanetFactory is not null, "paused_save_current_factory_or_saved_progress_unproved")
            || !Require("factory_identity", ReferenceEquals(world!.localLoadedPlanetFactory, GameMain.localPlanet?.factory), "paused_save_current_factory_or_saved_progress_unproved")
            || !Require(checkpointRole ? "checkpoint_saved_source" : "previous_normal_save", savedSourceState,
                "paused_save_current_factory_or_saved_progress_unproved")
            || !Require("saved_watermark_present", savedBoundary.HasValue, "paused_save_current_factory_or_saved_progress_unproved")
            || !Require("saved_progress_not_newer", savedBoundary <= observation.GameTick,
                "paused_save_current_factory_or_saved_progress_unproved")) return observation;
        // Pure current flags only: no file/ticket enumeration or extra Native query.
        var peaceful = world!.gameDesc is null ? PeacefulModeStates.Unknown
            : world.gameDesc.isPeaceMode ? PeacefulModeStates.ConfirmedPeaceful : PeacefulModeStates.ConfirmedCombat;
        var blockers = VoyagerReturnOnlyRecoveryPolicy.ActionBlockers(CreateWriteBlockers(peaceful),
            Spherewright.Contracts.Actions.NormalActionKinds.Save, false);
        Require("other_blockers_absent", blockers.Count == 1
            && blockers[0].Code == Spherewright.Contracts.Errors.BridgeErrorCodes.WriteSubsystemQuarantined
            && blockers[0].Message == observation.QuarantineReason, "paused_save_other_write_blocker");
        return observation;
    }
}
