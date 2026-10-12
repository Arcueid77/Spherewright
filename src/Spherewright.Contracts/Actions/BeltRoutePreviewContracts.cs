using Spherewright.Contracts.Factory;

namespace Spherewright.Contracts.Actions;

// Explicit, finite data supplied by the caller. No executor or automatic route search.
public sealed class BeltRoutePreviewRequest
{
    public string ExpectedFactoryGeometryHash { get; set; } = string.Empty;
    public int FilterItemId { get; set; }
    public int SourceSorterItemId { get; set; } = 2011;
    public int DestinationSorterItemId { get; set; } = 2012;
    public PlannedBeltEndpointBinding Source { get; set; } = new();
    public PlannedBeltEndpointBinding Destination { get; set; } = new();
    public List<BeltRouteSegment> Segments { get; set; } = new();
}

public sealed class BeltRouteSegment
{
    public string RoutingMode { get; set; } = BeltPathModes.NativeGrid;
    public Vector3Snapshot Start { get; set; } = new();
    public Vector3Snapshot End { get; set; } = new();
    public int StartAltitudeLevel { get; set; }
    public int EndAltitudeLevel { get; set; }
}

public sealed class BeltRoutePreviewSnapshot
{
    public string Phase { get; set; } = "read_only_bounded_belt_route_preview_v2";
    public bool Executable => false;
    public string FactoryGeometryHash { get; set; } = string.Empty;
    public bool NativePathCheckPerformed { get; set; }
    public bool NativePathCheckPassed { get; set; }
    public bool JointNativeCheckPerformed { get; set; }
    public bool JointNativeCheckPassed { get; set; }
    public int? FailedBeltIndex { get; set; }
    public string? FailureStage { get; set; }
    public string? FailureCondition { get; set; }
    public List<NativeBeltRouteSegment> Segments { get; set; } = new();
    public List<NativeBeltRoutePoint> Points { get; set; } = new();
    public BeltEndpointPreviewSnapshot Endpoints { get; set; } = new();
}

public sealed class NativeBeltRouteSegment
{
    public int Index { get; set; }
    public string RoutingMode { get; set; } = string.Empty;
    public int StartAltitudeLevel { get; set; }
    public int EndAltitudeLevel { get; set; }
    // Adjacent segments share exactly ONE index. No native point is truncated.
    public int FirstBeltIndex { get; set; }
    public int LastBeltIndex { get; set; }
    public int NativePointCount { get; set; }
}

public sealed class NativeBeltRoutePoint
{
    public int Index { get; set; }
    // Keep the generated request-bound point separate from a native-adjusted
    // position, especially on rejection. A positive requires them to match.
    public Vector3Snapshot PlannedPosition { get; set; } = new();
    public Vector3Snapshot Position { get; set; } = new();
    public QuaternionSnapshot Rotation { get; set; } = new();
    public float Tilt { get; set; }
    public string NativeCondition { get; set; } = "not_checked";
}
