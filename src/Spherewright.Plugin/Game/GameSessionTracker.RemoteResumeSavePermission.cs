using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    private bool RemoteResumeAllowsPrimaryResave()
    {
        if (_remoteResumeAdoption is not null && (_resumeAdoptionError is not null || _remoteResumeAdoption.FirstFrame.Failure is not null))
            return false;
        if (HasPendingRemoteResumeAdoption) return false;
        if (_remoteResumeAdoption is null || _ownedSaveState != OwnedSaveStates.WaitingToSave) return true;
        if (!_remoteResumeAdmitted || !IsCurrentSessionOwned || !ReferenceEquals(GameMain.data, _remoteResumeAdoption.World)
            || _sessionId != _remoteResumeAdoption.SessionId)
            return FailRemoteResumeAdmission("remote_resume_primary_resave_binding_unproved");
        string? blocker;
        try { blocker = _remoteResumeAdmissionGuard is null ? "remote_resume_idle_guard_unavailable" : _remoteResumeAdmissionGuard(_remoteResumeAdoption); }
        catch (Exception exception) { blocker = "remote_resume_resave_capture_failed:" + exception.GetType().Name; }
        return blocker is null || FailRemoteResumeAdmission(blocker);
    }
}
