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
        // The Journal and first strict idle owner are already proved by the
        // caller. A pending or failed transition must keep every lease held.
        ReleaseResumeSourceLease();
        evidence.Dispose();
        if(CompleteReturnOnlyContinuationAdmission(evidence) is { } taskPersistenceFailure)
            return FailRemoteResumeAdmission(taskPersistenceFailure);
        if (PersistRemoteResumeProgressDebit(evidence) is { } persistenceFailure)
        {
            RetainRemotePrimaryDebitFailure(persistenceFailure, evidence);
            return FailRemoteResumeAdmission(persistenceFailure);
        }
        _remoteResumeAdmitted = true;
        _pendingRemoteResumeEvidence = null;
        return true;
    }
}
