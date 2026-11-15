using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    private readonly PausedOwnedSaveGuard _pausedSaveGuard = new();
    // A completed resume remains completed; quarantine is never cleared here.
    internal void QuarantineRemoteIdleOnMainThread(string actionId, string reason)
    {
        QuarantineWritesOnMainThread(actionId, reason);
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
        var observation = new PausedOwnedSaveObservation
        {
            World = world, SessionId = _sessionId, OwnedSaveName = _ownedSaveName, NativeVersion = _gameVersion,
            PlanetId = GameMain.localPlanet?.id ?? 0, Revision = _revision, GameTick = GameMain.gameTick,
            RenderFrame = UnityEngine.Time.frameCount, NativePaused = GameMain.isPaused,
            QuarantineActionId = _writeQuarantineActionId, QuarantineReason = _writeQuarantineReason,
        };
        var names = new[] { "owned_session", "owned_world", "owned_embedded_identity", "quarantined",
            "adoption_not_pending", "journal_not_pending", "source_lease_released", "primary_lease_released",
            "journal_lease_released", "checkpoint_lease_released", "reload_not_pending", "native_running",
            "factory_present", "factory_identity", "saved_progress_not_newer", "other_blockers_absent" };
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
            || !Require("quarantined", _writeHealth == WriteHealthStates.Quarantined, "paused_save_quarantine_required")) return observation;
        // Serialization is independent of how quarantine arose: an unknown
        // action, failed contact, water or a previous save failure is not a
        // reason to discard current progress. This grants no Load or Resume.
        var savedBoundary = _lastOwnedSaveGameTick
            ?? (_checkpointReturnAdoption?.ReturnContinuation == true ? _checkpointReturnAdoption.SavedSourceGameTick : (long?)null);
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
            || !Require("saved_progress_not_newer", !savedBoundary.HasValue || savedBoundary <= observation.GameTick,
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
