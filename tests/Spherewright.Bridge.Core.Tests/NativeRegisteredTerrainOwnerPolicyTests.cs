using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeRegisteredTerrainOwnerPolicyTests
{
    [Fact]
    public void CompleteCurrentContactAndOneExactReadyNonlocalOwnerAreOnlyNecessaryPreflightEvidence()
    {
        var terrain = Preflight();
        Assert.NotNull(NativeTerrainObservationPolicy.FootBlocker(terrain));
        Assert.Null(NativeRegisteredTerrainOwnerPolicy.PreflightBlocker(terrain));
        Assert.False(terrain.SafetyCertified);
        Assert.Null(terrain.RegisteredPostPhysicsVerified);
    }

    [Theory]
    [InlineData("no_current")] [InlineData("unknown_hit")] [InlineData("unity_only")]
    [InlineData("ambiguous_hit")] [InlineData("duplicate_owner")] [InlineData("null_owner")]
    [InlineData("wrong_star")] [InlineData("wrong_slot")] [InlineData("wrong_simulator")]
    [InlineData("wrong_native_mesh_array")] [InlineData("unloaded")] [InlineData("loading")]
    [InlineData("factory_loading")] [InlineData("gas")] [InlineData("already_enabled_sphere")]
    [InlineData("missing_sphere")] [InlineData("nonfinite_radius")] [InlineData("trigger")]
    [InlineData("scene")] [InlineData("nonfinite_pose")] [InlineData("incomplete_owner")]
    [InlineData("unregistered")] [InlineData("too_many_slots")] [InlineData("too_many_surfaces")]
    [InlineData("truncated_hits")] [InlineData("missing_registry")] [InlineData("wrong_ordinal")]
    public void UnprovedOrIncoherentOwnerCannotFundTheOneFrame(string fault)
    {
        var t = Preflight(); var o = t.RegisteredTerrainOwners![0]; var hit = t.FootHits![1];
        switch (fault)
        {
            case "no_current": t.FootCurrentSurfaceHitCount = 0; t.FootHits.RemoveAt(0); t.FootOverlapCount = 1; break;
            case "unknown_hit": hit.UnknownOwner = true; break;
            case "unity_only": hit.RegisteredSurfaceReferenceMatchCount = 0; hit.RegisteredSurfaceUnityMatchCount = 1; break;
            case "ambiguous_hit": hit.RegisteredSurfaceReferenceMatchCount = 2; break;
            case "duplicate_owner": t.RegisteredTerrainOwners.Add(o.Copy()); break;
            case "null_owner": t.RegisteredTerrainOwners[0] = null!; break;
            case "wrong_star": o.SameLocalStar = false; break;
            case "wrong_slot": o.SlotMatchesPlanet = false; break;
            case "wrong_simulator": o.SimulatorMatchesPlanet = false; break;
            case "wrong_native_mesh_array": o.PlanetMeshArrayMatches = false; break;
            case "unloaded": o.Loaded = false; break;
            case "loading": o.Loading = true; break;
            case "factory_loading": o.FactoryLoading = true; break;
            case "gas": o.IsSolid = false; break;
            case "already_enabled_sphere": o.Sphere!.Enabled = true; break;
            case "missing_sphere": o.Sphere = null; break;
            case "nonfinite_radius": o.SphereRadius = float.NaN; break;
            case "trigger": o.Terrain!.SurfaceColliders![0].IsTrigger = true; break;
            case "scene": o.Sphere!.SameDefaultPhysicsScene = false; break;
            case "nonfinite_pose": o.Terrain!.RootWorldPosition!.X = float.NaN; break;
            case "incomplete_owner": o.Terrain!.Complete = false; break;
            case "unregistered": hit.UniqueRegisteredSimulatorOrdinal = 2; break;
            case "too_many_slots": t.RegisteredSimulatorCount = 17; break;
            case "too_many_surfaces": o.Terrain!.SurfaceColliders = Enumerable.Range(0, 257).Select(_ => Collider("mesh", 31, true)).ToList(); break;
            case "truncated_hits": t.FootOverlapComplete = false; break;
            case "missing_registry": t.RegisteredTerrainOwnersComplete = null; break;
            case "wrong_ordinal": o.SimulatorOrdinal = -1; break;
        }
        Assert.NotNull(NativeRegisteredTerrainOwnerPolicy.PreflightBlocker(t));
    }

    [Fact]
    public void UnityEqualityDiagnosticsCannotGrantUnmatchedReferencePermissionOrAlterLegacyAdmission()
    {
        var t = Preflight(); t.FootHits![1].RegisteredSurfaceReferenceMatchCount = 0;
        t.FootHits[1].RegisteredSurfaceUnityMatchCount = 1;
        Assert.NotNull(NativeRegisteredTerrainOwnerPolicy.PreflightBlocker(t));
        t = Preflight(); t.FootOverlapsOwnedSurfaceOnly = true;
        t.RegisteredSimulatorCount = 100; t.RegisteredTerrainOwnersComplete = false;
        t.RegisteredTerrainOwners = null; t.FootHits = null;
        Assert.Null(NativeTerrainObservationPolicy.FootBlocker(t));
        Assert.Null(NativeRegisteredTerrainOwnerPolicy.PreflightBlocker(t));
    }

    [Fact]
    public void RealPostUniverseRequiresNativePoseDisabledNonlocalSurfacesAndCurrentOnlyContact()
    {
        var t = PostUniverse(); var o = PostOwner();
        Assert.Null(NativeRegisteredTerrainOwnerPolicy.PostUniverseFootBlocker(t));
        Assert.Null(NativeRegisteredTerrainOwnerPolicy.OwnerPostUniverseBlocker(o, 2));
        o.SphereFootClearAfterUniverse = null;
        Assert.NotNull(NativeRegisteredTerrainOwnerPolicy.OwnerPostUniverseBlocker(o, 2));
        t.FootOverlapsOwnedSurfaceOnly = false;
        Assert.NotNull(NativeRegisteredTerrainOwnerPolicy.PostUniverseFootBlocker(t));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NonHitOrdinaryOceanKeepsItsKnownPrefabTriggerWhileNativeDisablesIt(bool trigger)
    {
        var before = Owner(); before.Ocean!.IsTrigger = trigger; before.ExpectedOceanTriggerAfterUniverse = trigger;
        Assert.Null(NativeRegisteredTerrainOwnerPolicy.OwnerPreflightBlocker(before, 2));
        var after = PostOwner(); after.Ocean!.IsTrigger = trigger; after.ExpectedOceanTriggerAfterUniverse = trigger;
        Assert.Null(NativeRegisteredTerrainOwnerPolicy.OwnerPostUniverseBlocker(after, 2));
        after.Ocean.IsTrigger = !trigger;
        Assert.NotNull(NativeRegisteredTerrainOwnerPolicy.OwnerPostUniverseBlocker(after, 2));
    }
    [Fact]
    public void IceNativeBranchRequiresObservedFalseTriggerAndUnknownOceanStateCannotPass()
    {
        var o = PostOwner(); o.ExpectedOceanLayerAfterUniverse = 9; o.Ocean!.Layer = 9;
        o.ExpectedOceanTriggerAfterUniverse = false; o.Ocean.IsTrigger = false;
        Assert.Null(NativeRegisteredTerrainOwnerPolicy.OwnerPostUniverseBlocker(o, 2));
        o.Ocean.IsTrigger = true; Assert.NotNull(NativeRegisteredTerrainOwnerPolicy.OwnerPostUniverseBlocker(o, 2));
        o.Ocean.IsTrigger = false; o.ExpectedOceanTriggerAfterUniverse = null;
        Assert.NotNull(NativeRegisteredTerrainOwnerPolicy.OwnerPostUniverseBlocker(o, 2));
        var before = Owner(); before.Ocean!.IsTrigger = null;
        Assert.NotNull(NativeRegisteredTerrainOwnerPolicy.OwnerPreflightBlocker(before, 2));
    }

    [Theory]
    [InlineData("surface_still_enabled")] [InlineData("ocean_still_enabled")] [InlineData("sphere_not_enabled")]
    [InlineData("sphere_at_foot")] [InlineData("wrong_native_pose")] [InlineData("unknown_native_pose")]
    [InlineData("wrong_layer")] [InlineData("wrong_ocean_layer")] [InlineData("nonfinite")]
    [InlineData("owner_changed")] [InlineData("mesh_changed")] [InlineData("unknown_disable")]
    public void AReal151NotificationDoesNotReplaceItsRequiredPostState(string fault)
    {
        var o = PostOwner();
        switch (fault)
        {
            case "surface_still_enabled": o.Terrain!.SurfaceColliders![0].Enabled = true; break;
            case "ocean_still_enabled": o.Ocean!.Enabled = true; break;
            case "sphere_not_enabled": o.Sphere!.Enabled = false; break;
            case "sphere_at_foot": o.SphereFootClearAfterUniverse = false; break;
            case "wrong_native_pose": o.PoseMatchesNativeAfterUniverse = false; break;
            case "unknown_native_pose": o.PoseMatchesNativeAfterUniverse = null; break;
            case "wrong_layer": o.Terrain!.BodyLayer = 31; break;
            case "wrong_ocean_layer": o.Ocean!.Layer = 9; break;
            case "nonfinite": o.Sphere!.WorldRotation!.W = float.NaN; break;
            case "owner_changed": o.SimulatorMatchesPlanet = false; break;
            case "mesh_changed": o.PlanetMeshArrayMatches = false; break;
            case "unknown_disable": o.Terrain!.SurfaceColliders![0].Enabled = null; break;
        }
        Assert.NotNull(NativeRegisteredTerrainOwnerPolicy.OwnerPostUniverseBlocker(o, 2));
    }

    [Theory]
    [InlineData("miss")] [InlineData("unobserved")] [InlineData("mixed")] [InlineData("overflow")]
    public void PostUniverseCurrentFootMustBeFreshCompleteAndPositive(string fault)
    {
        var t = PostUniverse();
        if (fault == "miss") t.NativeGroundSphereHit = false;
        if (fault == "unobserved") t.NativeGroundSphereHit = null;
        if (fault == "mixed") t.FootOverlapsOwnedSurfaceOnly = false;
        if (fault == "overflow") { t.FootOverlapCount = 257; t.FootOverlapComplete = false; }
        Assert.NotNull(NativeRegisteredTerrainOwnerPolicy.PostUniverseFootBlocker(t));
    }

    internal static NativePlanetTerrainPhysicsSnapshot Preflight()
    {
        var t = Terrain(31); t.NativeGroundSphereHit = false; t.GuideComplete = true;
        t.FootOverlapCount = 2; t.FootOverlapComplete = true; t.FootOverlapsOwnedSurfaceOnly = false;
        t.FootHitDetailsComplete = true; t.RegisteredSimulatorCount = 2; t.RegisteredTerrainOwnersComplete = true;
        t.FootCurrentSurfaceHitCount = 1; t.FootRegisteredSurfaceHitCount = 1; t.FootOverlapsCurrentOrRegisteredSurfaceOnly = true;
        t.FootHits = new() { Hit(true), Hit(false) }; t.RegisteredTerrainOwners = new() { Owner() }; return t;
    }
    private static NativePlanetTerrainPhysicsSnapshot PostUniverse()
    {
        var t = Terrain(9); t.NativeGroundSphereHit = true; t.GuideComplete = true;
        t.FootOverlapCount = 1; t.FootOverlapComplete = true; t.FootOverlapsOwnedSurfaceOnly = true; return t;
    }
    private static NativeRegisteredTerrainOwnerPhysicsSnapshot Owner() => new()
    {
        SimulatorOrdinal = 1, State = "observed", SameLocalStar = true, SlotMatchesPlanet = true, SimulatorMatchesPlanet = true,
        PlanetMeshArrayMatches = true, IsCurrentPlanet = false, IsSolid = true, Loaded = true, Loading = false, FactoryLoading = false,
        Terrain = Terrain(31), Sphere = Collider("sphere", 31, false), SphereRadius = 200, SphereCenter = new(), OceanPresent = true,
        Ocean = Collider("mesh", 31, true), ExpectedOceanLayerAfterUniverse = 4, ExpectedOceanTriggerAfterUniverse = false,
    };
    private static NativeRegisteredTerrainOwnerPhysicsSnapshot PostOwner()
    {
        var o = Owner(); o.Terrain = Terrain(9); o.Terrain.SurfaceColliders![0].Enabled = false;
        o.Sphere = Collider("sphere", 9, true); o.Ocean = Collider("mesh", 4, false);
        o.SphereFootClearAfterUniverse = true; o.PoseMatchesNativeAfterUniverse = true; return o;
    }
    private static NativeTerrainFootHitPhysicsSnapshot Hit(bool current) => new()
    {
        State = "observed", Type = "mesh", UnknownOwner = false, IsCurrentSurface = current, IsOtherRegisteredSurface = !current,
        UniqueSurfaceIndex = current ? 0 : null, RegisteredSurfaceReferenceMatchCount = 1,
        UniqueRegisteredSimulatorOrdinal = current ? 0 : 1, UniqueRegisteredSurfaceIndex = 0,
        Layer = 31, Enabled = true, ActiveInHierarchy = true, IsTrigger = false, SharedMeshPresent = true, SameDefaultPhysicsScene = true,
        WorldPosition = new(), WorldRotation = Identity(), WorldLossyScale = One(),
    };
    private static NativePlanetTerrainPhysicsSnapshot Terrain(int layer) => new()
    {
        State = "observed", Complete = true, RootLayer = layer == 31 ? 31 : 0, BodyLayer = layer,
        RootWorldPosition = new(), RootWorldRotation = Identity(), RootWorldLossyScale = One(),
        BodyWorldPosition = new(), BodyWorldRotation = Identity(), BodyWorldLossyScale = One(),
        SurfaceColliders = new() { Collider("mesh", layer, true) },
    };
    private static NativeTerrainColliderPhysicsSnapshot Collider(string type, int layer, bool enabled) => new()
    {
        Type = type, Layer = layer, Enabled = enabled, ActiveInHierarchy = true, IsTrigger = false,
        SharedMeshPresent = type == "mesh" ? true : null, SameDefaultPhysicsScene = true,
        WorldPosition = new(), WorldRotation = Identity(), WorldLossyScale = One(),
    };
    private static Vector3Snapshot One() => new() { X = 1, Y = 1, Z = 1 };
    private static QuaternionSnapshot Identity() => new() { W = 1 };
}
