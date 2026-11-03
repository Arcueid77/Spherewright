namespace Spherewright.Contracts.Factory;

// Same-tick detail observations, never an instruction or a launch receipt.
public sealed class SiloStateSnapshot
{
    public bool NativeBindingVerified { get; set; }
    public int SiloComponentId { get; set; }
    public int StarId { get; set; }
    public int RocketItemId { get; set; }
    public int RocketCount { get; set; }
    public int RocketIncPoints { get; set; }
    public int Direction { get; set; }
    public int CycleProgressRaw { get; set; }
    public int ChargeDurationRaw { get; set; }
    public int CooldownDurationRaw { get; set; }
    public bool NativeHasReceivingNode { get; set; }
    public bool BoostEnabled { get; set; }
    public bool FiredAnimationActive { get; set; }
    public bool FiredAnimationIsLaunchReceipt => false;
    public bool SphereAvailable { get; set; }
    public bool RocketCoverageComplete { get; set; }
    public int? InFlightRocketCount { get; set; }
    public List<DysonRocketSnapshot> Rockets { get; set; } = new();
    public bool NodeCoverageComplete { get; set; }
    public List<DysonReceivingNodeSnapshot> Nodes { get; set; } = new();
    public bool StarSiloCoverageComplete { get; set; }
    public int? StarSiloCount { get; set; }
    public MinimalDysonTargetSnapshot? MinimalReceivingTarget { get; set; }
}

public sealed class DysonRocketSnapshot
{
    public int RocketId { get; set; }
    public int SourcePlanetId { get; set; }
    public int TargetLayerId { get; set; }
    public int TargetNodeId { get; set; }
    public float NativeFlightProgress { get; set; }
    public double UniversalX { get; set; }
    public double UniversalY { get; set; }
    public double UniversalZ { get; set; }
    public float Speed { get; set; }
}

public sealed class DysonReceivingNodeSnapshot
{
    public int LayerId { get; set; }
    public int NodeId { get; set; }
    public int StructurePoints { get; set; }
    public int MaximumStructurePoints { get; set; }
    public int OrderedStructurePoints { get; set; }
    public int RemainingOrderableStructurePoints { get; set; }
}
