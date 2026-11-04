using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Factory;

public static class MechaWarperAutoSupplyPolicy
{
    public static string? ValidateParameters(PrepareConfigureBuildingRequest r)
    {
        if (r.Mode != BuildingConfigurationModes.EnableMechaWarperAutoSupply || r.EntityId != 0
            || r.RecipeId != 0 || r.TechId != 0 || r.FilterItemId != 0 || r.EjectorOrbitId != -1
            || !string.IsNullOrEmpty(r.StorageOperation) || r.StorageBannedGridCount != -1
            || r.StationStorageIndex != -1 || r.StationBeltSlotIndex != -1 || r.StationBeltStorageIndex != -1
            || r.StationItemId != 0 || r.StationMaximumCount != 0 || r.StationMaximumChargePowerWatts != 0
            || r.StationVesselDeliveryMinimumPercent != -1 || r.StationLocalLogic != "none"
            || r.StationRemoteLogic != "none" || !string.IsNullOrEmpty(r.ExpectedStationConfigurationStateHash))
            return "warper_auto_supply_requires_exclusive_parameters";
        return null;
    }

    public static string? Validate(PrepareConfigureBuildingRequest r, PlayerStateSnapshot p, MechaWarperSupplyFacts f)
    {
        if (ValidateParameters(r) is {} error) return error;
        if (r.StateHashVersion != CanonicalStateHash.Version || p.StateHashVersion != CanonicalStateHash.Version
            || r.PlanetId != p.PlanetId || string.IsNullOrEmpty(p.SessionId) || string.IsNullOrEmpty(p.StateHash)
            || r.ExpectedFactoryStateHash != p.StateHash || p.StateHash != CanonicalStateHash.PlayerAction(p))
            return "warper_auto_supply_player_changed";
        if (!p.IsAlive || !p.IsOnPlanet || p.MovementState != "Walk" || p.IsFlying || p.IsSailing
            || float.IsNaN(p.Speed) || float.IsInfinity(p.Speed) || p.Speed < 0 || p.Speed > .1f
            || !(p.CoreEnergy > 0) || p.HandcraftQueue.Count != 0 || p.ConstructionDrones.Working != 0
            || !ValidFacts(f)) return "warper_auto_supply_native_grounded_idle_binding_required";
        return f.AutoReplenishWarper ? "warper_auto_supply_already_enabled" : null;
    }

    public static bool ConfirmsEcho(PrepareConfigureBuildingRequest r, PreparedNormalAction? q) =>
        ValidateParameters(r) is null && q is { Prepared:true, CommitAllowedNow:true,
            ActionKind:NormalActionKinds.ConfigureBuilding, PlannedMechaWarperAutoSupplyEnabled:true }
        && !string.IsNullOrEmpty(q.PlanToken);

    public static bool VerifyReadback(MechaWarperSupplyFacts before, MechaWarperSupplyFacts after, bool wholePackagePreserved) =>
        ValidFacts(before) && ValidFacts(after) && !before.AutoReplenishWarper && after.AutoReplenishWarper
        && wholePackagePreserved && before.PackageCount == after.PackageCount && before.PackageInc == after.PackageInc
        && before.ChamberCount == after.ChamberCount && before.ChamberInc == after.ChamberInc;

    private static bool ValidFacts(MechaWarperSupplyFacts f) => f.OwnedBindingVerified && f.CurrentPlanetVerified
        && f.PlayerStateHashFresh && f.IsAlive && f.IsGrounded && f.ControllerEnabled && !f.NativeGuideRunning && f.IsIdle
        && f.ChamberIsFiltered && f.ChamberSlotCount == 1 && f.ChamberGridCount == 1 && f.ChamberBannedGridCount == 0
        && f.ChamberItemId == 1210 && f.ChamberFilterItemId == 1210 && f.ChamberStackSize == 20
        && f.PackageCount >= 0 && f.PackageInc >= 0 && f.ChamberCount is >= 0 and <= 20 && f.ChamberInc >= 0;
}
