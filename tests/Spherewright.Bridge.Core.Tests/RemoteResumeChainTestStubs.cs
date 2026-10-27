using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

// Only the Unity/session shell is substituted. Contact transition, fact factory,
// policies, persistence/lease Store and completion method are linked production.
internal sealed class VoyagerRemoteResumeAdoption
{
    internal PausedSaveTestWorld? World;
    internal string SessionId = "";
    internal string ResumeActionId = "";
    public NativeFirstFrameAdmission FirstFrame { get; } = new();
    public bool NativeContactPending { get; set; } = true;
    public NativeResumeFailureSnapshot? FirstFailure { get; private set; }
    public void RetainFailure(NativeResumeFailureSnapshot? evidence)
    { if (FirstFailure is null && evidence is not null) FirstFailure = evidence.Copy(); }
}

internal sealed partial class NormalGameActionCoordinator
{
    internal static string? AdvanceContactForTest(VoyagerRemoteResumeAdoption adoption,
        NativeResumeContactPolicy policy, NativeResumeContactFacts facts, bool atPhysics)
        => AdvanceRemoteResumeContact(adoption, policy, facts, atPhysics);
}

internal sealed partial class GameSessionTracker
{
    private bool _remoteResumeAdmitted;
    private VoyagerRemoteResumeEvidence? _pendingRemoteResumeEvidence;
    private string? _pendingRemoteResumeActionId;
    private bool _remoteResumeLoadCompleteObserved,_remoteResumeJournalConfirmed;
    private PausedSaveTestWorld? _remoteResumeCompletedWorld;
    internal void BindFailedPrimaryExitForTest(string? fault=null)
    {
        _pendingRemoteResumeEvidence=RemoteEvidence;_remoteResumeAdmitted=false;
        _sessionId=Snapshot.SessionId;_ownedSaveName=Snapshot.SaveName;IsCurrentSessionOwned=true;
        _ownedData=GameMain.data=new(){gameName=_ownedSaveName!};_remoteResumeCompletedWorld=GameMain.data;
        _remoteResumeAdoption=new(){World=GameMain.data,SessionId=_sessionId!,ResumeActionId=RemoteResumeActionId!};
        _remoteResumeAdoption.RetainFailure(new(){Code="synthetic_failed_contact",FirstFailedPredicate="actual_ground",Stage="native_universe_post151"});
        _remoteResumeLoadCompleteObserved=_remoteResumeJournalConfirmed=true;_resumeAdoptionError="synthetic_failed_contact";
        _pendingJournalResumeTicket=null;GameMain.isPaused=true;GameMain.gameTick=RemoteEvidence!.SourceGameTick+1;
        _writeHealth=Spherewright.Contracts.Sessions.WriteHealthStates.Quarantined;_writeQuarantineActionId=RemoteResumeActionId;
        switch(fault)
        {
            case "loading":_remoteResumeLoadCompleteObserved=false;break;
            case "journal":_pendingJournalResumeTicket=new();break;
            case "unpaused":GameMain.isPaused=false;break;
            case "other_world":_remoteResumeCompletedWorld=new();break;
            case "other_action":_writeQuarantineActionId="synthetic-other-action";break;
        }
    }
    internal void ReleaseFailedPrimaryExitForTest()=>ReleaseFailedRemotePrimaryEvidenceForExit();
    internal bool FailedExitStillQuarantinedForTest=>_writeHealth==Spherewright.Contracts.Sessions.WriteHealthStates.Quarantined
        && _resumeAdoptionError is not null && !_remoteResumeAdmitted && _remoteResumeAdoption?.FirstFailure is not null;
    internal bool CompleteContactForTest(string? blocker)
    {
        _pendingRemoteResumeEvidence ??= RemoteEvidence;
        return CompleteRemoteResumeAdmission(blocker, _pendingRemoteResumeEvidence!);
    }
    internal bool ContactAdmittedForTest => _remoteResumeAdmitted;
    internal void ResetContactForExpectedWorld() { _remoteResumeAdmitted = false; _pendingRemoteResumeEvidence = null; }
    private bool FailRemoteResumeAdmission(string code)
    {
        // Native Pause/ownership checks are shell substitutes. The new role's
        // failure persistence, witness completion and lease release are real.
        if(_pendingRemoteResumeEvidence?.ReturnOnlyContinuation==true)
        {
            RetainFailedReturnOnlyContinuation(code);
            GameMain.Pause();QuarantineWritesOnMainThread(_pendingRemoteResumeActionId!,code);
            ReleaseFailedReturnOnlyEvidenceForPreservation();
        }
        ResumeAdoptionError ??= code;return false;
    }
    private void ReleaseResumeSourceLease()
    {
        SourceLease?.Dispose(); SourceLease = null;
        JournalLease?.Dispose(); JournalLease = null;
        PrimaryLease?.Dispose(); PrimaryLease = null;
    }
}
