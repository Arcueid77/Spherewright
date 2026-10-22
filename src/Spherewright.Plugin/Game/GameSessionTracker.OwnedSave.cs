using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    public bool TrySaveOwnedWorldNowOnMainThread(out string? error, PausedOwnedSaveBinding? preservation = null)
    {
        error = null;
        if (preservation is not null && !ValidatePausedOwnedSave(preservation, out error)) return false;
        if (!RemoteResumeAllowsPrimaryResave())
        { error = "remote_resume_strict_idle_admission_pending_or_unproved"; return false; }
        if (!IsCurrentSessionOwned
            || _pendingJournalResumeTicket is not null
            || _resumeSourceLease is not null
            || string.IsNullOrWhiteSpace(_ownedSaveName)
            || GameMain.data?.localLoadedPlanetFactory is null)
        {
            error = "The exact owned world or local factory is unavailable.";
            return false;
        }

        bool? nativeReturned = null;
        var failedPredicate = "native_save_returned_true";
        var saveEvidence = preservation is null ? null
            : new NativeResumeDecisionEvidence("paused_owned_native_save", null, null, true);
        saveEvidence?.NotReached("native_save_returned_true", "native_save_complete_prefix", "native_save_owned_identity",
            "native_save_version", "native_save_peaceful", "native_save_current_tick", "native_save_complete_file_hash",
            "native_save_bound_progress_and_quarantine");
        saveEvidence?.Number("bound_game_tick", preservation?.GameTick);
        saveEvidence?.Number("native_file_tick", null);
        saveEvidence?.Number("native_ticks_requested", 0);
        void RetainSaveFailure(string predicate, string exceptionType)
        {
            if (saveEvidence is null) return;
            var cut = saveEvidence.Failure(Spherewright.Contracts.Errors.BridgeErrorCodes.ActionOutcomeUnknown, predicate);
            cut.CapturedAtUtc = DateTimeOffset.UtcNow; // Failure time; no new Native sample.
            cut.Observations["exception_type"] = exceptionType;
            cut.Observations["native_physics"] = "not_observed";
            preservation!.RecordNativeSaveFailure(cut);
        }
        try
        {
            GameMain.gameName = _ownedSaveName;
            nativeReturned = GameSave.SaveCurrentGame(_ownedSaveName);
            saveEvidence?.Require("native_save_returned_true", nativeReturned);
            if (nativeReturned != true)
            {
                RetainSaveFailure("native_save_returned_true", "");
                _ownedSaveState = OwnedSaveStates.SaveFailed;
                _ownedSaveError = "The game save API returned false.";
                error = _ownedSaveError;
                _logger.LogError("Spherewright could not save the owned ordinary world");
                return false;
            }

            // A successful native return alone cannot issue a new-version capability.
            // Re-read the newly written exact identity and researched format tuple first.
            failedPredicate = "native_save_complete_prefix";
            using (var savedStream = new FileStream(GameSave.SavePath(_ownedSaveName), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var savedPrefix = OwnedSavePrefixReader.Read(savedStream, _ownedSaveName!);
                saveEvidence?.Require("native_save_complete_prefix", true);
                saveEvidence?.Number("native_file_tick", savedPrefix.GameTick);
                bool Required(string name, bool value)
                {
                    failedPredicate = name; saveEvidence?.Require(name, value); return value;
                }
                if (!Required("native_save_owned_identity", savedPrefix.MatchesExpectedIdentity)
                    || !Required("native_save_version", savedPrefix.GameVersion == _gameVersion)
                    || !Required("native_save_peaceful", savedPrefix.Peaceful)
                    || !Required("native_save_current_tick", savedPrefix.GameTick == GameMain.gameTick))
                    throw new InvalidDataException("The normal owned save did not read back its exact identity, current format and tick.");
            }

            if (preservation is not null)
            {
                failedPredicate = "native_save_complete_file_hash";
                using var complete = OwnedSaveRecoveryLease.Open(GameSave.SavePath(_ownedSaveName), _ownedSaveName!);
                saveEvidence?.Require("native_save_complete_file_hash", true);
                failedPredicate = "native_save_bound_progress_and_quarantine";
                if (complete.Prefix.GameTick != preservation.GameTick || complete.Prefix.GameTick != GameMain.gameTick
                    || complete.Prefix.GameVersion != _gameVersion || !complete.Prefix.Peaceful
                    || !ValidatePausedOwnedSave(preservation, out _))
                    throw new InvalidDataException("The paused owned save did not prove complete current progress and unchanged quarantine.");
                saveEvidence?.Require("native_save_bound_progress_and_quarantine", true);
            }

            _ownedSaveState = OwnedSaveStates.Saved;
            _ownedSaveError = null;
            _lastOwnedSaveGameTick = GameMain.gameTick;
            if (preservation is null && !_flightCheckpoints.TryRetireAfterPrimarySave(
                    _ownedSaveName!,
                    _lastOwnedSaveGameTick.Value,
                    out _,
                    out var checkpointRetirementError))
            {
                _logger.LogWarning($"Spherewright could not finalize flight-checkpoint retirement after the covering primary save: {checkpointRetirementError}");
            }

            if (string.Equals(_writeHealth, WriteHealthStates.Healthy, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(_sessionId)
                && GameMain.localPlanet?.id is int localPlanetId
                && localPlanetId > 0)
            {
                try
                {
                    _resumeTickets.ArmFromHealthySavedOwnedSession(
                        _ownedSaveName!,
                        _sessionId!,
                        localPlanetId,
                        _lastOwnedSaveGameTick.Value,
                        CaptureGameplayJournalCheckpointOnMainThread());
                }
                catch (Exception exception)
                {
                    _logger.LogWarning($"Spherewright could not arm planned restart-resume after the healthy save ({exception.GetType().Name})");
                }
            }

            _logger.LogInfo("Spherewright saved the owned ordinary world");
            return true;
        }
        catch (Exception exception)
        {
            // Exceptions can leave the current check unobserved. Do not turn
            // an I/O failure or absent file tick into a false raw observation.
            var prior = saveEvidence?.Failure("", failedPredicate);
            if (prior is not null && (!prior.Predicates.TryGetValue(failedPredicate, out var state) || state == "not_reached"))
                saveEvidence!.Require(failedPredicate, null);
            RetainSaveFailure(failedPredicate, exception.GetType().Name);
            _ownedSaveState = OwnedSaveStates.SaveFailed;
            _ownedSaveError = exception.GetType().Name;
            error = _ownedSaveError;
            _logger.LogError($"Spherewright owned-world save failed ({_ownedSaveError})");
            return false;
        }
    }
}
