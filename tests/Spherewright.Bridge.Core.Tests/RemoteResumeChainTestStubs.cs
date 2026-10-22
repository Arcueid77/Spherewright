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
