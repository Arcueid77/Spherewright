using Spherewright.Contracts.Factory;

namespace Spherewright.Contracts.Actions;

/// <summary>Advisory samples, never proof of Walk, path clearance or future scene state.</summary>
public sealed class MovementSurfacePreview
{
    public string State { get; set; } = "unavailable";
    public string? ReasonCode { get; set; }
    public string EvidenceScope { get; set; } = "downward_samples_not_route_clearance";
    public long CapturedAtGameTick { get; set; }
    public double? ArcLengthMetres { get; set; }
    public double MaximumSampleSpacingMetres { get; set; } = 1;
    public double MaximumArcMetres { get; set; } = 64;
    public int? WaterItemId { get; set; }
    public int RaycastCount { get; set; }
    public int UnknownSampleCount { get; set; }
    public int ShoreRiskSampleCount { get; set; }
    // detected | not_detected | unknown. Not-detected is not permission to skip arrival checks.
    public string ShoreRisk { get; set; } = "unknown";
    public List<MovementSurfaceSample> Samples { get; set; } = new List<MovementSurfaceSample>();
}

public sealed class MovementSurfaceSample
{
    public Vector3Snapshot Position { get; set; } = new Vector3Snapshot();
    // True only for a hit whose copied distance/altitude evidence passed validation.
    public bool GroundHit { get; set; }
    public bool WaterHit { get; set; }
    public double? GroundAltitudeMetres { get; set; }
    public double? GroundBelowWaterMetres { get; set; }
    public string ShoreRisk { get; set; } = "unknown";
}
