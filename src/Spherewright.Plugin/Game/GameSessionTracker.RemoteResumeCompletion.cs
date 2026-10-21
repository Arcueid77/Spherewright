using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    // Called only after the real501 guard returned. Kept separate so the
    // production lease transition is exercised with the real evidence Store.
    private bool CompleteRemoteResumeAdmission(string? blocker, VoyagerRemoteResumeEvidence evidence)
    {
        if (ResumeAdoptionError is not null || _remoteResumeAdmitted || evidence.IsDisposed
            || !ReferenceEquals(_pendingRemoteResumeEvidence, evidence)) return false;
        if (blocker == VoyagerRecoveryWindowPolicy.ChargingPending || blocker == NativeResumeContactPolicy.GroundingPending) return false;
        if (blocker is not null) return FailRemoteResumeAdmission(blocker);
        _remoteResumeAdmitted = true;
        // The Journal and first strict idle owner are already proved by the
        // caller. A pending or failed transition must keep every lease held.
        ReleaseResumeSourceLease();
        evidence.Dispose();
        _pendingRemoteResumeEvidence = null;
        return true;
    }
}
