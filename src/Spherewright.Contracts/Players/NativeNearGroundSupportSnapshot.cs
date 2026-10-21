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
    public bool? ActualGroundSphereHit { get; set; }
    public bool? CastAttempted { get; set; }
    public bool? CastHit { get; set; }
    /// <summary>Native sphere-cast travel distance in metres; null on a miss or an unavailable query.</summary>
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

    public NativeNearGroundSupportSnapshot Copy() => new()
    {
        State = State, ReasonCode = ReasonCode, CapturedAtGameTick = CapturedAtGameTick, CaptureStage = CaptureStage,
        ActualGroundSphereHit = ActualGroundSphereHit, CastAttempted = CastAttempted, CastHit = CastHit,
        CastDistance = CastDistance, HitPoint = CopyVector(HitPoint), HitNormal = CopyVector(HitNormal), NormalDotUp = NormalDotUp,
        Layer = Layer, Shape = Shape, IsTrigger = IsTrigger, Enabled = Enabled, ActiveInHierarchy = ActiveInHierarchy,
        SameDefaultPhysicsScene = SameDefaultPhysicsScene, OwnerType = OwnerType, ObjectId = ObjectId,
        CurrentPoolReference = CurrentPoolReference, LiveObject = LiveObject, ColliderChain = ColliderChain,
        NativeLiveFlag = NativeLiveFlag, FootOverlapCount = FootOverlapCount, FootOverlapComplete = FootOverlapComplete,
        AllFootHitsCurrentSupportOrTerrain = AllFootHitsCurrentSupportOrTerrain,
        CurrentSupportFootHit = CurrentSupportFootHit, TerrainFootHit = TerrainFootHit,
    };

    private static Vector3Snapshot? CopyVector(Vector3Snapshot? value) =>
        value is null ? null : new() { X = value.X, Y = value.Y, Z = value.Z };
}
