using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Celestial;

namespace Spherewright.Contracts.Players;

/// <summary>Detached current parameters, never a solver, terrain or stop-distance certificate.</summary>
public sealed class NativePlayerPhysicsSnapshot
{
    public string State { get; set; } = "unavailable";
    public string? ReasonCode { get; set; }
    public bool SafetyCertified => false;
    public string? UnityVersion { get; set; }
    public float? FixedDeltaTimeSeconds { get; set; }
    public bool? AutoSimulation { get; set; }
    public bool? AutoSyncTransforms { get; set; }
    public NativePlayerGroundContactSnapshot? GroundContact { get; set; }
    public NativePlayerGroundContactSnapshot? ResumeGroundContactPreSync { get; set; }
    public NativePlayerGroundContactSnapshot? ResumeGroundContactPostSync { get; set; }
    public string? SimulationMode { get; set; }
    public Vector3Snapshot? Gravity { get; set; }
    /// <summary>Native controller universal gravity, metres per second squared; not Physics.gravity.</summary>
    public UniversalPositionSnapshot? NativeGravity { get; set; }
    public Vector3Snapshot? Velocity { get; set; }
    public bool? UseGravity { get; set; }
    public bool? IsKinematic { get; set; }
    public bool? DetectCollisions { get; set; }
    public string? Constraints { get; set; }
    public string? CollisionDetectionMode { get; set; }
    public float? MaxDepenetrationVelocity { get; set; }
    public float? MaxLinearVelocity { get; set; }
    public int? SolverIterations { get; set; }
    public int? SolverVelocityIterations { get; set; }
    public float? Drag { get; set; }
    public float? Mass { get; set; }
    public bool? Sleeping { get; set; }
    public bool? SameDefaultPhysicsScene { get; set; }
    public bool? CollidersComplete { get; set; }
    public bool? CollidersTruncated { get; set; }
    public List<NativePlayerColliderPhysicsSnapshot>? Colliders { get; set; }
}

/// <summary>Copied Native query evidence; cached fields are not admission evidence.</summary>
public sealed class NativePlayerGroundContactSnapshot
{
    public string State { get; set; } = "unavailable";
    public string? ReasonCode { get; set; }
    public long? CapturedAtGameTick { get; set; }
    public string CaptureStage { get; set; } = string.Empty;
    public bool SafetyCertified => false;
    public int LayerMask => 15873;
    public float SearchDistance => 20f;
    public bool? AutoSyncTransforms { get; set; }
    public bool? QueriesHitTriggers { get; set; }
    public bool? CheckSphereHit { get; set; }
    public bool? SphereCastAttempted { get; set; }
    public bool? SphereCastHit { get; set; }
    public float? GroundDistance { get; set; }
    public bool? CachedWalkIsGrounded { get; set; }
    public float? CachedWalkGroundDistance { get; set; }
    public bool CachedValuesAreObservationOnly => true;
    public NativePlayerGroundColliderSnapshot? HitCollider { get; set; }

    public NativePlayerGroundContactSnapshot Copy() => new()
    {
        State = State, ReasonCode = ReasonCode, CapturedAtGameTick = CapturedAtGameTick, CaptureStage = CaptureStage,
        AutoSyncTransforms = AutoSyncTransforms, QueriesHitTriggers = QueriesHitTriggers, CheckSphereHit = CheckSphereHit,
        SphereCastAttempted = SphereCastAttempted, SphereCastHit = SphereCastHit, GroundDistance = GroundDistance,
        CachedWalkIsGrounded = CachedWalkIsGrounded, CachedWalkGroundDistance = CachedWalkGroundDistance,
        HitCollider = HitCollider is null ? null : new NativePlayerGroundColliderSnapshot
        {
            Type = HitCollider.Type, Layer = HitCollider.Layer, IsTrigger = HitCollider.IsTrigger,
            Enabled = HitCollider.Enabled, ActiveInHierarchy = HitCollider.ActiveInHierarchy,
            SameDefaultPhysicsScene = HitCollider.SameDefaultPhysicsScene,
        },
    };
}

public sealed class NativePlayerGroundColliderSnapshot
{
    public string? Type { get; set; }
    public int? Layer { get; set; }
    public bool? IsTrigger { get; set; }
    public bool? Enabled { get; set; }
    public bool? ActiveInHierarchy { get; set; }
    public bool? SameDefaultPhysicsScene { get; set; }
}

public sealed class NativePlayerColliderPhysicsSnapshot
{
    public string? Shape { get; set; }
    public string? ShapeReasonCode { get; set; }
    public bool? IsTrigger { get; set; }
    public int? Layer { get; set; }
    public bool? AttachedRigidbodyMatches { get; set; }
    public bool? SameDefaultPhysicsScene { get; set; }
    public Vector3Snapshot? LocalCenter { get; set; }
    public Vector3Snapshot? WorldPosition { get; set; }
    public QuaternionSnapshot? WorldRotation { get; set; }
    public Vector3Snapshot? WorldLossyScale { get; set; }
    public float? Radius { get; set; }
    public float? Height { get; set; }
    public int? CapsuleDirection { get; set; }
    public Vector3Snapshot? Size { get; set; }
    public string MaterialState { get; set; } = "unknown";
    public string? MaterialReasonCode { get; set; }
    public NativePlayerPhysicsMaterialSnapshot? Material { get; set; }
}

public sealed class NativePlayerPhysicsMaterialSnapshot
{
    public float? Bounciness { get; set; }
    public float? StaticFriction { get; set; }
    public float? DynamicFriction { get; set; }
    public string? FrictionCombine { get; set; }
    public string? BounceCombine { get; set; }
}
