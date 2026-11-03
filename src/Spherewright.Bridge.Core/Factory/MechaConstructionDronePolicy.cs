using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Factory;

public static class MechaConstructionDronePolicy
{
    // Snapshot half only; the Native coordinator additionally proves the
    // complete paid factory, empty target arrays/pools and physical idle.
    public static bool RemoteEmptyTargetsProved(ConstructionDroneSnapshot d) => !d.Enabled && d.ConstructionEnabled
        && d.Total>0 && d.Alive==d.Total && d.Idle==d.Total && d.Working==0
        && d.PendingBuildTargets==0 && d.PendingRepairTargets==0;
    public static string? BuildReadiness(PlayerStateSnapshot player) =>
        !player.ConstructionDrones.Enabled ? "construction_drone_master_switch_disabled"
        : !player.ConstructionDrones.ConstructionEnabled ? "construction_drone_building_switch_disabled"
        : player.ConstructionDrones.Alive <= 0 ? "no_alive_construction_drone"
        : null;

    public static string? ValidateParameters(PrepareConfigureBuildingRequest request)
    {
        if (request.Mode != BuildingConfigurationModes.EnableMechaConstructionDrones
            || request.EntityId != 0 || request.RecipeId != 0 || request.TechId != 0
            || request.FilterItemId != 0 || request.EjectorOrbitId != -1
            || !string.IsNullOrEmpty(request.StorageOperation) || request.StorageBannedGridCount != -1
            || request.StationStorageIndex != -1 || request.StationBeltSlotIndex != -1
            || request.StationBeltStorageIndex != -1 || request.StationItemId != 0
            || request.StationMaximumCount != 0 || request.StationMaximumChargePowerWatts != 0
            || request.StationLocalLogic != "none" || request.StationRemoteLogic != "none"
            || !string.IsNullOrEmpty(request.ExpectedStationConfigurationStateHash))
            return "mecha_drone_enable_requires_exclusive_parameters";
        return null;
    }

    public static string? ValidateEnable(PrepareConfigureBuildingRequest request, PlayerStateSnapshot player)
    {
        if (ValidateParameters(request) is { } error) return error;
        if (request.StateHashVersion != CanonicalStateHash.Version || player.StateHashVersion != CanonicalStateHash.Version
            || request.PlanetId != player.PlanetId || string.IsNullOrEmpty(player.SessionId)
            || string.IsNullOrEmpty(player.StateHash) || request.ExpectedFactoryStateHash != player.StateHash
            || player.StateHash != CanonicalStateHash.PlayerAction(player))
            return "mecha_drone_player_state_changed";
        if (!player.IsAlive || !player.IsOnPlanet || player.MovementState != "Walk"
            || player.IsFlying || player.IsSailing || float.IsNaN(player.Speed) || float.IsInfinity(player.Speed)
            || player.Speed < 0 || player.Speed > .1f || !(player.CoreEnergy > 0)
            || player.HandcraftQueue.Count != 0 || player.ConstructionDrones.Working != 0)
            return "mecha_drone_enable_requires_grounded_idle_player";
        if (player.ConstructionDrones.Enabled || !player.ConstructionDrones.ConstructionEnabled
            || player.ConstructionDrones.Alive <= 0)
            return "mecha_drone_enable_requires_disabled_master_and_available_build_drones";
        return null;
    }

    public static bool ConfirmsPreparedEcho(PrepareConfigureBuildingRequest request, PreparedNormalAction? plan) =>
        ValidateParameters(request) is null
        && plan is { Prepared: true, CommitAllowedNow: true, PlannedMechaConstructionDronesEnabled: true }
        && plan.ActionKind == NormalActionKinds.ConfigureBuilding && !string.IsNullOrEmpty(plan.PlanToken);
}
