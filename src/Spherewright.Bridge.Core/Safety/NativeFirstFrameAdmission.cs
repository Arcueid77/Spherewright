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
    public string? Arm(long sourceTick, bool exactLoader, bool guideComplete, bool terrainOwned31FootHit, bool funded)
    {
        if (Failure is not null) return Failure;
        if (Armed || sourceTick < 0 || sourceTick == long.MaxValue || !exactLoader || !guideComplete || !terrainOwned31FootHit || !funded)
            return Fail("remote_resume_first_frame_preflight_unproved");
        SourceGameTick = sourceTick; Armed = true; return null;
    }
    public string? ObserveUniverse(long tick, bool exactBinding, bool unchangedPlayer, bool expectedTerrain)
    {
        if (Failure is not null) return Failure;
        if (!Armed || UniverseObserved || PhysicsObserved || tick != SourceGameTick + 1 || !exactBinding || !unchangedPlayer || !expectedTerrain)
            return Fail("remote_resume_first_frame_universe_unproved");
        UniverseObserved = true; return null;
    }
    public string? ObservePhysics(long tick, bool exactBinding)
    {
        if (Failure is not null) return Failure;
        if (!Armed || !UniverseObserved || PhysicsObserved || tick != SourceGameTick + 1 || !exactBinding)
            return Fail("remote_resume_first_frame_physics_unproved");
        PhysicsObserved = true; return null;
    }
    public string Fail(string code) => Failure ??= code;
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
