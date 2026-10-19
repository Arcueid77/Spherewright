using Spherewright.Contracts.Factory;

namespace Spherewright.Contracts.Logistics;

public sealed class LogisticsStationSnapshot
{
    public string SessionId { get; set; } = string.Empty;

    public int PlanetId { get; set; }

    public int EntityId { get; set; }

    public int StationId { get; set; }

    public int GalacticStationId { get; set; }

    public int BuildingItemId { get; set; }

    public string BuildingName { get; set; } = string.Empty;

    public Vector3Snapshot Position { get; set; } = new Vector3Snapshot();

    public bool IsInterstellar { get; set; }

    public bool IsCollector { get; set; }

    public bool IsVeinCollector { get; set; }

    public int? PowerNetworkId { get; set; }

    public double? PowerServeRatio { get; set; }

    public long Energy { get; set; }

    public long EnergyCapacity { get; set; }

    public long RequestedChargeEnergyPerTick { get; set; }

    public long RequestedChargePowerWatts { get; set; }

    public long MaximumChargeEnergyPerTick { get; set; }

    public long MaximumChargePowerWatts { get; set; }

    public int WarperCount { get; set; }

    public int WarperCapacity { get; set; }

    public int IdleDroneCount { get; set; }

    public int DroneCapacity { get; set; }

    public int WorkingDroneCount { get; set; }

    public int IdleVesselCount { get; set; }

    public int VesselCapacity { get; set; }

    public int WorkingVesselCount { get; set; }

    // Detail-only copied Native transport evidence. Missing/unavailable is
    // unknown, never zero; excluded from action/configuration/fleet hashes.
    public LogisticsActiveVesselObservation? ActiveVessels { get; set; }

    public double DroneTripRangeRaw { get; set; }

    public double VesselTripRangeRaw { get; set; }

    public bool IncludeOrbitCollectors { get; set; }

    public double WarpEnableDistanceRaw { get; set; }

    public bool WarpersRequired { get; set; }

    public int DroneDeliverySetting { get; set; }

    public int VesselDeliverySetting { get; set; }

    public int PilerCount { get; set; }

    public bool DroneAutoReplenish { get; set; }

    public bool VesselAutoReplenish { get; set; }

    public long RemoteGroupMask { get; set; }

    public string RemoteRoutePriority { get; set; } = string.Empty;

    public List<int> NeededItemIds { get; set; } = new List<int>();

    public List<LogisticsStationStorageSlotSnapshot> StorageSlots { get; set; } =
        new List<LogisticsStationStorageSlotSnapshot>();

    public List<LogisticsStationBeltSlotSnapshot> BeltSlots { get; set; } =
        new List<LogisticsStationBeltSlotSnapshot>();

    public long CapturedAtGameTick { get; set; }

    public string StateHash { get; set; } = string.Empty;

    public int StateHashVersion { get; set; } = 1;

    public string ConfigurationStateHash { get; set; } = string.Empty;

    public int ConfigurationStateHashVersion { get; set; } = 1;

    public string FleetStateHash { get; set; } = string.Empty;

    public int FleetStateHashVersion { get; set; } = 1;
}

public sealed class LogisticsStationStorageSlotSnapshot
{
    public int Index { get; set; }

    public int ItemId { get; set; }

    public string? ItemName { get; set; }

    public int Count { get; set; }

    public int Inc { get; set; }

    public int MaximumCount { get; set; }

    public int LocalOrder { get; set; }

    public int RemoteOrder { get; set; }

    public int TotalOrdered { get; set; }

    public int LocalSupplyCount { get; set; }

    public int LocalDemandCount { get; set; }

    public int RemoteSupplyCount { get; set; }

    public int RemoteDemandCount { get; set; }

    public string LocalLogic { get; set; } = string.Empty;

    public string RemoteLogic { get; set; } = string.Empty;

    public int KeepMode { get; set; }

    public float KeepIncRatio { get; set; }
}

public sealed class LogisticsStationBeltSlotSnapshot
{
    public int Index { get; set; }

    public string Direction { get; set; } = string.Empty;

    public int BeltComponentId { get; set; }

    public int BeltEntityId { get; set; }

    public int StorageIndex { get; set; }

    public int Counter { get; set; }
}
