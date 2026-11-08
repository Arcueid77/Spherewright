using Spherewright.Contracts.Factory;

namespace Spherewright.Contracts.Actions;

public sealed class MovementGeometrySnapshot
{
    public int EvidenceVersion { get; set; } = 1;
    public string State { get; set; } = "unavailable";
    public bool Complete { get; set; }
    public List<string> Blockers { get; set; } = new();
    public string SessionId { get; set; } = string.Empty;
    public int PlanetId { get; set; }
    public long CapturedAtGameTick { get; set; }
    public string PlayerStateHash { get; set; } = string.Empty;
    public string GeometryHash { get; set; } = string.Empty;
    public Vector3Snapshot Origin { get; set; } = new();
    public Vector3Snapshot EffectiveTarget { get; set; } = new();
    public double RadiusMetres { get; set; }
    public double GroundOffsetMetres { get; set; }
    public List<WalkingBodyCapsule> Body { get; set; } = new();
    public List<WalkingColliderSnapshot> Colliders { get; set; } = new();
}

public sealed class WalkingBodyCapsule
{
    public double LowerCentreHeight { get; set; }
    public double UpperCentreHeight { get; set; }
    public double Radius { get; set; }
}

public sealed class WalkingColliderSnapshot
{
    public int NativeColliderId { get; set; }
    public string ObjectKind { get; set; } = string.Empty;
    public int ObjectId { get; set; }
    public string Shape { get; set; } = string.Empty;
    public string GeometrySource { get; set; } = "native_physics_pool";
    public Vector3Snapshot Centre { get; set; } = new();
    public Vector3Snapshot Extents { get; set; } = new();
    public QuaternionSnapshot Rotation { get; set; } = new();
    public double Radius { get; set; }
    public bool CurrentSceneProximityObserved { get; set; }
}

public sealed class WalkingTerrainPoint
{
    public int Index { get; set; }
    public Vector3Snapshot RequestedPosition { get; set; } = new();
    public Vector3Snapshot? Position { get; set; }
    public bool Observed { get; set; }
    public bool Dry { get; set; }
    public double? GroundRadiusMetres { get; set; }
    public double? WaterRadiusMetres { get; set; }
    public double? GroundNormalDotUp { get; set; }
    public string? ReasonCode { get; set; }
}

public sealed class WalkingRoutePreview
{
    public int EvidenceVersion { get; set; } = 1;
    public bool Executable => false;
    public string State { get; set; } = "unavailable";
    public bool TerrainCoverageComplete { get; set; }
    public MovementGeometrySnapshot? Geometry { get; set; }
    public List<WalkingTerrainPoint> TerrainPoints { get; set; } = new();
    public List<Vector3Snapshot> Waypoints { get; set; } = new();
    public List<WalkingRouteSegment> Segments { get; set; } = new();
    public List<string> Blockers { get; set; } = new();
    public double SafetyMarginMetres { get; set; }
    public double? LengthMetres { get; set; }
}

public sealed class WalkingRouteSegment
{
    public double StartPositionAllowanceMetres { get; set; }
    public Vector3Snapshot Start { get; set; } = new();
    public Vector3Snapshot End { get; set; } = new();
    public double? MinimumClearanceMetres { get; set; }
    public double MaximumSampleSpacingMetres { get; set; }
}

public sealed class MovementDiagnosticsSnapshot
{
    public int EvidenceVersion { get; set; } = 1;
    public string State { get; set; } = "unavailable";
    public long CapturedAtGameTick { get; set; }
    public string CaptureStage { get; set; } = "movement_failure_observation";
    public Vector3Snapshot? Position { get; set; }
    public Vector3Snapshot? EffectiveTarget { get; set; }
    public double? RemainingDistanceMetres { get; set; }
    public double? TangentialRemainingDistanceMetres { get; set; }
    public double? RadialMismatchMetres { get; set; }
    public double? ArrivalToleranceMetres { get; set; }
    public string Hint { get; set; } = "unknown";
    public bool GeometryComplete { get; set; }
    public List<string> Blockers { get; set; } = new();
    public List<MovementObstructionCandidate> Candidates { get; set; } = new();
}

public sealed class MovementObstructionCandidate
{
    public int NativeColliderId { get; set; }
    public string ObjectKind { get; set; } = string.Empty;
    public int ObjectId { get; set; }
    public string EvidenceSource { get; set; } = string.Empty;
    public double BodyClearanceMetres { get; set; }
    public bool InTargetDirection { get; set; }
}
