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
        var environment = RemoteResumeChargeEnvironmentBlocker(p, journey.JourneyId, adoption.CurrentDecisionEvidence);
        if (environment is not null) return environment;
        if(adoption.Evidence.ReturnOnlyContinuation)
            return ReturnOnlyNativeFundingBlocker(adoption,journey,reserve.PerNativeTickEnergyJoules??double.NaN);
        // No charge-start/envelope mutation, no idle=true surrogate, no grant reset.
        return VoyagerRecoveryWindowPolicy.ChargeBlocker(p.mecha.coreEnergy, p.mecha.coreEnergyCap,
            CalculateAvailableFlightEnergy(p.mecha), journey.ReturnEnergyFloorJoules,
            reserve.PerNativeTickEnergyJoules ?? double.NaN, journey.ReturnCoreEnergyMinimumJoules,
            _sessions.EffectiveVoyagerSpentGameTicks(journey) ?? -1, GameMain.gameTick - journey.LastLandedGameTick!.Value,
            _sessions.EffectiveVoyagerMaximumGameTicks(journey), journey.ReturnReserveGameTicks, 0, authorization.Proposal.MaximumChargeGameTicks,
            0d, authorization.Proposal.MaximumChargeWallSeconds, now, deadline, adoption.CurrentDecisionEvidence);
    }

}
