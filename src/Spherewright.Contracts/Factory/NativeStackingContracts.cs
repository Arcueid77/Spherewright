namespace Spherewright.Contracts.Factory;

public sealed class NativeStackingCatalogEntry
{
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ModelIndex { get; set; }
    public string Family { get; set; } = string.Empty;
    public bool CanStack { get; set; }
    public bool StorageAttachment { get; set; }
    public bool Unlocked { get; set; }
    public int InventoryCount { get; set; }
    public int MaximumLayers { get; set; }
    public bool AllowsRotation { get; set; }
    public bool AllowsPorts { get; set; }
    public Vector3Snapshot JointOffset { get; set; } = new();
    public List<int> CompatibleBaseItemIds { get; set; } = new();
    public List<int> QuarterTurnBaseItemIds { get; set; } = new();
}

public sealed class NativeStackSnapshot
{
    public int EvidenceVersion { get; set; } = 1;
    public string State { get; set; } = "unavailable";
    public string? ReasonCode { get; set; }
    public long CapturedAtGameTick { get; set; }
    public bool Complete { get; set; }
    public int SelectedObjectId { get; set; }
    public int RootObjectId { get; set; }
    public int TopObjectId { get; set; }
    public string StateHash { get; set; } = string.Empty;
    public List<NativeStackLayerSnapshot> Layers { get; set; } = new();
}

public sealed class NativeStackLayerSnapshot
{
    public int ObjectId { get; set; }
    public int ItemId { get; set; }
    public int ModelIndex { get; set; }
    public string Family { get; set; } = string.Empty;
    public bool IsStorageHostBase { get; set; }
    public Vector3Snapshot Position { get; set; } = new();
    public QuaternionSnapshot Rotation { get; set; } = new();
    public string EndpointStateHash { get; set; } = string.Empty;
    public List<FactoryConnectionSnapshot> Connections { get; set; } = new();
    public StorageConfigurationSnapshot? StorageConfiguration { get; set; }
    public NativeStackDeviceState? DeviceState { get; set; }
    public List<FactoryBufferSnapshot> Buffers { get; set; } = new();
    public int? TankFluidCount { get; set; }
    public int RecipeId { get; set; }
    public int Progress { get; set; }
    public int ProgressRequired { get; set; }
}

public sealed class NativeStackDeviceState
{
    public int? NextComponentId { get; set; }
    public int? PreviousComponentId { get; set; }
    public bool? ResearchMode { get; set; }
    public int? TechId { get; set; }
    public int? BonusProgress { get; set; }
    public int? ResearchHashBytes { get; set; }
    public int? BonusResearchHashBytes { get; set; }
    public int? FluidId { get; set; }
    public int? FluidInc { get; set; }
    public int? FluidCapacity { get; set; }
    public bool? InputEnabled { get; set; }
    public bool? OutputEnabled { get; set; }
    public bool? InputPriority { get; set; }
    public bool? OutputPriority { get; set; }
    public int? OutputFilterItemId { get; set; }
    public int? OutputFilterPreset { get; set; }
    public int? PrioritySlotPresets { get; set; }
    public List<int>? NativeBeltIds { get; set; }
    public List<int>? NativeInputBeltIds { get; set; }
    public List<int>? NativeOutputBeltIds { get; set; }
}

public sealed class NativeStackConstructionPlan
{
    public int EvidenceVersion { get; set; } = 1;
    public int SelectedObjectId { get; set; }
    public int RootObjectId { get; set; }
    public int AnchorObjectId { get; set; }
    public string BeforeStackStateHash { get; set; } = string.Empty;
    public int BuildingItemId { get; set; }
    public int ModelIndex { get; set; }
    public int ExistingLayerCount { get; set; }
    public List<int> ExistingObjectIds { get; set; } = new();
    public int MaximumLayers { get; set; }
    public int InputFromSlot { get; set; } = 15;
    public int InputToSlot { get; set; } = 14;
}
