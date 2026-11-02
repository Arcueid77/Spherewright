using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    internal bool TryOpenFailedLandingSource(OwnedWorldResumeTicketStore credentials, string token,
        PrepareOwnedWorldResumeRequest request, out VoyagerFailedLandingSource? source, out NativeResumeFailureSnapshot? failure)
        => _flightCheckpoints.TryOpenFailedLandingSource(credentials, token, request, out source, out failure);

    internal bool RegisterFailedLandingAdmission(VoyagerFailedLandingSource source, string actionId,
        bool confirmed, string digest, DateTimeOffset now, out string rejection)
        => _flightCheckpoints.TryRegisterFailedLandingAdmission(source, actionId, confirmed, digest, now, out rejection);

    internal string FailedLandingAdmissionJourneyJson() => PluginJson.Serialize(_flightCheckpoints.VoyagerJourney);

    internal NativeResumeFailureSnapshot RecordFailedLandingPreloadConsumptionFailure(string actionId)
    {
        var failure = NativeResumeDecisionEvidence.UnobservedFailure("remote_resume_durable_credential_consumption_failed",
            "failed_landing_preload_consumption", null, DateTimeOffset.UtcNow, "durable_credential_consumption");
        _flightCheckpoints.TryFinishFailedLandingAdmission(actionId, false, failure, null, out _);
        return failure;
    }

    private int ExpectedResumePlanet(OwnedWorldResumeTicket ticket)
    {
        var source = _pendingRemoteResumeEvidence?.FailedLandingSource;
        return source is { IsDisposed: false } && source.Ticket.ResumeToken == ticket.ResumeToken
            && source.Ticket.SourceSessionId == ticket.SourceSessionId && source.Ticket.OwnedSaveName == ticket.OwnedSaveName
            && source.Ticket.GameVersion == ticket.GameVersion && source.Journey.RemotePlanetId is { } remote
                ? remote : ticket.ExpectedPlanetId;
    }

    private bool BindLoadedFailedLandingAdmission()
    {
        if (_pendingRemoteResumeEvidence is not { FailedLandingRecovery: true } evidence) return true;
        var j = _flightCheckpoints.VoyagerJourney; var run = j?.FailedLandingAdmissions.LastOrDefault();
        if (j is null || run is null || run.ActionId != _pendingRemoteResumeActionId || !IsCurrentSessionOwned
            || string.IsNullOrEmpty(_sessionId) || j.OwnedSaveName != _ownedSaveName || j.GameVersion != _gameVersion
            || GameMain.data?.gameName != j.OwnedSaveName || GameMain.localPlanet?.id != j.RemotePlanetId
            || GameMain.gameTick < evidence.SourceGameTick || run.SourceFingerprint != evidence.SourceFingerprint
            || run.SourceGameTick != evidence.SourceGameTick) return false;
        if (run.State == "pending_native_load")
            return _flightCheckpoints.TryBindFailedLandingAdmissionSession(run.ActionId, _sessionId!, GameMain.gameTick, out _);
        return run.State == "adopted" && run.SessionId == _sessionId;
    }

    private string? CompleteFailedLandingAdmission(VoyagerRemoteResumeEvidence evidence)
    {
        if (!evidence.FailedLandingRecovery) return null;
        if (_remoteResumeAdoption is not { } adoption || adoption.FirstFailure is not null
            || !IsCurrentSessionOwned || _sessionId != adoption.SessionId || !ReferenceEquals(GameMain.data, adoption.World)
            || !_flightCheckpoints.TryFinishFailedLandingAdmission(adoption.ResumeActionId, true, null, GameMain.gameTick, out _))
            return "failed_landing_healthy_admission_persistence_unproved";
        return null;
    }

    private void PreserveFailedLandingAdmission(string code)
    {
        if (_pendingRemoteResumeEvidence is not { FailedLandingRecovery: true } evidence
            || _remoteResumeAdoption is not { } adoption || adoption.FirstFailure is null
            || !_remoteResumeLoadCompleteObserved || !_remoteResumeJournalConfirmed || _pendingJournalResumeTicket is not null
            || !IsCurrentSessionOwned || !GameMain.isPaused || _sessionId != adoption.SessionId
            || !ReferenceEquals(_remoteResumeCompletedWorld, GameMain.data) || !ReferenceEquals(adoption.World, GameMain.data)
            || !ReferenceEquals(_ownedData, GameMain.data) || GameMain.gameTick < evidence.SourceGameTick) return;
        var tick = GameMain.gameTick; var failure = adoption.FirstFailure.Copy();
        ReleaseResumeSourceLease(); evidence.Dispose();
        _flightCheckpoints.TryFinishFailedLandingAdmission(adoption.ResumeActionId, false, failure, tick, out _);
        _pendingRemoteResumeEvidence = null;
    }
}
