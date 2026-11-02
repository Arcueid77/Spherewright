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
        return reason is null && GameMain.mainPlayer?.currentOrder is null && GameMain.localPlanet?.factory is not null
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
}
