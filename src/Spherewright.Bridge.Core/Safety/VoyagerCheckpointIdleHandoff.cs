using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Safety;

// Provenance adoption is not a Native idle certificate. Only the production
// post501 guard can complete this pending handoff. No Native call is hidden here.
public sealed class VoyagerCheckpointIdleHandoff
{
    public VoyagerCheckpointIdleHandoff(string actionId, long startedTick, DateTimeOffset startedUtc)
    { ActionId = actionId; StartedTick = LastObservedTick = startedTick; StartedUtc = startedUtc; }
    public string ActionId { get; }
    public long StartedTick { get; }
    public DateTimeOffset StartedUtc { get; }
    public long LastObservedTick { get; private set; }
    public bool Admitted { get; private set; }
    public long? AdmittedAtGameTick { get; private set; }
    private NativeResumeFailureSnapshot? _failure;
    public NativeResumeFailureSnapshot? FirstFailure => _failure?.Copy();

    public void ObservePostPhysics(long tick, DateTimeOffset now, bool? exactBinding,
        VoyagerRemoteIdleAdmissionDecision decision, NativeResumeDecisionEvidence evidence)
    {
        if (_failure is not null) return;
        evidence.NotReached("exact_checkpoint_idle_binding", "native501_sequence", "strict_idle", "healthy_takeover");
        if (!evidence.Require("exact_checkpoint_idle_binding", exactBinding)
            || !evidence.Require("native501_sequence", tick >= LastObservedTick && (decimal)tick - LastObservedTick <= 1
                && now >= StartedUtc && (Admitted || (now - StartedUtc).TotalSeconds <= NativeResumeContactPolicy.MaximumWallSeconds)))
        { Reject(evidence.Failure("return_only_checkpoint_idle_binding_unproved")); return; }
        if (!evidence.Require("strict_idle", decision.Proved))
        { Reject(decision.FailureEvidence ?? evidence.Failure(decision.BlockerCode ?? "return_only_checkpoint_idle_unproved", decision.CaptureStage)); return; }
        evidence.Require("healthy_takeover", true); LastObservedTick = tick; Admitted = true; AdmittedAtGameTick ??= tick;
    }

    public void Reject(NativeResumeFailureSnapshot failure) => _failure ??= failure.Copy();
    public void CheckObservationDeadline(long? tick, DateTimeOffset now)
    {
        if (Admitted || _failure is not null) return;
        if (now < StartedUtc || (now - StartedUtc).TotalSeconds > NativeResumeContactPolicy.MaximumWallSeconds)
            Reject(NativeResumeDecisionEvidence.UnobservedFailure("return_only_checkpoint_native501_not_observed",
                "checkpoint_idle_observer_wait", tick, now, "native501_observed"));
    }
}
