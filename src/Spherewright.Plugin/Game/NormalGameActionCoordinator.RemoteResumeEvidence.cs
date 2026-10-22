using Spherewright.Bridge.Core.Safety;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private static NativeResumeDecisionEvidence BeginRemoteResumeDecision(VoyagerRemoteResumeAdoption adoption, string stage)
    {
        var e = new NativeResumeDecisionEvidence(stage, GameMain.gameTick, DateTimeOffset.UtcNow, true);
        // Plain field copies at decision entry. No Physics query, loaded-energy
        // calculation, clock re-capture after failure, or actor-state write.
        var p = adoption.Player;
        e.Number("local_x", p.position.x); e.Number("local_y", p.position.y); e.Number("local_z", p.position.z);
        e.Number("universe_x", p.uPosition.x); e.Number("universe_y", p.uPosition.y); e.Number("universe_z", p.uPosition.z);
        e.Number("core_j", p.mecha.coreEnergy); e.Number("reactor_j", p.mecha.reactorEnergy);
        e.Number("speed_mps", p.speed); e.Number("vertical_mps", p.controller.vertSpeed);
        e.Observe("native_paused", GameMain.isPaused);
        if (adoption.Evidence.RemotePrimaryRecovery)
        {
            e.Number("original_spent_ticks", adoption.Evidence.Journey.SpentGameTicks);
            e.Number("discarded_progress_debit_ticks", adoption.Evidence.KnownPrimaryProgressTick is { } floor
                ? Math.Max(0, floor - adoption.Evidence.SourceGameTick) : (long?)null);
        }
        e.NotReached("native_capture", "contact", "charge", "strict_idle", "healthy_takeover");
        adoption.CurrentDecisionEvidence = e;
        return e;
    }

    private static string? RetainRemoteResumeDecision(VoyagerRemoteResumeAdoption adoption,
        NativeResumeDecisionEvidence evidence, string? code)
    {
        if (code is null || code == NativeResumeContactPolicy.GroundingPending
            || code == NativeResumeInitializationPolicy.PendingCode || code == VoyagerRecoveryWindowPolicy.ChargingPending) return code;
        // A nested predicate may already have latched more precise copied facts.
        // Cancellation, Pause and later observers cannot replace that first cut.
        adoption.RetainFailure(evidence.Failure(code.Split(':')[0], adoption.FirstFrame.FailureDetail ?? code));
        return code;
    }
}
