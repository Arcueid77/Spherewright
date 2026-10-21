using Spherewright.Contracts.Players;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>A one-frame observation latch; it never grants idle, skips a task or pays energy.</summary>
public sealed class NativeFirstFrameAdmission
{
    public bool Armed { get; private set; }
    public bool UniverseObserved { get; private set; }
    public bool PhysicsObserved { get; private set; }
    public long SourceGameTick { get; private set; }
    public string? Failure { get; private set; }
    public string? FailureDetail { get; private set; }
    private Func<string?>? _recheckAtPhysics;
    public string? Arm(long sourceTick, bool exactLoader, bool guideComplete, bool terrainOwned31FootHit, bool funded,
        Func<string?>? recheckAtPhysics = null)
        => ArmBound(sourceTick, exactLoader, guideComplete, terrainOwned31FootHit, funded, recheckAtPhysics);

    // Separate evidence kind: a current-pool support cast is not a terrain foot
    // hit or a landing. The Plugin retains it and owns the finite contact guard.
    public string? ArmNearSupport(long sourceTick, bool exactLoader, bool guideComplete, bool exactCurrentSupport, bool funded,
        Func<string?> recheckAtPhysics)
        => recheckAtPhysics is null ? Fail("remote_resume_near_support_recheck_unavailable")
            : ArmBound(sourceTick, exactLoader, guideComplete, exactCurrentSupport, funded, recheckAtPhysics);

    private string? ArmBound(long sourceTick, bool exactLoader, bool guideComplete, bool supportProved, bool funded,
        Func<string?>? recheckAtPhysics)
    {
        if (Failure is not null) return Failure;
        if (Armed || sourceTick < 0 || sourceTick == long.MaxValue || !exactLoader || !guideComplete || !supportProved || !funded)
            return Fail("remote_resume_first_frame_preflight_unproved");
        SourceGameTick = sourceTick; _recheckAtPhysics = recheckAtPhysics; Armed = true; return null;
    }
    public string? ObserveUniverse(long tick, bool exactBinding, bool unchangedPlayer, bool expectedTerrain)
        => ObserveUniverse(tick, exactBinding, unchangedPlayer, expectedTerrain, null, null, null);
    public string? ObserveUniverse(long tick, bool exactBinding, bool unchangedPlayer, bool expectedTerrain,
        string? bindingBlocker, string? playerBlocker, string? terrainBlocker)
    {
        if (Failure is not null) return Failure;
        // Retain the first decision, not a later explanatory re-capture. Detail
        // cannot turn a false premise into success or replace a prior failure.
        var detail = !Armed ? "universe_not_armed"
            : UniverseObserved ? "universe_already_observed"
            : PhysicsObserved ? "universe_after_physics"
            : tick != SourceGameTick + 1 ? "universe_tick_changed"
            : !exactBinding ? bindingBlocker ?? "universe_binding_unproved"
            : !unchangedPlayer ? playerBlocker ?? "universe_player_unproved"
            : !expectedTerrain ? terrainBlocker ?? "universe_terrain_unproved" : null;
        if (detail is not null) return Fail("remote_resume_first_frame_universe_unproved", detail);
        UniverseObserved = true; return null;
    }
    public string? ObservePhysics(long tick, bool exactBinding)
    {
        if (Failure is not null) return Failure;
        var detail = !Armed ? "physics_not_armed"
            : !UniverseObserved ? "physics_before_universe"
            : PhysicsObserved ? "physics_already_observed"
            : tick != SourceGameTick + 1 ? "physics_tick_changed"
            : !exactBinding ? "physics_binding_unproved" : null;
        if (detail is not null) return Fail("remote_resume_first_frame_physics_unproved", detail);
        try
        {
            var retained = _recheckAtPhysics?.Invoke();
            if (retained is not null) return Fail(retained);
        }
        catch (Exception exception) { return Fail("remote_resume_first_frame_physics_capture_unavailable:" + exception.GetType().Name); }
        if (Failure is not null) return Failure;
        PhysicsObserved = true; return null;
    }
    public string Fail(string code) => Fail(code, null);
    public string Fail(string code, string? detail)
    {
        if (Failure is not null) return Failure;
        FailureDetail = detail;
        return Failure = code;
    }
}

public static class NativeTerrainObservationPolicy
{
    public const int MaximumSurfaceColliders = 256;
    public static string? Blocker(NativePlanetTerrainPhysicsSnapshot s, int expectedLayer)
    {
        if (s.State != "observed" || !s.Complete || s.Truncated || s.SurfaceColliders is not { Count: > 0 and <= MaximumSurfaceColliders } colliders)
            return "remote_resume_first_frame_terrain_incomplete";
        if (!Vector(s.RootWorldPosition) || !Rotation(s.RootWorldRotation) || !Scale(s.RootWorldLossyScale)
            || !Vector(s.BodyWorldPosition) || !Rotation(s.BodyWorldRotation) || !Scale(s.BodyWorldLossyScale))
            return "remote_resume_first_frame_terrain_pose_unproved";
        if (s.RootLayer != (expectedLayer == 31 ? 31 : 0) || s.BodyLayer != expectedLayer)
            return "remote_resume_first_frame_terrain_layer_unproved";
        foreach (var c in colliders)
            if (c is null || c.Type != "mesh" || c.Enabled != true || c.ActiveInHierarchy != true || c.Layer != expectedLayer
                || c.IsTrigger != false || c.SharedMeshPresent != true || c.SameDefaultPhysicsScene != true
                || !Vector(c.WorldPosition) || !Rotation(c.WorldRotation) || !Scale(c.WorldLossyScale))
                return "remote_resume_first_frame_terrain_membership_unproved";
        return null;
    }
    public static string? FootBlocker(NativePlanetTerrainPhysicsSnapshot s) => Blocker(s, 31)
        ?? (s.NativeGroundSphereHit == false && s.GuideComplete == true && s.FootOverlapComplete == true
            && s.FootOverlapsOwnedSurfaceOnly == true && s.FootOverlapCount is > 0 and <= MaximumSurfaceColliders
            ? null : "remote_resume_first_frame_owned_foot_unproved");
    private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    private static bool Vector(Vector3Snapshot? p) => p is not null && Finite(p.X) && Finite(p.Y) && Finite(p.Z);
    private static bool Scale(Vector3Snapshot? p) => Vector(p) && p!.X > 0 && p.Y > 0 && p.Z > 0;
    private static bool Rotation(QuaternionSnapshot? q) => q is not null && Finite(q.X) && Finite(q.Y) && Finite(q.Z) && Finite(q.W)
        && q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W is > .99f and < 1.01f;
}

/// <summary>Necessary copied-value checks; the Plugin must also retain and recheck the exact live Native references.</summary>
public static class NativeRegisteredTerrainOwnerPolicy
{
    public const int MaximumRegisteredSimulators = 16;

    public static string? PreflightBlocker(NativePlanetTerrainPhysicsSnapshot s)
    {
        // Preserve the original all-current route. New diagnostic fields cannot change that decision.
        if (NativeTerrainObservationPolicy.FootBlocker(s) is null) return null;
        var geometry = NativeTerrainObservationPolicy.Blocker(s, 31);
        if (geometry is not null) return geometry;
        if (s.NativeGroundSphereHit != false || s.GuideComplete != true || s.FootOverlapComplete != true
            || s.FootOverlapCount is not (> 0 and <= NativeTerrainObservationPolicy.MaximumSurfaceColliders)
            || s.RegisteredSimulatorCount is not (> 0 and <= MaximumRegisteredSimulators)
            || s.RegisteredTerrainOwnersComplete != true || s.FootOverlapsCurrentOrRegisteredSurfaceOnly != true
            || s.FootHits is null || s.FootHits.Count != s.FootOverlapCount
            || s.RegisteredTerrainOwners is not { Count: > 0 and < MaximumRegisteredSimulators } owners)
            return "remote_resume_registered_terrain_foot_unproved";
        if (owners.Any(o => o is null) || owners.Select(o => o.SimulatorOrdinal).Distinct().Count() != owners.Count)
            return "remote_resume_registered_terrain_ambiguous";
        foreach (var owner in owners)
        {
            var blocker = OwnerPreflightBlocker(owner, s.RegisteredSimulatorCount.Value);
            if (blocker is not null) return blocker;
        }
        var current = 0; var other = 0;
        foreach (var hit in s.FootHits)
        {
            if (hit is null || hit.State != "observed" || !Collider(hit, 31) || hit.UnknownOwner != false
                || hit.RegisteredSurfaceReferenceMatchCount != 1)
                return "remote_resume_registered_terrain_hit_unproved";
            if (hit.IsCurrentSurface == true && hit.IsOtherRegisteredSurface == false
                && hit.UniqueSurfaceIndex is >= 0 && hit.UniqueSurfaceIndex < s.SurfaceColliders!.Count) current++;
            else if (hit.IsCurrentSurface == false && hit.IsOtherRegisteredSurface == true
                && hit.UniqueRegisteredSimulatorOrdinal is { } ordinal && hit.UniqueRegisteredSurfaceIndex is >= 0
                && owners.SingleOrDefault(o => o.SimulatorOrdinal == ordinal) is { } owner
                && hit.UniqueRegisteredSurfaceIndex < owner.Terrain!.SurfaceColliders!.Count) other++;
            else return "remote_resume_registered_terrain_hit_unproved";
        }
        if (current < 1 || other < 1 || current != s.FootCurrentSurfaceHitCount || other != s.FootRegisteredSurfaceHitCount
            || owners.Any(o => !s.FootHits.Any(h => h.IsOtherRegisteredSurface == true && h.UniqueRegisteredSimulatorOrdinal == o.SimulatorOrdinal)))
            return "remote_resume_registered_terrain_current_contact_unproved";
        return null;
    }

    public static string? OwnerPreflightBlocker(NativeRegisteredTerrainOwnerPhysicsSnapshot owner, int registeredCount)
    {
        if (registeredCount is < 1 or > MaximumRegisteredSimulators || owner.SimulatorOrdinal < 0 || owner.SimulatorOrdinal >= registeredCount
            || owner.State != "observed" || owner.SameLocalStar != true || owner.SlotMatchesPlanet != true
            || owner.SimulatorMatchesPlanet != true || owner.PlanetMeshArrayMatches != true || owner.IsCurrentPlanet != false
            || owner.IsSolid != true || owner.Loaded != true || owner.Loading != false || owner.FactoryLoading != false
            || owner.Terrain is null || NativeTerrainObservationPolicy.Blocker(owner.Terrain, 31) is not null)
            return "remote_resume_registered_terrain_owner_unproved";
        // Native nonlocal flag2=false only enters its disable branch when the sphere is disabled.
        if (!Collider(owner.Sphere, "sphere", 31, false) || !Finite(owner.SphereRadius) || owner.SphereRadius <= 0
            || !Vector(owner.SphereCenter) || owner.OceanPresent is null
            || (owner.OceanPresent == true && !Collider(owner.Ocean, null, 31, null, allowKnownTrigger: true))
            || (owner.OceanPresent == false && owner.Ocean is not null))
            return "remote_resume_registered_terrain_disable_branch_unproved";
        return null;
    }

    public static string? PostUniverseFootBlocker(NativePlanetTerrainPhysicsSnapshot s) =>
        NativeTerrainObservationPolicy.Blocker(s, 9)
        ?? (s.NativeGroundSphereHit == true && s.GuideComplete == true && s.FootOverlapComplete == true
            && s.FootOverlapsOwnedSurfaceOnly == true
            && s.FootOverlapCount is > 0 and <= NativeTerrainObservationPolicy.MaximumSurfaceColliders
                ? null : "remote_resume_registered_terrain_post_universe_contact_unproved");

    public static string? OwnerPostUniverseBlocker(NativeRegisteredTerrainOwnerPhysicsSnapshot owner, int registeredCount)
    {
        if (registeredCount is < 1 or > MaximumRegisteredSimulators || owner.SimulatorOrdinal < 0 || owner.SimulatorOrdinal >= registeredCount
            || owner.State != "observed" || owner.SameLocalStar != true || owner.SlotMatchesPlanet != true
            || owner.SimulatorMatchesPlanet != true || owner.PlanetMeshArrayMatches != true || owner.IsCurrentPlanet != false
            || owner.IsSolid != true || owner.Loaded != true || owner.Loading != false || owner.FactoryLoading != false
            || owner.PoseMatchesNativeAfterUniverse != true
            || owner.Terrain is not { State: "observed", Complete: true, Truncated: false, RootLayer: 0, BodyLayer: 9 } t
            || !Vector(t.RootWorldPosition) || !Rotation(t.RootWorldRotation) || !Scale(t.RootWorldLossyScale)
            || !Vector(t.BodyWorldPosition) || !Rotation(t.BodyWorldRotation) || !Scale(t.BodyWorldLossyScale)
            || t.SurfaceColliders is not { Count: > 0 and <= NativeTerrainObservationPolicy.MaximumSurfaceColliders } surfaces
            || surfaces.Any(c => !Collider(c, "mesh", 9, false)))
            return "remote_resume_registered_terrain_post_universe_owner_unproved";
        if (!Collider(owner.Sphere, "sphere", 9, true) || !Finite(owner.SphereRadius) || owner.SphereRadius <= 0
            || !Vector(owner.SphereCenter) || owner.SphereFootClearAfterUniverse != true || owner.OceanPresent is null
            || (owner.OceanPresent == true && (owner.ExpectedOceanLayerAfterUniverse is not (4 or 9)
                || owner.ExpectedOceanTriggerAfterUniverse is null || owner.Ocean?.IsTrigger != owner.ExpectedOceanTriggerAfterUniverse
                || !Collider(owner.Ocean, null, owner.ExpectedOceanLayerAfterUniverse.Value, false, allowKnownTrigger: true)))
            || (owner.OceanPresent == false && owner.Ocean is not null))
            return "remote_resume_registered_terrain_post_universe_disable_unproved";
        return null;
    }

    private static bool Collider(NativeTerrainFootHitPhysicsSnapshot c, int layer) => c.Type == "mesh" && c.Enabled == true
        && c.ActiveInHierarchy == true && c.Layer == layer && c.IsTrigger == false && c.SharedMeshPresent == true
        && c.SameDefaultPhysicsScene == true && Vector(c.WorldPosition) && Rotation(c.WorldRotation) && Scale(c.WorldLossyScale);
    private static bool Collider(NativeTerrainColliderPhysicsSnapshot? c, string? type, int layer, bool? enabled, bool allowKnownTrigger = false) => c is not null
        && (type is null || c.Type == type) && c.Enabled is not null && (enabled is null || c.Enabled == enabled)
        && c.ActiveInHierarchy == true && c.Layer == layer && (allowKnownTrigger ? c.IsTrigger is not null : c.IsTrigger == false)
        && (c.Type != "mesh" || c.SharedMeshPresent == true) && c.SameDefaultPhysicsScene == true
        && Vector(c.WorldPosition) && Rotation(c.WorldRotation) && Scale(c.WorldLossyScale);
    private static bool Finite(float? v) => v is { } n && !float.IsNaN(n) && !float.IsInfinity(n);
    private static bool Vector(Vector3Snapshot? v) => v is not null && Finite(v.X) && Finite(v.Y) && Finite(v.Z);
    private static bool Scale(Vector3Snapshot? v) => Vector(v) && v!.X > 0 && v.Y > 0 && v.Z > 0;
    private static bool Rotation(QuaternionSnapshot? q) => q is not null && Finite(q.X) && Finite(q.Y) && Finite(q.Z) && Finite(q.W)
        && q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W is > .99f and < 1.01f;
}
