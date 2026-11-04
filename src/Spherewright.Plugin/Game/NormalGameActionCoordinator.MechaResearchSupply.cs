using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameCallResult<PreparedNormalAction> PrepareMechaResearchSupplyOnMainThread(
        string? sessionId, PrepareConfigureBuildingRequest request, SessionState session)
    {
        var inspected=_reader.GetPlayerStateOnMainThread(sessionId,new LocalPlanetRequest {PlanetId=request.PlanetId});
        if (!inspected.Success || inspected.Value is null) return GameCallResult<PreparedNormalAction>.Failed(inspected.Error!);
        if (MechaResearchSupplyPolicy.Validate(request,inspected.Value) is {} reason) return StalePlan(reason);
        if (_flightCheckpoints.VoyagerJourney is {} j && !IsVoyagerJourneyClosed(j))
            return StalePlan("mecha_research_supply_requires_closed_journey");
        var payload=NormalActionPlanPayload.Configure(sessionId!,request.PlanetId,
            CanonicalStateHash.Combine("mecha-research-auto-supply-v1",sessionId,request.PlanetId,
                inspected.Value.StateHash,inspected.Value.AutoManageResearchItems,MechaResearchSupplyPolicy.Desired(request.Mode)),
            inspected.Value.StateHash,0,0,request.Mode,0,0,string.Empty,-1,-1,-1,0,0,0,"none","none",0);
        if (RevalidateMechaResearchSupplyOnMainThread(payload) is {} nativeError)
            return GameCallResult<PreparedNormalAction>.Failed(nativeError);
        var result=AddPreparedPlan(payload,session,1,
            "Toggles the ordinary native backpack research supply checkbox once, with an empty mecha point buffer. No items, research points, technology, production buffers or elapsed ticks are written. Factory research continues normally.");
        if (result.Success && result.Value is not null)
            result.Value.PlannedMechaResearchAutoSupplyEnabled=MechaResearchSupplyPolicy.Desired(request.Mode);
        return result;
    }

    private BridgeError? RevalidateMechaResearchSupplyOnMainThread(NormalActionPlanPayload plan)
    {
        var inspected=_reader.GetPlayerStateOnMainThread(plan.SessionId,new LocalPlanetRequest {PlanetId=plan.PlanetId});
        if (!inspected.Success || inspected.Value is null) return inspected.Error;
        var request=new PrepareConfigureBuildingRequest {PlanetId=plan.PlanetId,EntityId=0,RecipeId=0,
            Mode=plan.ConfigureMode,ExpectedFactoryStateHash=plan.FactoryStateHash};
        if (MechaResearchSupplyPolicy.Validate(request,inspected.Value) is {} reason) return Stale(reason);
        if (_flightCheckpoints.VoyagerJourney is {} j && !IsVoyagerJourneyClosed(j))
            return Stale("mecha_research_supply_requires_closed_journey");
        var p=GameMain.mainPlayer;
        return p is not null && ConstructionPlayerIdle(p) && GameMain.history is not null
            && p.mecha?.lab?.itemPoints?.items is {} points && points.All(x=>x.Key>0 && x.Value==0)
            && ReferenceEquals(p.mecha.lab.gameHistory,GameMain.history)
            ? null : Stale("mecha_research_supply_native_controller_or_empty_buffer_changed");
    }

    private void ExecuteMechaResearchSupplyOnMainThread(ActionRecord action)
    {
        if (RevalidateMechaResearchSupplyOnMainThread(action.Plan) is {} error) throw new InvalidOperationException(error.Message);
        // Exact UIMechaLab.SwitchAutoManageLabItems setting path. Never invoke
        // ManageTakeback or clear itemPoints: the empty-buffer guard prevents
        // Native floor-and-clear from losing a partial research cube.
        GameMain.history.autoManageLabItems = !GameMain.history.autoManageLabItems;
        var after=_reader.GetPlayerStateOnMainThread(action.SessionId,new LocalPlanetRequest {PlanetId=action.PlanetId});
        if (!after.Success || after.Value is null || after.Value.MechaResearchItemBuffer.Count!=0
            || after.Value.AutoManageResearchItems!=MechaResearchSupplyPolicy.Desired(action.Plan.ConfigureMode))
            throw new InvalidOperationException("Native mecha research supply setting readback did not match.");
        action.AfterStateHash=after.Value.StateHash;
        Complete(action,"The native backpack research auto-supply setting changed once with an empty point buffer. Factory labs remain independent and no research progress or inventory was cleared.");
    }
}
