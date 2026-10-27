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
        // caller. Pending transitions keep leases; an exact failed paused
        // loaded world has a separate exit-preservation release below.
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

    private void ReleaseFailedRemotePrimaryEvidenceForExit()
    {
        if (_pendingRemoteResumeEvidence is not { RemotePrimaryRecovery:true } evidence
            || _remoteResumeAdoption is not { } adoption || adoption.FirstFailure is null
            || !_remoteResumeLoadCompleteObserved || !_remoteResumeJournalConfirmed
            || _pendingJournalResumeTicket is not null || _resumeAdoptionError is null
            || !IsCurrentSessionOwned || !GameMain.isPaused || _sessionId!=adoption.SessionId
            || !ReferenceEquals(_remoteResumeCompletedWorld,GameMain.data) || !ReferenceEquals(adoption.World,GameMain.data)
            || !ReferenceEquals(_ownedData,GameMain.data) || GameMain.data?.gameName!=_ownedSaveName
            || _writeHealth!=Spherewright.Contracts.Sessions.WriteHealthStates.Quarantined
            || _writeQuarantineActionId!=adoption.ResumeActionId || GameMain.gameTick<evidence.SourceGameTick) return;
        // Import and Journal adoption completed; retained failure/quarantine
        // seal future work. A LastExit input lease would otherwise block the
        // normal Native exit Save to that slot. Release files only: no Resume,
        // ordinary Save permission, idle owner or budget renewal is granted.
        ReleaseResumeSourceLease();evidence.Dispose();_pendingRemoteResumeEvidence=null;
    }
}
