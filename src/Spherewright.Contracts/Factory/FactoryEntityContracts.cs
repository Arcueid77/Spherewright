using Spherewright.Contracts.Logistics;

namespace Spherewright.Contracts.Factory;

public static class FactoryObjectKinds
{
    public const string Entity = "entity";
    public const string Prebuild = "prebuild";
}

public sealed class ListFactoryEntitiesRequest
{
    public int PlanetId { get; set; }

    public string? ObjectKind { get; set; }

    public string? ComponentKind { get; set; }

    public int? ItemId { get; set; }

    public int Limit { get; set; } = 50;

    public string? Cursor { get; set; }
}

public sealed class ListFactoryEntitiesResult
{
    public string SessionId { get; set; } = string.Empty;

    public int PlanetId { get; set; }

    public long CapturedAtGameTick { get; set; }

    public string SnapshotId { get; set; } = string.Empty;

    public DateTimeOffset SnapshotExpiresAtUtc { get; set; }

    public List<FactoryEntitySnapshot> Entities { get; set; } = new List<FactoryEntitySnapshot>();

    public string? NextCursor { get; set; }
}

public sealed class InspectFactoryEntityRequest
{
    public FactoryGeometryReadRequest? Geometry { get; set; }
    public int PlanetId { get; set; }

    public int ObjectId { get; set; }

    // Opt-in explicit selection. Empty keeps the existing single-object read.
    public List<int> MaterialInventoryObjectIds { get; set; } = new List<int>();
}

public sealed class FactoryEntitySnapshot
{
    // Detail-only, opt-in local geometry; excluded from all action/configuration hashes.
    public FactoryGeometryCutSnapshot? GeometryCut { get; set; }
    public string SessionId { get; set; } = string.Empty;

    public int PlanetId { get; set; }

    public int ObjectId { get; set; }

    public string ObjectKind { get; set; } = string.Empty;

    public int ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ComponentKind { get; set; } = string.Empty;

    public Vector3Snapshot Position { get; set; } = new Vector3Snapshot();

    public QuaternionSnapshot Rotation { get; set; } = new QuaternionSnapshot();

    public int RecipeId { get; set; }

    public string? RecipeName { get; set; }

    public bool? ForceAccelerationMode { get; set; }

    public bool IsWorking { get; set; }

    public int Progress { get; set; }

    public int ProgressRequired { get; set; }

    public int? PowerNetworkId { get; set; }

    public long? PowerDemandPerTick { get; set; }

    public double? PowerServeRatio { get; set; }

    // Detail-only scalar observation for ordinary thermal/fusion generators.
    // Null/unavailable is unknown, not zero. Excluded from every action hash;
    // fuelEnergy is residual loaded heat, never an extra count of fuel items.
    public FuelPowerStateSnapshot? FuelPowerState { get; set; }

    public List<FactoryConnectionSnapshot> Connections { get; set; } = new List<FactoryConnectionSnapshot>();

    public List<FactoryBufferSnapshot> Buffers { get; set; } = new List<FactoryBufferSnapshot>();

    // Total native fluid items in this exact tank, including verified zero.
    // Null/missing means unobserved or not a tank; empty Buffers alone is unknown.
    // Read-only evidence, excluded from action/configuration/endpoint hashes.
    public int? TankFluidCount { get; set; }

    // Detail-only observation, separate from device buffers and action hashes.
    // Null (including list snapshots) means not observed, never an empty belt.
    public BeltCargoSnapshot? BeltCargo { get; set; }

    // Detail-only, opt-in and same-tick. Null/partial is not an empty material cut.
    public MaterialInventoryCutSnapshot? MaterialInventoryCut { get; set; }

    // Optional detail-only geometry. Null/unknown never means no possible endpoint.
    public SorterEndpointObservation? SorterEndpoints { get; set; }

    public List<int> ResourceNodeIds { get; set; } = new List<int>();

    public LogisticsStationSnapshot? LogisticsStation { get; set; }

    // Inventory grids, NOT the physical connection ports in build-catalog SlotCount.
    // Null means not a storage or configuration could not be captured within the bound.
    public StorageConfigurationSnapshot? StorageConfiguration { get; set; }

    public int? PickTargetObjectId { get; set; }

    public int? InsertTargetObjectId { get; set; }

    public int? FilterItemId { get; set; }

    public string? FilterItemName { get; set; }

    public string? InserterStage { get; set; }

    public int? InserterStackCount { get; set; }

    public int? RequiredBuildItemCount { get; set; }

    public float? ConstructionProgress { get; set; }

    public long CapturedAtGameTick { get; set; }

    public string StateHash { get; set; } = string.Empty;

    public int StateHashVersion { get; set; } = 1;

    public string ConfigurationStateHash { get; set; } = string.Empty;

    public int ConfigurationStateHashVersion { get; set; } = 1;

    public string EndpointStateHash { get; set; } = string.Empty;

    public int EndpointStateHashVersion { get; set; } = 1;
}

public sealed class StorageConfigurationSnapshot
{
    public int GridCount { get; set; }

    public int BannedGridCount { get; set; }

    public string Mode { get; set; } = string.Empty;

    public List<int> GridFilterItemIds { get; set; } = new List<int>();
}

public sealed class QuaternionSnapshot
{
    public float X { get; set; }

    public float Y { get; set; }

    public float Z { get; set; }

    public float W { get; set; }
}

public sealed class FactoryConnectionSnapshot
{
    public int Slot { get; set; }

    public bool IsOutput { get; set; }

    public int OtherObjectId { get; set; }

    public int OtherSlot { get; set; }
}

public sealed class FactoryBufferSnapshot
{
    // Research buffers use 3600 points per item. Energy observations are not item stocks.
    public string CountUnit { get; set; } = "items";

    // Zero means no item conversion (for example joules_per_tick); never divide in that case.
    public int UnitsPerItem { get; set; } = 1;

    public string Role { get; set; } = string.Empty;

    public int ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Count { get; set; }

    public int Inc { get; set; }
}
