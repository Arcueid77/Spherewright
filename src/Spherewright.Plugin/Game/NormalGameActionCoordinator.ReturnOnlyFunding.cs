using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private string? ReturnOnlyNativeFundingBlocker(VoyagerRemoteResumeAdoption adoption, VoyagerJourneyRecord j, double perTick)
    {
        var t = j.ReturnOnlyTask; var p = adoption.Player; var now = DateTimeOffset.UtcNow;
        var f = new VoyagerLocalActionBudgetFacts
        {
            ExactRemoteBinding = t is not null && t.CurrentActionId == adoption.ResumeActionId && t.CurrentSessionId == adoption.SessionId
                && _sessions.SessionId == adoption.SessionId && _sessions.IsCurrentSessionOwned
                && ReferenceEquals(GameMain.data, adoption.World) && ReferenceEquals(GameMain.mainPlayer, p),
            NativePlayerIdle = false,
            CurrentGameTick = GameMain.gameTick,
            LastLandedGameTick = j.LastLandedGameTick,
            SpentGameTicks = j.SpentGameTicks,
            MaximumGameTicks = j.MaximumGameTicks,
            ReturnReserveGameTicks = j.ReturnReserveGameTicks,
            WorkGameTicks = 1,
            TailGameTicks = VoyagerLocalWalkReservePolicy.StopGameTicks,
            NowUtc = now,
            JourneyDeadlineUtc = _sessions.EffectiveVoyagerDeadline(j),
            AvailableLoadedEnergyJoules = CalculateAvailableFlightEnergy(p.mecha),
            LockedReturnFloorJoules = j.ReturnEnergyFloorJoules,
            WorkPowerUpperWatts = perTick * 60,
            TailAndNextTickEnergyJoules = perTick * VoyagerLocalWalkReservePolicy.StopGameTicks,
            NativeWarperCount = CountVoyagerWarpers(p.mecha.warpStorage),
            OriginalExtraWarperReserve = j.ExtraWarperReserve,
        };
        var budget = new VoyagerReturnOnlyIdleBudgetFacts
        {
            ExactTaskBinding = f.ExactRemoteBinding,
            StartedAtGameTick = t?.StartedAtGameTick ?? -1,
            MaximumGameTicks = t?.MaximumGameTicks ?? -1,
            DeadlineUtc = t?.CurrentDeadlineUtc ?? default
        };
        var failure = VoyagerReturnOnlyIdleBudget.BootstrapFailure(budget, f, p.mecha.coreEnergy, perTick, NativeResumeContactPolicy.FundedTicks);
        if (failure is not null) adoption.RetainFailure(failure);
        return failure?.Code;
    }
}
