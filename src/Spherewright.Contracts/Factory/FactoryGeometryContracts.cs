using Spherewright.Contracts.Actions;

namespace Spherewright.Contracts.Factory;

public sealed class FactoryGeometryReadRequest
{
    public List<int> ObjectIds { get; set; } = new();
    public List<Vector3Snapshot> TerrainPoints { get; set; } = new();
    // Omit only for the first cut. Subsequent cuts and route previews bind this hash.
    public string? ExpectedFactoryGeometryHash { get; set; }
    // Explicit free-to-free native point generation, never placement validation.
    // Omit for existing object/terrain reads. A whole-layout hash is required.
    public List<BeltRouteSegment>? NativeBeltSegments { get; set; }
}

public sealed class FactoryGeometryCutSnapshot
{
    public string Phase { get; set; } = "read_only_factory_geometry_cut_v2";
    public bool Executable => false;
    public string State { get; set; } = "unavailable";
    public string SessionId { get; set; } = string.Empty;
    public int PlanetId { get; set; }
    public long Revision { get; set; }
    public long CapturedAtGameTick { get; set; }
    public string FactoryGeometryHash { get; set; } = string.Empty;
    public int BuiltObjectCount { get; set; }
    public int PrebuildObjectCount { get; set; }
    public List<FactoryObjectGeometrySnapshot> Objects { get; set; } = new();
    public List<FactoryTerrainPointSnapshot> Terrain { get; set; } = new();
    public List<FactoryNativeBeltSegmentSnapshot> NativeBeltSegments { get; set; } = new();
    public List<string> Blockers { get; set; } = new();
}

public sealed class FactoryNativeBeltSegmentSnapshot
{
    public string Phase { get; set; } = "read_only_native_belt_segment_points_v1";
    public bool Executable => false;
    public bool NativeConditionsChecked { get; set; }
    public int Index { get; set; }
    public BeltRouteSegment Segment { get; set; } = new();
    public bool Observed { get; set; }
    public string? ReasonCode { get; set; }
    public float? NativeGroundRadius { get; set; }
    public float? NativeMaximumSlope { get; set; }
    // Complete SnapLineNonAlloc output before occupancy or native build checks.
    // No truncation, endpoint replacement, collider or placement approval.
    public List<Vector3Snapshot> Points { get; set; } = new();
}

public sealed class FactoryObjectGeometrySnapshot
{
    public int ObjectId { get; set; }
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Observed { get; set; }
    public string? ReasonCode { get; set; }
    public Vector3Snapshot Position { get; set; } = new();
    public Vector3Snapshot Position2 { get; set; } = new();
    public QuaternionSnapshot Rotation { get; set; } = new();
    public QuaternionSnapshot Rotation2 { get; set; } = new();
    public float Tilt { get; set; }
    public bool NativeHasBuildCollider { get; set; }
    public bool InserterSpanExpanded { get; set; }
    // Built belt collider extents/centre are adjusted by CargoTraffic and can
    // differ from the prefab. Expose the source of the returned world bounds.
    public string GeometrySource { get; set; } = "native_build_collider_world_bounds";
    public List<FactoryBuildColliderSnapshot> Colliders { get; set; } = new();
    public List<FactoryConnectionSnapshot> Connections { get; set; } = new();
}

// Native build-collider geometry transformed into the SAME world bounds used by
// the Plugin's placement guards. Non-box shapes retain their conservative flag;
// an overlapping conservative enclosure is not proof of actual collision.
public sealed class FactoryBuildColliderSnapshot
{
    public string NativeShape { get; set; } = string.Empty;
    public bool ConservativeEnclosure { get; set; }
    public Vector3Snapshot Centre { get; set; } = new();
    public Vector3Snapshot AxisX { get; set; } = new();
    public Vector3Snapshot AxisY { get; set; } = new();
    public Vector3Snapshot AxisZ { get; set; } = new();
    public Vector3Snapshot HalfExtents { get; set; } = new();
    public float PaddingMetres { get; set; }
}

public sealed class FactoryTerrainPointSnapshot
{
    public int Index { get; set; }
    public Vector3Snapshot RequestedPosition { get; set; } = new();
    public bool Observed { get; set; }
    public string? ReasonCode { get; set; }
    public float? NativeModifiedSurfaceRadius { get; set; }
    public bool GroundRayHit { get; set; }
    public float? GroundRadius { get; set; }
    public bool WaterRayHit { get; set; }
    public float? WaterRadius { get; set; }
    public int WaterItemId { get; set; }
    public double? MaximumNeighbourGradient { get; set; }
    public Vector3Snapshot? GroundGridSnapPosition { get; set; }
    public List<Vector3Snapshot> AltitudeGridSnapPositions { get; set; } = new();
}
