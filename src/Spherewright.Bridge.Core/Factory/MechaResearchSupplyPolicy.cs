using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Factory;

public static class MechaResearchSupplyPolicy
{
    public static bool IsMode(string mode) => mode is BuildingConfigurationModes.DisableMechaResearchAutoSupply
        or BuildingConfigurationModes.EnableMechaResearchAutoSupply;
    public static bool Desired(string mode) => mode == BuildingConfigurationModes.EnableMechaResearchAutoSupply;

    public static string? ValidateParameters(PrepareConfigureBuildingRequest request)
    {
        // Reuse the exclusive no-device-parameter contract of the native mecha switch.
        if (!IsMode(request.Mode)) return "mecha_research_supply_mode_required";
        var exclusive = new PrepareConfigureBuildingRequest
        {
            Mode = BuildingConfigurationModes.EnableMechaConstructionDrones, EntityId=request.EntityId,
            RecipeId=request.RecipeId, TechId=request.TechId, FilterItemId=request.FilterItemId,
            EjectorOrbitId=request.EjectorOrbitId, StorageOperation=request.StorageOperation,
            StorageBannedGridCount=request.StorageBannedGridCount, StationStorageIndex=request.StationStorageIndex,
            StationBeltSlotIndex=request.StationBeltSlotIndex, StationBeltStorageIndex=request.StationBeltStorageIndex,
            StationItemId=request.StationItemId, StationMaximumCount=request.StationMaximumCount,
            StationMaximumChargePowerWatts=request.StationMaximumChargePowerWatts,
            StationLocalLogic=request.StationLocalLogic, StationRemoteLogic=request.StationRemoteLogic,
            ExpectedStationConfigurationStateHash=request.ExpectedStationConfigurationStateHash,
        };
        if (MechaConstructionDronePolicy.ValidateParameters(exclusive) is not null
            || request.StationVesselDeliveryMinimumPercent != -1) return "mecha_research_supply_requires_exclusive_parameters";
        return null;
    }

    public static string? Validate(PrepareConfigureBuildingRequest request, PlayerStateSnapshot player)
    {
        if (ValidateParameters(request) is {} error) return error;
        if (request.StateHashVersion != CanonicalStateHash.Version || player.StateHashVersion != CanonicalStateHash.Version
            || request.PlanetId != player.PlanetId || string.IsNullOrEmpty(player.SessionId)
            || request.ExpectedFactoryStateHash != player.StateHash || player.StateHash != CanonicalStateHash.PlayerAction(player))
            return "mecha_research_supply_player_state_changed";
        if (!player.IsAlive || !player.IsOnPlanet || player.MovementState != "Walk" || player.IsFlying || player.IsSailing
            || float.IsNaN(player.Speed) || float.IsInfinity(player.Speed) || player.Speed < 0 || player.Speed > .1f
            || !(player.CoreEnergy > 0) || player.HandcraftQueue.Count != 0 || player.ConstructionDrones.Working != 0)
            return "mecha_research_supply_requires_grounded_idle_player";
        if (player.MechaResearchItemBuffer.Count != 0) return "mecha_research_supply_requires_empty_point_buffer";
        if (player.AutoManageResearchItems == Desired(request.Mode)) return "mecha_research_supply_requires_actual_setting_change";
        return null;
    }

    public static bool ConfirmsEcho(PrepareConfigureBuildingRequest request, PreparedNormalAction? plan) =>
        ValidateParameters(request) is null && plan is { Prepared:true, CommitAllowedNow:true }
        && plan.ActionKind == NormalActionKinds.ConfigureBuilding && !string.IsNullOrEmpty(plan.PlanToken)
        && plan.PlannedMechaResearchAutoSupplyEnabled == Desired(request.Mode);
}
