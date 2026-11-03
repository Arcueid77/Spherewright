namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    internal bool ObserveRemoteResumeUniverseOnMainThread()
    {
        if (_remoteResumeAdoption is not null && (_resumeAdoptionError is not null || _remoteResumeAdoption.FirstFrame.Failure is not null))
            return FailRemoteResumeAdmission("remote_resume_failed_adoption_is_terminal");
        if (!HasPendingRemoteResumeAdoption || _remoteResumeAdoption is not { } current) return true;
        // A separately bound surface controller does not arm the legacy
        // single-frame latch. It still needs EVERY real151 before its501.
        if (!current.SurfaceRecoveryPending)
        {
            var frame = current.FirstFrame;
            if (!frame.Armed) return true;
            if (frame.PhysicsObserved && GameMain.gameTick > frame.SourceGameTick + 1 && !current.NativeContactPending) return true;
        }
        var blocker = CaptureRemoteResumeAdoption(out var adoption);
        if (blocker is null)
            blocker = _remoteResumeUniverseGuard is null ? "remote_resume_universe_guard_unavailable" : _remoteResumeUniverseGuard(adoption!);
        return blocker is null || FailRemoteResumeAdmission(blocker);
    }
}
