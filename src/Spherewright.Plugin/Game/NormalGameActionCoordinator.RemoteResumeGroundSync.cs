using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.RuntimeDescriptor;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private string? LoadedGroundSyncFunding(VoyagerRemoteResumeAdoption adoption, VoyagerJourneyRecord journey,
        VoyagerRemoteIdleAdmissionFacts facts, VoyagerRecoveryAuthorization authorization)
    {
        // This is only first-frame funding, not idle admission. The contact
        // query failed, so this projection must retain NativePlayerIdle=false.
        var f = facts.JourneyBudget; var p = adoption.Player; var now = DateTimeOffset.UtcNow;
        var deadline = _sessions.EffectiveVoyagerDeadline(journey);
        if (f is null || !f.ExactRemoteBinding || f.NativePlayerIdle || !f.LastLandedGameTick.HasValue
            || f.CurrentGameTick != adoption.Evidence.SourceGameTick || f.CurrentGameTick != GameMain.gameTick
            || f.CurrentGameTick < f.LastLandedGameTick || f.SpentGameTicks < 0
            || f.MaximumGameTicks < 1 || f.ReturnReserveGameTicks < 3600 || f.ReturnReserveGameTicks > 108000
            || f.WorkGameTicks != 1 || f.TailGameTicks != VoyagerLocalWalkReservePolicy.StopGameTicks
            || f.OriginalExtraWarperReserve < 0 || f.OriginalExtraWarperReserve > 100
            || f.NowUtc == default || now < f.NowUtc || now - f.NowUtc > TimeSpan.FromSeconds(2)
            || f.JourneyDeadlineUtc != deadline || adoption.ChargeStartedGameTick.HasValue || adoption.ChargeStartedUtc.HasValue
            || facts.MovementReserve is not { Proved: true } reserve)
            return "remote_resume_ground_sync_original_budget_unproved";
        if (!f.NativeWarperCount.HasValue || f.NativeWarperCount < 1 + f.OriginalExtraWarperReserve)
            return "remote_resume_charge_original_warper_reserve_insufficient";
        var environment = RemoteResumeChargeEnvironmentBlocker(p);
        if (environment is not null) return environment;
        // No charge-start/envelope mutation, no idle=true surrogate, no grant reset.
        return VoyagerRecoveryWindowPolicy.ChargeBlocker(p.mecha.coreEnergy, p.mecha.coreEnergyCap,
            CalculateAvailableFlightEnergy(p.mecha), journey.ReturnEnergyFloorJoules,
            reserve.PerNativeTickEnergyJoules ?? double.NaN, journey.ReturnCoreEnergyMinimumJoules,
            journey.SpentGameTicks, GameMain.gameTick - journey.LastLandedGameTick!.Value,
            journey.MaximumGameTicks, journey.ReturnReserveGameTicks, 0, authorization.Proposal.MaximumChargeGameTicks,
            0d, authorization.Proposal.MaximumChargeWallSeconds, now, deadline);
    }

    private static string? RemoteResumeChargeEnvironmentBlocker(Player p)
    {
        var h = GameMain.history; var factory = p.factory; var module = p.mecha.constructionModule;
        if (p.controller.actionWalk.rtsVelocity.sqrMagnitude != 0f || p.controller.actionWalk.moveVelocity.sqrMagnitude != 0f
            || p.controller.actionDrift.rtsVelocity.sqrMagnitude != 0f || p.controller.actionDrift.moveVelocity.sqrMagnitude != 0f)
            return "remote_resume_charge_rts_or_manual_velocity_nonzero";
        if (h is null || h.currentTech != 0 || h.techQueue is null || h.techQueue.Length != 32 || h.techQueue.Any(id => id != 0))
            return "remote_resume_charge_global_research_active";
        if (factory is null || factory.entityCount != 0 || factory.entityCursor > 1
            || factory.prebuildCount != 0 || factory.prebuildCursor > 1
            || module.buildTargetTotalCount != 0 || module.repairTargetTotalCount != 0 || module.autoReconstructTargetTotalCount != 0
            || module.buildTargets is { } bt && (bt.Length > 256 || bt.Any(t => t.objectId != 0))
            || module.repairTargets is { } rt && (rt.Length > 256 || rt.Any(t => t.objectId != 0))
            || module.autoReconstructTargets is { } at && (at.Length > 256 || at.Any(t => t.objectId != 0)))
            return "remote_resume_charge_native_construction_not_empty";
        return null;
    }

}
