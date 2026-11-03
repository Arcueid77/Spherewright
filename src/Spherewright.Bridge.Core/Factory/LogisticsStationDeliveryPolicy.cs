using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Logistics;

namespace Spherewright.Bridge.Core.Factory;

public static class LogisticsStationDeliveryPolicy
{
    public static bool CanApply(PrepareConfigureBuildingRequest r,LogisticsStationSnapshot? s)
        =>r.Mode==BuildingConfigurationModes.LogisticsStationDelivery && r.RecipeId==0 && r.TechId==0 && r.FilterItemId==0
            && r.StorageOperation==string.Empty && r.StorageBannedGridCount==-1 && r.EjectorOrbitId==-1
            && r.StationStorageIndex==-1 && r.StationBeltSlotIndex==-1 && r.StationBeltStorageIndex==-1
            && r.StationItemId==0 && r.StationMaximumCount==0 && r.StationMaximumChargePowerWatts==0
            && r.StationLocalLogic==LogisticsStorageLogics.None && r.StationRemoteLogic==LogisticsStorageLogics.None
            && !string.IsNullOrEmpty(r.ExpectedStationConfigurationStateHash)
            && r.StationVesselDeliveryMinimumPercent is >=1 and <=100 && s is not null
            && s.EntityId==r.EntityId && s.IsInterstellar && !s.IsCollector && !s.IsVeinCollector
            && s.ConfigurationStateHash==r.ExpectedStationConfigurationStateHash
            && s.WorkingVesselCount==0 && s.WorkingDroneCount==0
            && s.VesselDeliverySetting is >=1 and <=100 && s.VesselDeliverySetting!=r.StationVesselDeliveryMinimumPercent;

    public static bool ConfirmsEcho(PrepareConfigureBuildingRequest r,PreparedNormalAction? p)
        =>p is not null && p.PlannedStationVesselDeliveryMinimumPercent==r.StationVesselDeliveryMinimumPercent;
}
