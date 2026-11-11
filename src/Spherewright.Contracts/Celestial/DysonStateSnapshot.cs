using Spherewright.Contracts.Factory;

namespace Spherewright.Contracts.Celestial;

public sealed class GetDysonStateRequest
{
    public DysonDesignPreviewRequest? DesignPreview { get; set; }
    public int PlanetId { get; set; }
    public int LayerId { get; set; }
    public int StateHashVersion { get; set; } = 1;
}

public sealed class DysonStateSnapshot
{
    public DysonDesignPreview? DesignPreview { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int PlanetId { get; set; }
    public int StarId { get; set; }
    public long CapturedAtGameTick { get; set; }
    public int SelectedLayerId { get; set; }
    public bool? SelectedLayerPresent { get; set; }
    public bool SphereAvailable { get; set; }
    public bool NativeBindingVerified { get; set; }
    public float? UnlockedNodeLatitudeDegrees { get; set; }
    public int? NodePrototypeCount { get; set; }
    public int? FramePrototypeCount { get; set; }
    public bool StructureCoverageComplete { get; set; }
    public List<DysonLayerSnapshot> Layers { get; set; } = new();
    public long? StructurePoints { get; set; }
    public long? MaximumStructurePoints { get; set; }
    public long? OrderedStructurePoints { get; set; }
    public long? CellPoints { get; set; }
    public long? MaximumCellPoints { get; set; }
    public string DesignStateHash { get; set; } = string.Empty;
    public long? SphereGeneratedEnergyPerTick { get; set; }
    public long? SphereRequestedEnergyPerTick { get; set; }
    public int? ActiveSailCount { get; set; }
    public int? InFlightSailCount { get; set; }
    public bool RocketCoverageComplete { get; set; }
    public int? InFlightRocketCount { get; set; }
    public List<DysonRocketSnapshot> Rockets { get; set; } = new();
    public bool ReceiverCoverageComplete { get; set; }
    public List<RayReceiverStateSnapshot> Receivers { get; set; } = new();
    public long? ReceiverGeneratedEnergyPerTick { get; set; }
    public double? ReceiverGeneratedPowerWatts => ReceiverGeneratedEnergyPerTick * 60d;
    public List<string> CoverageIssues { get; set; } = new();
    public bool ReadOnly => true;
    public bool Executable => false;
    public bool CommitAllowedNow => false;
}

public sealed class DysonLayerSnapshot
{
    public int LayerId { get; set; }
    public double OrbitRadius { get; set; }
    public QuaternionSnapshot OrbitRotation { get; set; } = new();
    public long GeneratedEnergyPerTick { get; set; }
    public List<DysonNodeSnapshot> Nodes { get; set; } = new();
    public List<DysonFrameSnapshot> Frames { get; set; } = new();
    public List<DysonShellSnapshot> Shells { get; set; } = new();
}

public sealed class DysonNodeSnapshot
{
    public int NodeId { get; set; }
    public int PrototypeId { get; set; }
    public Vector3Snapshot Position { get; set; } = new();
    public int StructurePoints { get; set; }
    public int MaximumStructurePoints { get; set; }
    public int OrderedStructurePoints { get; set; }
    public int OrderedCellPoints { get; set; }
}

public sealed class DysonFrameSnapshot
{
    public int FrameId { get; set; }
    public int PrototypeId { get; set; }
    public int NodeAId { get; set; }
    public int NodeBId { get; set; }
    public bool Euler { get; set; }
    public int StructurePointsA { get; set; }
    public int StructurePointsB { get; set; }
    public int MaximumStructurePoints { get; set; }
}

public sealed class DysonShellSnapshot
{
    public int ShellId { get; set; }
    public int PrototypeId { get; set; }
    public List<int> NodeIds { get; set; } = new();
    public List<int> FrameIds { get; set; } = new();
    public long CellPoints { get; set; }
    public long MaximumCellPoints { get; set; }
}

public sealed class RayReceiverStateSnapshot
{
    public bool NativeBindingVerified { get; set; }
    public int PlanetId { get; set; }
    public int StarId { get; set; }
    public int EntityId { get; set; }
    public int ItemId { get; set; }
    public int GeneratorComponentId { get; set; }
    public int NetworkId { get; set; }
    public long CapturedAtGameTick { get; set; }
    public string Mode => ProductItemId == 0 ? "electricity" : "photon";
    public long CapacityEnergyPerTick { get; set; }
    public long GeneratedEnergyPerTick { get; set; }
    public double GeneratedPowerWatts => GeneratedEnergyPerTick * 60d;
    public float Warmup { get; set; }
    public float SunlightStrength { get; set; }
    public int ProductItemId { get; set; }
    public float ProductCount { get; set; }
    public long ProductHeat { get; set; }
    public int CatalystItemId { get; set; }
    public int CatalystCount { get; set; }
    public int CatalystIncPoints { get; set; }
    public int ActiveCatalystItemId { get; set; }
    public int CatalystPoints { get; set; }
    public bool BoostEnabled { get; set; }
}
