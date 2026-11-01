using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameCallResult<PreparedNormalAction> PrepareEjectorOrbitOnMainThread(
        string? sessionId, PrepareConfigureBuildingRequest request, SessionState session)
    {
        var inspected = _reader.InspectFactoryEntityOnMainThread(sessionId,
            new InspectFactoryEntityRequest { PlanetId = request.PlanetId, ObjectId = request.EntityId });
        if (!inspected.Success || inspected.Value is null)
            return GameCallResult<PreparedNormalAction>.Failed(inspected.Error!);
        var reason = EjectorOrbitConfigurationPolicy.Validate(request, inspected.Value, out var configuration);
        if (reason is not null || configuration is null) return InvalidPlan(reason ?? "ejector_configuration_unavailable");
        if (!EjectorWithinNativeBuildReach(request.EntityId))
            return StalePlan("Ejector configuration requires the exact local native player build reach.");
        var expected = CanonicalStateHash.Combine("ejector-orbit-plan-v1", sessionId, request.PlanetId,
            request.EntityId, configuration.ConfigurationStateHash, configuration.OrbitId,
            configuration.StarId, configuration.OrbitStateHash);
        var payload = NormalActionPlanPayload.EjectorOrbit(sessionId!, request.PlanetId, request.EntityId,
            expected, configuration);
        var prepared = AddPreparedPlan(payload, session, 1,
            "Calls native EjectorComponent.SetOrbit once for this exact local fixed-mode ejector; verifies the target and preserves sail count/inc, automatic selection and boost. This setting call does not manufacture, fill or launch anything, or simulate a tick.");
        if (prepared.Success && prepared.Value is not null) prepared.Value.PlannedEjectorOrbit = configuration;
        return prepared;
    }

    private BridgeError? RevalidateEjectorOrbitOnMainThread(NormalActionPlanPayload plan)
    {
        var inspected = _reader.InspectFactoryEntityOnMainThread(plan.SessionId,
            new InspectFactoryEntityRequest { PlanetId = plan.PlanetId, ObjectId = plan.EntityId });
        if (!inspected.Success || inspected.Value is null) return inspected.Error ?? Stale("Ejector disappeared.");
        var request = new PrepareConfigureBuildingRequest { Mode = BuildingConfigurationModes.EjectorOrbit,
            PlanetId = plan.PlanetId, EntityId = plan.EntityId, EjectorOrbitId = plan.ConfigureEjectorOrbitId,
            ExpectedFactoryStateHash = plan.FactoryStateHash };
        var reason = EjectorOrbitConfigurationPolicy.Validate(request, inspected.Value, out var current);
        return reason is null && current is not null && current.StarId == plan.ConfigureEjectorStarId
            && current.OrbitStateHash == plan.ConfigureEjectorOrbitStateHash
            && EjectorWithinNativeBuildReach(plan.EntityId)
                ? null : Stale(reason ?? "The native ejector target orbit, identity or player reach changed.");
    }

    private static bool EjectorWithinNativeBuildReach(int entityId)
    {
        var factory = GameMain.localPlanet?.factory;
        var player = GameMain.mainPlayer;
        if (factory is null || player?.mecha is null || entityId <= 0
            || entityId >= factory.entityCursor || entityId >= factory.entityPool.Length) return false;
        var distance = (factory.entityPool[entityId].pos - player.position).magnitude;
        return !float.IsNaN(distance) && !float.IsInfinity(distance)
            && !float.IsNaN(player.mecha.buildArea) && !float.IsInfinity(player.mecha.buildArea)
            && player.mecha.buildArea > 0 && distance <= player.mecha.buildArea;
    }

    private void ApplyEjectorOrbitOnMainThread(NormalActionPlanPayload plan)
    {
        if (RevalidateEjectorOrbitOnMainThread(plan) is { } error) throw new InvalidOperationException(error.Message);
        var factory = GameMain.localPlanet!.factory;
        ref var native = ref factory.factorySystem.ejectorPool[factory.entityPool[plan.EntityId].ejectorId];
        var before = native;
        // Same ordinary business method as UIEjectorWindow.OnSetOrbit. Native
        // may cool an existing charge; never overwrite its time/direction.
        native.SetOrbit(plan.ConfigureEjectorOrbitId);
        if (native.orbitId != plan.ConfigureEjectorOrbitId || native.runtimeOrbitId != plan.ConfigureEjectorOrbitId
            || native.id != before.id || native.entityId != before.entityId || native.planetId != before.planetId
            || native.bulletId != before.bulletId || native.bulletCount != before.bulletCount
            || native.bulletInc != before.bulletInc || native.autoOrbit != before.autoOrbit
            || native.boost != before.boost || native.pcId != before.pcId)
            throw new InvalidOperationException("Native orbit setting did not preserve exact ejector identity and stock.");
    }

    private static bool IsEjectorOrbitApplied(FactoryEntitySnapshot entity, NormalActionPlanPayload plan) =>
        entity.ItemId == 2311 && entity.Ejector is { NativeBindingVerified: true } state
        && state.StarId == plan.ConfigureEjectorStarId && state.OrbitId == plan.ConfigureEjectorOrbitId
        && state.RuntimeOrbitId == plan.ConfigureEjectorOrbitId && !state.AutomaticOrbitSelection;
}
