using Spherewright.Contracts.Players;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Spherewright.Bridge.Core.Safety;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    private NativeResumeFailureSnapshot? _returnOnlySessionBindingFailure;
    internal NativeResumeFailureSnapshot? ReturnOnlySessionBindingFailure => _returnOnlySessionBindingFailure?.Copy();
    internal NativeResumeFailureSnapshot? RemoteResumeFailureForAction(string actionId)
    {
        if (_remoteResumeAdoption?.ResumeActionId == actionId && _remoteResumeAdoption.FirstFailure is { } native) return native.Copy();
        if (_pendingRemoteResumeActionId == actionId && _returnOnlySessionBindingFailure is { } binding) return binding.Copy();
        var run = _flightCheckpoints?.VoyagerJourney?.ReturnOnlyTask?.CurrentContinuation;
        return run?.ActionId == actionId ? run.FirstFailure?.Copy() : null;
    }
    internal bool TryOpenReturnOnlyContinuationSource(OwnedWorldResumeTicketStore credentials, string token,
        DateTimeOffset? deadline, out VoyagerReturnOnlyContinuationSource? evidence, out NativeResumeFailureSnapshot? failure)
        => _flightCheckpoints.TryOpenReturnOnlyContinuationSource(credentials, token, deadline, out evidence, out failure);

    internal bool RegisterReturnOnlyContinuation(VoyagerReturnOnlyContinuationSource source, string actionId,
        bool confirmed, string digest, DateTimeOffset now, out string rejection)
        => _flightCheckpoints.TryRegisterReturnOnlyContinuation(source, actionId, confirmed, digest, now, out rejection);

    internal string ReturnOnlyContinuationJourneyJson() => PluginJson.Serialize(_flightCheckpoints.VoyagerJourney);
    internal NativeResumeFailureSnapshot RecordReturnOnlyPreloadConsumptionFailure(string actionId)
    {
        var e = new NativeResumeDecisionEvidence("return_only_preload_consumption", null, DateTimeOffset.UtcNow, false);
        e.NotReached("native_load", "native_initialization", "contact", "strict_idle", "normal_save");
        e.Require("durable_credential_consumption", null);
        var cut = e.Failure("remote_resume_durable_credential_consumption_failed");
        _flightCheckpoints.RecordReturnOnlyPreloadConsumptionFailure(actionId, cut);
        return cut;
    }

    private bool BindLoadedReturnOnlyContinuation()
    {
        var evidence = _pendingRemoteResumeEvidence;
        if (evidence?.ReturnOnlyContinuation != true) return true;
        var actionId = _pendingRemoteResumeActionId;
        var j = _flightCheckpoints.VoyagerJourney; var task = j?.ReturnOnlyTask; var run = task?.CurrentContinuation;
        var e = new NativeResumeDecisionEvidence("return_only_loaded_session_binding", GameMain.gameTick, DateTimeOffset.UtcNow, true);
        e.Number("loaded_tick", GameMain.gameTick); e.Number("source_tick", evidence.SourceGameTick);
        bool Fail(string? detail = null) { _returnOnlySessionBindingFailure ??= e.Failure("return_only_loaded_session_binding_unproved", detail); return false; }
        if (!e.Require("owned_session", IsCurrentSessionOwned) || !e.Require("original_pending_action", actionId is not null && run?.ActionId == actionId)
            || !e.Require("current_session_observed", _sessionId is not null)
            || !e.Require("owned_native_world", GameMain.data?.gameName == j!.OwnedSaveName && _ownedSaveName == j.OwnedSaveName && _gameVersion == j.GameVersion)
            || !e.Require("original_remote_planet", GameMain.localPlanet?.id == j!.RemotePlanetId)
            || !e.Require("exact_source_file", run!.SourceFingerprint == evidence.SourceFingerprint && run.SourceGameTick == evidence.SourceGameTick)) return Fail();
        if (run.State == "pending_native_load")
        {
            if (!_flightCheckpoints.TryBindReturnOnlyContinuationSession(actionId!, _sessionId!, GameMain.gameTick, out var rejection))
            { e.Require("durable_session_binding", null); return Fail(rejection); }
            run = _flightCheckpoints.VoyagerJourney!.ReturnOnlyTask!.CurrentContinuation!;
        }
        if (!e.Require("adopted_session_matches", run.State == "adopted" && run.SessionId == _sessionId)) return Fail();
        // A verified menu-to-world Load can replace an earlier world's witness.
        // Never reuse its controller proof or replace a witness in this session.
        if (_checkpointReturnAdoption is null
            || (!ReferenceEquals(_checkpointReturnAdoption.World, GameMain.data)
                && _checkpointReturnAdoption.SessionId != _sessionId))
            _checkpointReturnAdoption = new()
            {
                World = GameMain.data!,
                SessionId = _sessionId!,
                ActionId = actionId!,
                OwnedSaveName = j.OwnedSaveName,
                NativeVersion = j.GameVersion,
                CheckpointId = task!.CheckpointId,
                CheckpointFingerprint = task.CheckpointFingerprint,
                ReturnContinuation = true,
                SavedSourceGameTick = evidence.SourceGameTick,
                SourceFingerprint = evidence.SourceFingerprint
            };
        if (!e.Require("current_world_adoption_witness", _checkpointReturnAdoption.ActionId == actionId
            && _checkpointReturnAdoption.SessionId == _sessionId
            && ReferenceEquals(_checkpointReturnAdoption.World, GameMain.data))) return Fail();
        return true;
    }

    private string? CompleteReturnOnlyContinuationAdmission(VoyagerRemoteResumeEvidence evidence)
    {
        if (!evidence.ReturnOnlyContinuation) return null;
        var actionId = _remoteResumeAdoption?.ResumeActionId ?? _pendingRemoteResumeActionId;
        if (actionId is null || !_flightCheckpoints.TryFinishReturnOnlyContinuation(actionId, true, null,
            GameMain.gameTick, out _)) return "return_only_native_admission_persistence_unproved";
        FinishCheckpointReturnHandoff(actionId, null); return null;
    }

    private void RetainFailedReturnOnlyContinuation(string code)
    {
        if (_pendingRemoteResumeEvidence?.ReturnOnlyContinuation != true) return;
        var actionId = _remoteResumeAdoption?.ResumeActionId ?? _pendingRemoteResumeActionId;
        if (actionId is null) return;
        var failure = _remoteResumeAdoption?.FirstFailure ?? _returnOnlySessionBindingFailure
            ?? NativeResumeDecisionEvidence.UnobservedFailure(code, "return_only_native_admission", null, DateTimeOffset.UtcNow, code);
        _flightCheckpoints.TryFinishReturnOnlyContinuation(actionId, false, failure,
            IsCurrentSessionOwned ? GameMain.gameTick : null, out _);
        FinishCheckpointReturnHandoff(actionId, failure);
    }

    private void ReleaseFailedReturnOnlyEvidenceForPreservation()
    {
        if (_pendingRemoteResumeEvidence is not { ReturnOnlyContinuation: true } evidence
            || _checkpointReturnAdoption is not { ReturnContinuation: true, Finished: true } witness
            || !IsCurrentSessionOwned || !GameMain.isPaused || _pendingJournalResumeTicket is not null
            || !ReferenceEquals(witness.World, GameMain.data) || witness.SessionId != _sessionId) return;
        ReleaseResumeSourceLease(); evidence.Dispose(); _pendingRemoteResumeEvidence = null;
    }
}
