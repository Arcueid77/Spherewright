using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameCallResult<PreparedNormalAction> PrepareEnableMechaConstructionOnMainThread(
        string? sessionId, PrepareConfigureBuildingRequest request, SessionState session)
    {
        var inspected = _reader.GetPlayerStateOnMainThread(sessionId, new LocalPlanetRequest { PlanetId = request.PlanetId });
        if (!inspected.Success || inspected.Value is null)
            return GameCallResult<PreparedNormalAction>.Failed(inspected.Error!);
        var reason = MechaConstructionDronePolicy.ValidateEnable(request, inspected.Value);
        if (reason is not null) return StalePlan(reason);
        var payload = NormalActionPlanPayload.Configure(sessionId!, request.PlanetId,
            CanonicalStateHash.Combine("enable-mecha-construction-drones-v1", sessionId, request.PlanetId, inspected.Value.StateHash),
            inspected.Value.StateHash, 0, 0, BuildingConfigurationModes.EnableMechaConstructionDrones, 0, 0, string.Empty, -1, -1, -1, 0, 0, 0, "none", "none", 0);
        var result = AddPreparedPlan(payload, session, 1,
            "Calls the ordinary native ToggleConstructionDrones once from OFF to ON. Native construction/repair target search may schedule existing work; no prebuild is created or instantly constructed.");
        if (result.Success && result.Value is not null) result.Value.PlannedMechaConstructionDronesEnabled = true;
        return result;
    }

    private BridgeError? RevalidateEnableMechaConstructionOnMainThread(NormalActionPlanPayload plan)
    {
        var inspected = _reader.GetPlayerStateOnMainThread(plan.SessionId, new LocalPlanetRequest { PlanetId = plan.PlanetId });
        if (!inspected.Success || inspected.Value is null) return inspected.Error;
        var request = new PrepareConfigureBuildingRequest { PlanetId = plan.PlanetId, EntityId = 0, RecipeId = 0,
            Mode = BuildingConfigurationModes.EnableMechaConstructionDrones, ExpectedFactoryStateHash = plan.FactoryStateHash };
        var reason = MechaConstructionDronePolicy.ValidateEnable(request, inspected.Value);
        var remote=_flightCheckpoints.VoyagerJourney is {} journey && !IsVoyagerJourneyClosed(journey);
        return reason is null && (!remote || RemoteMechaDroneEnableHasNoTargets(plan))
            && GameMain.mainPlayer?.currentOrder is null && GameMain.localPlanet?.factory is not null
            ? null : Stale(reason ?? "Mecha controller or current local factory changed.");
    }

    private void ExecuteEnableMechaConstructionOnMainThread(ActionRecord action)
    {
        if (RevalidateEnableMechaConstructionOnMainThread(action.Plan) is { } error)
            throw new InvalidOperationException(error.Message);
        var player = GameMain.mainPlayer;
        var module = player.mecha.constructionModule;
        // Same ordinary command as the native mecha construction-drone switch.
        // It searches eligible existing targets; normal game ticks own completion.
        module.ToggleConstructionDrones(GameMain.localPlanet.factory, player);
        if (!module.droneEnabled || !module.droneConstructEnabled
            || !ReferenceEquals(module, GameMain.mainPlayer.mecha.constructionModule))
            throw new InvalidOperationException("Native construction-drone enable readback did not match.");
        var readback = _reader.GetPlayerStateOnMainThread(action.SessionId, new LocalPlanetRequest { PlanetId = action.PlanetId });
        if (!readback.Success || readback.Value is null || !readback.Value.ConstructionDrones.Enabled)
            throw new InvalidOperationException("Enabled mecha construction drones could not be read back.");
        action.AfterStateHash = readback.Value.StateHash;
        Complete(action, "The native mecha construction-drone master switch changed from OFF to ON once; existing eligible work is scheduled normally, without another Create or instant completion.");
    }

    private bool RemoteMechaDroneEnableHasNoTargets(NormalActionPlanPayload plan)
    {
        if(plan.ConfigureMode!=BuildingConfigurationModes.EnableMechaConstructionDrones || plan.EntityId!=0
            || _flightCheckpoints.VoyagerJourney is not {} j || j.Phase!="remote_landed"
            || !VoyagerOwnedJourneyMatchesCurrentSession(j,plan.SessionId))return false;
        var p=GameMain.mainPlayer;var f=GameMain.localPlanet?.factory;
        if(p?.mecha?.constructionModule is not {} m || f is null || !ConstructionPlayerIdle(p)
            || m.droneEnabled || m.buildTargetTotalCount!=0 || m.repairTargetTotalCount!=0 || m.autoReconstructTargetTotalCount!=0
            || m.buildTargets is {} bt && (bt.Length>256 || bt.Any(t=>t.objectId!=0))
            || m.repairTargets is {} rt && (rt.Length>256 || rt.Any(t=>t.objectId!=0))
            || m.autoReconstructTargets is {} at && (at.Length>256 || at.Any(t=>t.objectId!=0))
            || f.prebuildCount!=0 || f.prebuildCursor<1 || f.prebuildCursor>131072 || f.prebuildPool is null
            || f.prebuildCursor>f.prebuildPool.Length || Enumerable.Range(1,f.prebuildCursor-1).Any(i=>f.prebuildPool[i].id!=0)
            || !TryObserveOwnedConstructionDrones(f,m,out var idle,out _) || !idle || !_blueprints.TryRead(out var builds))return false;
        var snapshot=_reader.GetPlayerStateOnMainThread(plan.SessionId,new LocalPlanetRequest {PlanetId=plan.PlanetId});
        return snapshot.Success && snapshot.Value is not null
            && MechaConstructionDronePolicy.RemoteEmptyTargetsProved(snapshot.Value.ConstructionDrones)
            && SavedFactorySiteMatches(p,f,j.JourneyId,builds,null);
    }
}
