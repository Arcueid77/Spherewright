using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Factory;

public static class EjectorOrbitConfigurationPolicy
{
    public static string ConfigurationHash(FactoryEntitySnapshot entity, EjectorStateSnapshot state) =>
        CanonicalStateHash.Combine("ejector-orbit-configuration-v1", entity.PlanetId, entity.ObjectId,
            entity.ItemId, state.EjectorComponentId, state.StarId, entity.Position.X, entity.Position.Y, entity.Position.Z,
            entity.Rotation.X, entity.Rotation.Y, entity.Rotation.Z, entity.Rotation.W,
            state.SailItemId, state.OrbitId, state.AutomaticOrbitSelection, state.BoostEnabled);

    public static string OrbitHash(int starId, SwarmOrbitSnapshot orbit) =>
        CanonicalStateHash.Combine("native-swarm-orbit-v1", starId, orbit.OrbitId, orbit.Enabled,
            orbit.Radius, orbit.Rotation.X, orbit.Rotation.Y, orbit.Rotation.Z, orbit.Rotation.W);

    public static string? Validate(PrepareConfigureBuildingRequest request, FactoryEntitySnapshot entity,
        out EjectorOrbitPlanSnapshot? plan)
    {
        plan = null;
        if (request.Mode != BuildingConfigurationModes.EjectorOrbit || request.EjectorOrbitId < 0
            || request.RecipeId != 0 || request.TechId != 0 || request.FilterItemId != 0
            || !string.IsNullOrEmpty(request.StorageOperation) || request.StorageBannedGridCount != -1
            || request.StationStorageIndex != -1 || request.StationBeltSlotIndex != -1
            || request.StationBeltStorageIndex != -1 || request.StationItemId != 0
            || request.StationMaximumCount != 0 || request.StationMaximumChargePowerWatts != 0
            || request.StationLocalLogic != LogisticsStorageLogics.None
            || request.StationRemoteLogic != LogisticsStorageLogics.None
            || !string.IsNullOrEmpty(request.ExpectedStationConfigurationStateHash))
            return "ejector_orbit_requires_exclusive_configuration_parameters";
        var state = entity.Ejector;
        if (entity.ObjectKind != FactoryObjectKinds.Entity || entity.ComponentKind != "ejector"
            || entity.ItemId != 2311 || entity.ObjectId != request.EntityId || entity.PlanetId != request.PlanetId
            || state is null || !state.NativeBindingVerified || state.EjectorComponentId <= 0
            || state.SailItemId != 1501 || state.StarId <= 0
            || string.IsNullOrEmpty(state.ConfigurationStateHash)
            || request.ExpectedFactoryStateHash != state.ConfigurationStateHash
            || state.ConfigurationStateHash != ConfigurationHash(entity, state))
            return "ejector_identity_or_configuration_changed";
        if (state.AutomaticOrbitSelection)
            return "ejector_fixed_orbit_requires_native_automatic_selection_off";
        var orbitHash = CanonicalStateHash.Combine("native-ejector-targeting-disabled-v1", state.StarId);
        if (request.EjectorOrbitId > 0)
        {
            if (!state.SwarmAvailable || !state.OrbitCoverageComplete || state.Orbits is null)
                return "ejector_native_orbit_coverage_unavailable";
            var matches = state.Orbits.Where(o => o is not null && o.OrbitId == request.EjectorOrbitId).ToArray();
            if (matches.Length != 1 || !matches[0].Enabled || !ValidOrbit(matches[0])
                || matches[0].StateHash != OrbitHash(state.StarId, matches[0]))
                return "ejector_target_orbit_missing_disabled_or_changed";
            orbitHash = matches[0].StateHash;
        }
        plan = new EjectorOrbitPlanSnapshot { OrbitId = request.EjectorOrbitId, StarId = state.StarId,
            ConfigurationStateHash = state.ConfigurationStateHash, OrbitStateHash = orbitHash };
        return null;
    }

    public static bool ConfirmsPreparedEcho(PrepareConfigureBuildingRequest request, PreparedNormalAction? result) =>
        request.Mode == BuildingConfigurationModes.EjectorOrbit && request.EjectorOrbitId >= 0
        && result is { Prepared: true, CommitAllowedNow: true, PlannedEjectorOrbit: { } plan }
        && !string.IsNullOrEmpty(result.PlanToken) && result.ActionKind == NormalActionKinds.ConfigureBuilding
        && plan.OrbitId == request.EjectorOrbitId && plan.StarId > 0
        && plan.ConfigurationStateHash == request.ExpectedFactoryStateHash
        && !string.IsNullOrEmpty(plan.OrbitStateHash);

    private static bool ValidOrbit(SwarmOrbitSnapshot orbit) => orbit.Radius > 0
        && !float.IsNaN(orbit.Radius) && !float.IsInfinity(orbit.Radius) && orbit.Rotation is { } r
        && Finite(r.X) && Finite(r.Y) && Finite(r.Z) && Finite(r.W)
        && (double)r.X*r.X+(double)r.Y*r.Y+(double)r.Z*r.Z+(double)r.W*r.W > 0;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
