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
    public bool? FootHitDetailsComplete { get; set; }
    public int? RegisteredSimulatorCount { get; set; }
    public bool? RegisteredTerrainOwnersComplete { get; set; }
    public int? FootCurrentSurfaceHitCount { get; set; }
    public int? FootRegisteredSurfaceHitCount { get; set; }
    public bool? FootOverlapsCurrentOrRegisteredSurfaceOnly { get; set; }
    public List<NativeRegisteredTerrainOwnerPhysicsSnapshot>? RegisteredTerrainOwners { get; set; }
    public long? RegisteredPostPhysicsCapturedAtGameTick { get; set; }
    public bool? RegisteredPostPhysicsVerified { get; set; }
    public string? RegisteredPostPhysicsReasonCode { get; set; }
    // At most the 257 results from the same bounded query; diagnostics never replace aggregate admission evidence.
    public List<NativeTerrainFootHitPhysicsSnapshot>? FootHits { get; set; }
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
        FootHitDetailsComplete = FootHitDetailsComplete,
        RegisteredSimulatorCount = RegisteredSimulatorCount, RegisteredTerrainOwnersComplete = RegisteredTerrainOwnersComplete,
        FootCurrentSurfaceHitCount = FootCurrentSurfaceHitCount, FootRegisteredSurfaceHitCount = FootRegisteredSurfaceHitCount,
        FootOverlapsCurrentOrRegisteredSurfaceOnly = FootOverlapsCurrentOrRegisteredSurfaceOnly,
        RegisteredTerrainOwners = RegisteredTerrainOwners?.Select(c => c.Copy()).ToList(),
        RegisteredPostPhysicsCapturedAtGameTick = RegisteredPostPhysicsCapturedAtGameTick,
        RegisteredPostPhysicsVerified = RegisteredPostPhysicsVerified, RegisteredPostPhysicsReasonCode = RegisteredPostPhysicsReasonCode,
        FootHits = FootHits?.Select(c => new NativeTerrainFootHitPhysicsSnapshot
        {
            State = c.State, ReasonType = c.ReasonType, Type = c.Type, UniqueSurfaceIndex = c.UniqueSurfaceIndex,
            IsCurrentSurface = c.IsCurrentSurface, SameCurrentOcean = c.SameCurrentOcean,
            SameCurrentSphere = c.SameCurrentSphere, UnknownOwner = c.UnknownOwner,
            CurrentSurfaceUnityMatchCount = c.CurrentSurfaceUnityMatchCount,
            SameCurrentOceanUnity = c.SameCurrentOceanUnity, SameCurrentSphereUnity = c.SameCurrentSphereUnity,
            RegisteredSurfaceReferenceMatchCount = c.RegisteredSurfaceReferenceMatchCount,
            RegisteredSurfaceUnityMatchCount = c.RegisteredSurfaceUnityMatchCount,
            UniqueRegisteredSimulatorOrdinal = c.UniqueRegisteredSimulatorOrdinal,
            UniqueRegisteredSurfaceIndex = c.UniqueRegisteredSurfaceIndex, IsOtherRegisteredSurface = c.IsOtherRegisteredSurface,
            Enabled = c.Enabled, ActiveInHierarchy = c.ActiveInHierarchy, Layer = c.Layer, IsTrigger = c.IsTrigger,
            SharedMeshPresent = c.SharedMeshPresent, SameDefaultPhysicsScene = c.SameDefaultPhysicsScene,
            WorldPosition = Vector(c.WorldPosition), WorldRotation = Rotation(c.WorldRotation), WorldLossyScale = Vector(c.WorldLossyScale),
        }).ToList(),
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

/// <summary>Copied hit scalars. Unity equality is diagnostic; admission retains exact live managed references separately.</summary>
public sealed class NativeTerrainFootHitPhysicsSnapshot
{
    public string State { get; set; } = "unavailable";
    public string? ReasonType { get; set; }
    public string? Type { get; set; }
    // Zero-based only when exactly one current surface slot matches; otherwise unknown.
    public int? UniqueSurfaceIndex { get; set; }
    public bool? IsCurrentSurface { get; set; }
    public bool? SameCurrentOcean { get; set; }
    public bool? SameCurrentSphere { get; set; }
    public bool? UnknownOwner { get; set; }
    public int? CurrentSurfaceUnityMatchCount { get; set; }
    public bool? SameCurrentOceanUnity { get; set; }
    public bool? SameCurrentSphereUnity { get; set; }
    public int? RegisteredSurfaceReferenceMatchCount { get; set; }
    public int? RegisteredSurfaceUnityMatchCount { get; set; }
    public int? UniqueRegisteredSimulatorOrdinal { get; set; }
    public int? UniqueRegisteredSurfaceIndex { get; set; }
    public bool? IsOtherRegisteredSurface { get; set; }
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

/// <summary>One bounded, already registered local-star simulator; no world identifiers or resource data.</summary>
public sealed class NativeRegisteredTerrainOwnerPhysicsSnapshot
{
    public int SimulatorOrdinal { get; set; }
    public string State { get; set; } = "unavailable";
    public string? ReasonCode { get; set; }
    public bool? SameLocalStar { get; set; }
    public bool? SlotMatchesPlanet { get; set; }
    public bool? SimulatorMatchesPlanet { get; set; }
    public bool? PlanetMeshArrayMatches { get; set; }
    public bool? IsCurrentPlanet { get; set; }
    public bool? IsSolid { get; set; }
    public bool? Loaded { get; set; }
    public bool? Loading { get; set; }
    public bool? FactoryLoading { get; set; }
    public NativePlanetTerrainPhysicsSnapshot? Terrain { get; set; }
    public NativeTerrainColliderPhysicsSnapshot? Sphere { get; set; }
    public float? SphereRadius { get; set; }
    public Vector3Snapshot? SphereCenter { get; set; }
    public NativeTerrainColliderPhysicsSnapshot? Ocean { get; set; }
    public bool? OceanPresent { get; set; }
    public int? ExpectedOceanLayerAfterUniverse { get; set; }
    public bool? ExpectedOceanTriggerAfterUniverse { get; set; }
    public bool? SphereFootClearAfterUniverse { get; set; }
    public bool? PoseMatchesNativeAfterUniverse { get; set; }

    public NativeRegisteredTerrainOwnerPhysicsSnapshot Copy() => new()
    {
        SimulatorOrdinal = SimulatorOrdinal, State = State, ReasonCode = ReasonCode,
        SameLocalStar = SameLocalStar, SlotMatchesPlanet = SlotMatchesPlanet, SimulatorMatchesPlanet = SimulatorMatchesPlanet,
        PlanetMeshArrayMatches = PlanetMeshArrayMatches, IsCurrentPlanet = IsCurrentPlanet, IsSolid = IsSolid,
        Loaded = Loaded, Loading = Loading, FactoryLoading = FactoryLoading, Terrain = Terrain?.Copy(),
        Sphere = CopyCollider(Sphere), SphereRadius = SphereRadius,
        SphereCenter = SphereCenter is null ? null : new() { X = SphereCenter.X, Y = SphereCenter.Y, Z = SphereCenter.Z },
        Ocean = CopyCollider(Ocean), OceanPresent = OceanPresent,
        ExpectedOceanLayerAfterUniverse = ExpectedOceanLayerAfterUniverse, SphereFootClearAfterUniverse = SphereFootClearAfterUniverse,
        ExpectedOceanTriggerAfterUniverse = ExpectedOceanTriggerAfterUniverse,
        PoseMatchesNativeAfterUniverse = PoseMatchesNativeAfterUniverse,
    };
    private static NativeTerrainColliderPhysicsSnapshot? CopyCollider(NativeTerrainColliderPhysicsSnapshot? c) => c is null ? null : new()
    {
        Type = c.Type, Enabled = c.Enabled, ActiveInHierarchy = c.ActiveInHierarchy, Layer = c.Layer,
        IsTrigger = c.IsTrigger, SharedMeshPresent = c.SharedMeshPresent, SameDefaultPhysicsScene = c.SameDefaultPhysicsScene,
        WorldPosition = c.WorldPosition is null ? null : new() { X = c.WorldPosition.X, Y = c.WorldPosition.Y, Z = c.WorldPosition.Z },
        WorldRotation = c.WorldRotation is null ? null : new() { X = c.WorldRotation.X, Y = c.WorldRotation.Y, Z = c.WorldRotation.Z, W = c.WorldRotation.W },
        WorldLossyScale = c.WorldLossyScale is null ? null : new() { X = c.WorldLossyScale.X, Y = c.WorldLossyScale.Y, Z = c.WorldLossyScale.Z },
    };
}
