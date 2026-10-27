using Spherewright.Contracts.Factory;

namespace Spherewright.Contracts.Players;

/// <summary>Detached Native query and current-pool membership facts, never a landing certificate.</summary>
public sealed class NativeNearGroundSupportSnapshot
{
    public string State { get; set; } = "unavailable";
    public string? ReasonCode { get; set; }
    public long? CapturedAtGameTick { get; set; }
    public string CaptureStage { get; set; } = string.Empty;
    public bool SafetyCertified => false;
    /// <summary>Bounded Native registered-collider count; unknown before observation, never ownership or ground proof.</summary>
    public int? RegisteredColliderCount { get; set; }
    public bool? ActualGroundSphereHit { get; set; }
    public bool? CastAttempted { get; set; }
    public bool? CastHit { get; set; }
    /// <summary>Actual query mask. A loaded terrain layer31 cast is conditional support, never Native ground contact.</summary>
    public int? CastLayerMask { get; set; }
    /// <summary>Actual sphere-cast travel distance in metres; CastLayerMask distinguishes the query, null on a miss.</summary>
    public float? CastDistance { get; set; }
    public Vector3Snapshot? HitPoint { get; set; }
    public Vector3Snapshot? HitNormal { get; set; }
    public float? NormalDotUp { get; set; }
    public int? Layer { get; set; }
    public string? Shape { get; set; }
    public bool? IsTrigger { get; set; }
    public bool? Enabled { get; set; }
    public bool? ActiveInHierarchy { get; set; }
    public bool? SameDefaultPhysicsScene { get; set; }
    public string? OwnerType { get; set; }
    public int? ObjectId { get; set; }
    public bool? CurrentPoolReference { get; set; }
    public bool? LiveObject { get; set; }
    public bool? ColliderChain { get; set; }
    /// <summary>Native near-collider liveness mark. Native task501 clears this mark before Update renews it.</summary>
    public bool? NativeLiveFlag { get; set; }
    public int? FootOverlapCount { get; set; }
    public bool? FootOverlapComplete { get; set; }
    public bool? AllFootHitsCurrentSupportOrTerrain { get; set; }
    public bool? CurrentSupportFootHit { get; set; }
    public bool? TerrainFootHit { get; set; }
    /// <summary>Read-only identity diagnostics captured before rejection; never permission to replace support.</summary>
    public NativeNearGroundSupportIdentitySnapshot? Identity { get; set; }

    public NativeNearGroundSupportSnapshot Copy() => new()
    {
        State = State, ReasonCode = ReasonCode, CapturedAtGameTick = CapturedAtGameTick, CaptureStage = CaptureStage,
        RegisteredColliderCount = RegisteredColliderCount,
        ActualGroundSphereHit = ActualGroundSphereHit, CastAttempted = CastAttempted, CastHit = CastHit,
        CastLayerMask = CastLayerMask,
        CastDistance = CastDistance, HitPoint = CopyVector(HitPoint), HitNormal = CopyVector(HitNormal), NormalDotUp = NormalDotUp,
        Layer = Layer, Shape = Shape, IsTrigger = IsTrigger, Enabled = Enabled, ActiveInHierarchy = ActiveInHierarchy,
        SameDefaultPhysicsScene = SameDefaultPhysicsScene, OwnerType = OwnerType, ObjectId = ObjectId,
        CurrentPoolReference = CurrentPoolReference, LiveObject = LiveObject, ColliderChain = ColliderChain,
        NativeLiveFlag = NativeLiveFlag, FootOverlapCount = FootOverlapCount, FootOverlapComplete = FootOverlapComplete,
        AllFootHitsCurrentSupportOrTerrain = AllFootHitsCurrentSupportOrTerrain,
        CurrentSupportFootHit = CurrentSupportFootHit, TerrainFootHit = TerrainFootHit,
        Identity = Identity?.Copy(),
    };

    private static Vector3Snapshot? CopyVector(Vector3Snapshot? value) =>
        value is null ? null : new() { X = value.X, Y = value.Y, Z = value.Z };
}

/// <summary>Nullable observations distinguish queried and retained colliders without exposing Native instance IDs.</summary>
public sealed class NativeNearGroundSupportIdentitySnapshot
{
    public NativePlayerGroundColliderSnapshot? QueriedHit { get; set; }
    public bool? SameManagedReference { get; set; }
    public bool? SameUnityObject { get; set; }
    public bool? SameNativeInstance { get; set; }
    public bool? RetainedRecordMatches { get; set; }
    public bool? RetainedColliderMatches { get; set; }
    public bool? RetainedPoolCellMatches { get; set; }
    public bool? RetainedObjectPoolMatches { get; set; }
    public bool? RetainedChainMatches { get; set; }
    public bool? PlanetColliderDirty { get; set; }
    public string? ObservationReasonCode { get; set; }

    public NativeNearGroundSupportIdentitySnapshot Copy() => new()
    {
        QueriedHit = QueriedHit is null ? null : new NativePlayerGroundColliderSnapshot
        {
            Type = QueriedHit.Type, Layer = QueriedHit.Layer, IsTrigger = QueriedHit.IsTrigger,
            Enabled = QueriedHit.Enabled, ActiveInHierarchy = QueriedHit.ActiveInHierarchy,
            SameDefaultPhysicsScene = QueriedHit.SameDefaultPhysicsScene,
        },
        SameManagedReference = SameManagedReference, SameUnityObject = SameUnityObject,
        SameNativeInstance = SameNativeInstance, RetainedRecordMatches = RetainedRecordMatches,
        RetainedColliderMatches = RetainedColliderMatches, RetainedPoolCellMatches = RetainedPoolCellMatches,
        RetainedObjectPoolMatches = RetainedObjectPoolMatches, RetainedChainMatches = RetainedChainMatches,
        PlanetColliderDirty = PlanetColliderDirty, ObservationReasonCode = ObservationReasonCode,
    };
}
