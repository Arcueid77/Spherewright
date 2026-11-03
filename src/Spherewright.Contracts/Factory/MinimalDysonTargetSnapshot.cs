namespace Spherewright.Contracts.Factory;

// Detached proof of one existing pristine Native layer; never initialization.
public sealed class MinimalDysonTargetSnapshot
{
    public int EvidenceVersion { get; set; } = 1;
    public int StarId { get; set; }
    public int LayerId { get; set; }
    public float Radius { get; set; }
    public int NodePrototypeId { get; set; }
    public bool ExactExistingLayerBinding { get; set; }
    public bool CompleteEmptyStructureCoverage { get; set; }
    public bool NativePrototypeAlreadyLoaded { get; set; }
    public bool NativeLatitudeAllowsEquator { get; set; }
    public string ConfigurationStateHash { get; set; } = string.Empty;
    public int PlannedNodeCount => 1;
    public int PlannedDeliveredStructurePoints => 0;
    public int PlannedRequiredStructurePoints => 30;
    public MinimalDysonTargetSnapshot Copy() => (MinimalDysonTargetSnapshot)MemberwiseClone();
}
