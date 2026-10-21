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
        e.NotReached("native_capture", "contact", "charge", "strict_idle", "healthy_takeover");
        adoption.CurrentDecisionEvidence = e;
        return e;
    }

    private static void RecordRemoteResumeFacts(NativeResumeDecisionEvidence e, VoyagerRemoteIdleAdmissionFacts facts)
    {
        e.Require("native_capture", true);
        e.Observe("movement_reserve_proved", facts.MovementReserve?.Proved);
        e.Number("core_j", facts.CoreEnergyJoules); e.Number("per_tick_j", facts.MovementReserve?.PerNativeTickEnergyJoules);
        var j = facts.JourneyBudget;
        e.Observe("journey_binding", j?.ExactRemoteBinding); e.Observe("native_idle", j?.NativePlayerIdle);
        e.Number("loaded_j", j?.AvailableLoadedEnergyJoules); e.Number("return_floor_j", j?.LockedReturnFloorJoules);
        e.Number("spent_ticks", j?.SpentGameTicks); e.Number("last_landed_tick", j?.LastLandedGameTick);
        e.Number("maximum_ticks", j?.MaximumGameTicks); e.Number("return_ticks", j?.ReturnReserveGameTicks);
        e.Number("warpers", j?.NativeWarperCount);
        e.Number("return_wall_seconds", j is null ? null : (j.JourneyDeadlineUtc - j.NowUtc).TotalSeconds);
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
