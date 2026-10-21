using Spherewright.Bridge.Core.Safety;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    // Shared by the real151/501 callbacks. No capture or Native invocation is
    // hidden in this transition; a151 can never retire contact or publish idle.
    private static string? AdvanceRemoteResumeContact(VoyagerRemoteResumeAdoption adoption,
        NativeResumeContactPolicy policy, NativeResumeContactFacts facts, bool atPhysics)
    {
        if (adoption.FirstFrame.Failure is not null) return adoption.FirstFrame.Failure;
        if (!atPhysics)
        {
            var failure = policy.ObserveUniverse(facts);
            if (failure is null) return null;
            adoption.RetainFailure(policy.FailureEvidence);
            return adoption.FirstFrame.Fail(failure, policy.FailureDetail);
        }
        var step = policy.Observe(facts);
        if (step == NativeResumeContactStep.Ready) { adoption.NativeContactPending = false; return null; }
        if (step == NativeResumeContactStep.Pending) return NativeResumeContactPolicy.GroundingPending;
        adoption.RetainFailure(policy.FailureEvidence);
        return adoption.FirstFrame.Fail(policy.Failure ?? "remote_resume_contact_attempt_retired", policy.FailureDetail);
    }
}
