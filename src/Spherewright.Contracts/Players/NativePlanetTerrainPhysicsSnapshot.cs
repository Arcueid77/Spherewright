using Spherewright.Contracts.Factory;

namespace Spherewright.Contracts.Players;

/// <summary>Bounded copied local terrain facts, never an execution certificate.</summary>
public sealed class NativePlanetTerrainPhysicsSnapshot
{
    public string State { get; set; } = "unavailable";
    public string? ReasonCode { get; set; }
    public long? CapturedAtGameTick { get; set; }
    public string CaptureStage { get; set; } = string.Empty;
    public bool SafetyCertified => false;
    public bool Complete { get; set; }
    public bool Truncated { get; set; }
    public bool? GuideComplete { get; set; }
    public int? RootLayer { get; set; }
    public int? BodyLayer { get; set; }
    public Vector3Snapshot? RootWorldPosition { get; set; }
    public QuaternionSnapshot? RootWorldRotation { get; set; }
    public Vector3Snapshot? RootWorldLossyScale { get; set; }
    public Vector3Snapshot? BodyWorldPosition { get; set; }
    public QuaternionSnapshot? BodyWorldRotation { get; set; }
    public Vector3Snapshot? BodyWorldLossyScale { get; set; }
    public bool? SphereColliderEnabled { get; set; }
    public int? SphereColliderLayer { get; set; }
    public bool? OceanColliderEnabled { get; set; }
    public int? OceanColliderLayer { get; set; }
    public int? FootOverlapCount { get; set; }
    public bool? FootOverlapComplete { get; set; }
    public bool? FootOverlapsOwnedSurfaceOnly { get; set; }
    public bool? NativeGroundSphereHit { get; set; }
    public List<NativeTerrainColliderPhysicsSnapshot>? SurfaceColliders { get; set; }

    public NativePlanetTerrainPhysicsSnapshot Copy() => new()
    {
        State = State, ReasonCode = ReasonCode, CapturedAtGameTick = CapturedAtGameTick, CaptureStage = CaptureStage,
        Complete = Complete, Truncated = Truncated, GuideComplete = GuideComplete, RootLayer = RootLayer, BodyLayer = BodyLayer,
        RootWorldPosition = Vector(RootWorldPosition), RootWorldRotation = Rotation(RootWorldRotation), RootWorldLossyScale = Vector(RootWorldLossyScale),
        BodyWorldPosition = Vector(BodyWorldPosition), BodyWorldRotation = Rotation(BodyWorldRotation), BodyWorldLossyScale = Vector(BodyWorldLossyScale),
        SphereColliderEnabled = SphereColliderEnabled, SphereColliderLayer = SphereColliderLayer,
        OceanColliderEnabled = OceanColliderEnabled, OceanColliderLayer = OceanColliderLayer,
        FootOverlapCount = FootOverlapCount, FootOverlapComplete = FootOverlapComplete, FootOverlapsOwnedSurfaceOnly = FootOverlapsOwnedSurfaceOnly,
        NativeGroundSphereHit = NativeGroundSphereHit,
        SurfaceColliders = SurfaceColliders?.Select(c => new NativeTerrainColliderPhysicsSnapshot
        {
            Type = c.Type, Enabled = c.Enabled, ActiveInHierarchy = c.ActiveInHierarchy, Layer = c.Layer,
            IsTrigger = c.IsTrigger, SharedMeshPresent = c.SharedMeshPresent, SameDefaultPhysicsScene = c.SameDefaultPhysicsScene,
            WorldPosition = Vector(c.WorldPosition), WorldRotation = Rotation(c.WorldRotation), WorldLossyScale = Vector(c.WorldLossyScale),
        }).ToList(),
    };
    private static Vector3Snapshot? Vector(Vector3Snapshot? p) => p is null ? null : new() { X = p.X, Y = p.Y, Z = p.Z };
    private static QuaternionSnapshot? Rotation(QuaternionSnapshot? p) => p is null ? null : new() { X = p.X, Y = p.Y, Z = p.Z, W = p.W };
}

public sealed class NativeTerrainColliderPhysicsSnapshot
{
    public string? Type { get; set; }
    public bool? Enabled { get; set; }
    public bool? ActiveInHierarchy { get; set; }
    public int? Layer { get; set; }
    public bool? IsTrigger { get; set; }
    public bool? SharedMeshPresent { get; set; }
    public bool? SameDefaultPhysicsScene { get; set; }
    public Vector3Snapshot? WorldPosition { get; set; }
    public QuaternionSnapshot? WorldRotation { get; set; }
    public Vector3Snapshot? WorldLossyScale { get; set; }
}
