using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    internal static NativePlanetTerrainPhysicsSnapshot CaptureNativeTerrain(Player player, string stage)
    {
        var result = new NativePlanetTerrainPhysicsSnapshot { CapturedAtGameTick = GameMain.gameTick, CaptureStage = stage };
        try
        {
            var planet = GameMain.localPlanet;
            if (planet is null || planet.type == EPlanetType.Gas || !planet.loaded || planet.loading || !planet.factoryLoaded
                || planet.factoryLoading || planet.factory is null || planet.gameObject == null || planet.bodyObject == null
                || !ReferenceEquals(player.planetData, planet) || !ReferenceEquals(player.factory, planet.factory)
                || !planet.gameObject.scene.IsValid() || !planet.gameObject.scene.isLoaded
                || !planet.gameObject.scene.GetPhysicsScene().Equals(Physics.defaultPhysicsScene))
                throw new InvalidOperationException("context");
            var sim = planet.gameObject.GetComponent<PlanetSimulator>();
            if (sim == null || !ReferenceEquals(sim.planetData, planet) || sim.surfaceCollider is null)
                throw new InvalidOperationException("population");
            if (sim.surfaceCollider.Length < 1 || sim.surfaceCollider.Length > NativeTerrainObservationPolicy.MaximumSurfaceColliders)
            { result.Truncated = sim.surfaceCollider.Length > NativeTerrainObservationPolicy.MaximumSurfaceColliders; return result; }
            result.GuideComplete = GameMain.data.guideComplete;
            result.RootLayer = planet.gameObject.layer; result.BodyLayer = planet.bodyObject.layer;
            result.RootWorldPosition = TerrainVector(planet.gameObject.transform.position);
            result.RootWorldRotation = TerrainRotation(planet.gameObject.transform.rotation);
            result.RootWorldLossyScale = TerrainVector(planet.gameObject.transform.lossyScale);
            CaptureNativeTerrainRootLocalPose(planet.gameObject.transform, result);
            result.BodyWorldPosition = TerrainVector(planet.bodyObject.transform.position);
            result.BodyWorldRotation = TerrainRotation(planet.bodyObject.transform.rotation);
            result.BodyWorldLossyScale = TerrainVector(planet.bodyObject.transform.lossyScale);
            result.SphereColliderEnabled = sim.sphereCollider == null ? null : sim.sphereCollider.enabled;
            result.SphereColliderLayer = sim.sphereCollider == null ? null : sim.sphereCollider.gameObject.layer;
            result.OceanColliderEnabled = sim.oceanCollider == null ? null : sim.oceanCollider.enabled;
            result.OceanColliderLayer = sim.oceanCollider == null ? null : sim.oceanCollider.gameObject.layer;
            var colliders = new List<NativeTerrainColliderPhysicsSnapshot>(sim.surfaceCollider.Length);
            foreach (var c in sim.surfaceCollider)
            {
                if (c == null) throw new InvalidOperationException("collider");
                colliders.Add(new NativeTerrainColliderPhysicsSnapshot
                {
                    Type = c is MeshCollider ? "mesh" : "other", Enabled = c.enabled, ActiveInHierarchy = c.gameObject.activeInHierarchy,
                    Layer = c.gameObject.layer, IsTrigger = c.isTrigger, SharedMeshPresent = c is MeshCollider mesh && mesh.sharedMesh != null,
                    SameDefaultPhysicsScene = c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded
                        && c.gameObject.scene.GetPhysicsScene().Equals(Physics.defaultPhysicsScene),
                    WorldPosition = TerrainVector(c.transform.position), WorldRotation = TerrainRotation(c.transform.rotation),
                    WorldLossyScale = TerrainVector(c.transform.lossyScale),
                });
            }
            result.SurfaceColliders = colliders; result.Complete = true; result.State = "observed";
        }
        catch (Exception exception) { result.ReasonCode = "native_terrain_capture_unavailable:" + exception.GetType().Name; }
        return result;
    }

    // Read only: no SyncTransforms, Native update, normalization or scene write.
    internal static void CaptureNativeTerrainRootLocalPose(Transform root, NativePlanetTerrainPhysicsSnapshot result)
    {
        result.RootWorldPosition = TerrainVector(root.position);
        result.RootWorldRotation = TerrainRotation(root.rotation);
        result.RootWorldLossyScale = TerrainVector(root.lossyScale);
        result.RootHasParent = root.parent != null;
        result.RootLocalPosition = TerrainVector(root.localPosition);
        result.RootLocalRotation = TerrainRotation(root.localRotation);
        result.RootLocalScale = TerrainVector(root.localScale);
        var matrix = root.localToWorldMatrix;
        var entries = new List<float>(16);
        for (var row = 0; row < 4; row++)
            for (var column = 0; column < 4; column++)
            {
                var value = matrix[row, column];
                if (float.IsNaN(value) || float.IsInfinity(value)) return;
                entries.Add(value);
            }
        result.RootLocalToWorldMatrix = entries;
    }

    internal static string? CaptureOwnedTerrainFootOverlap(Player player, NativePlanetTerrainPhysicsSnapshot terrain,
        Collider[] ownedSurface) => CaptureTerrainFootOverlap(player, terrain, ownedSurface, 31, false, out _);

    internal static string? CaptureFirstFrameTerrainFootOverlap(Player player, NativePlanetTerrainPhysicsSnapshot terrain,
        Collider[] ownedSurface, out RegisteredTerrainCapture? registered) =>
        CaptureTerrainFootOverlap(player, terrain, ownedSurface, 31, true, out registered);

    internal static string? CapturePostUniverseTerrainFootOverlap(Player player, NativePlanetTerrainPhysicsSnapshot terrain,
        Collider[] ownedSurface) => CaptureTerrainFootOverlap(player, terrain, ownedSurface, 9, false, out _);

    private static string? CaptureTerrainFootOverlap(Player player, NativePlanetTerrainPhysicsSnapshot terrain,
        Collider[] ownedSurface, int expectedLayer, bool allowRegistered, out RegisteredTerrainCapture? registered)
    {
        registered = null;
        terrain.FootHits = null; terrain.FootHitDetailsComplete = null;
        var blocker = NativeTerrainObservationPolicy.Blocker(terrain, expectedLayer);
        if (blocker is not null) return blocker;
        try
        {
            var results = new Collider[NativeTerrainObservationPolicy.MaximumSurfaceColliders + 1];
            // Legal public five-argument overload. Include Native solid layers so
            // mixed/foreign/trigger overlaps cannot masquerade as a31-only hit.
            var count = Physics.OverlapSphereNonAlloc(player.position + player.position.normalized * .15f, .35f,
                results, 15873 | (1 << 31), QueryTriggerInteraction.Collide);
            terrain.FootOverlapCount = count;
            terrain.FootOverlapComplete = count >= 0 && count < results.Length;
            CaptureTerrainFootHitDetails(player, terrain, results, count);
            terrain.FootOverlapsOwnedSurfaceOnly = count > 0 && count < results.Length
                && results.Take(count).All(c => c != null && c is MeshCollider mesh && mesh.sharedMesh != null
                    && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && c.gameObject.layer == expectedLayer
                    && ownedSurface.Any(o => ReferenceEquals(c, o))
                    && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded
                    && c.gameObject.scene.GetPhysicsScene().Equals(Physics.defaultPhysicsScene));
            var captured = CaptureRegisteredTerrainFootOwners(player, terrain, results, count);
            if (expectedLayer == 9) return NativeRegisteredTerrainOwnerPolicy.PostUniverseFootBlocker(terrain);
            blocker = NativeTerrainObservationPolicy.FootBlocker(terrain);
            if (blocker is null || !allowRegistered) return blocker;
            blocker = NativeRegisteredTerrainOwnerPolicy.PreflightBlocker(terrain);
            if (blocker is null && captured is not null && captured.ConditionalOwners.Length > 0 && captured.MatchesBeforeUniverse())
            { registered = captured; return null; }
            return blocker ?? "remote_resume_registered_terrain_binding_unproved";
        }
        catch (Exception exception)
        { terrain.FootHitDetailsComplete = false; return "remote_resume_first_frame_foot_query_failed:" + exception.GetType().Name; }
    }

    // This consumes the original query array before its original aggregate guard returns.
    // Current reference membership is preserved separately from Unity-equality diagnostics.
    private static void CaptureTerrainFootHitDetails(Player player, NativePlanetTerrainPhysicsSnapshot terrain,
        Collider[] results, int count)
    {
        Collider[]? surface = null; Collider? ocean = null; Collider? sphere = null;
        string? contextFailure = null;
        try
        {
            var planet = GameMain.localPlanet;
            var sim = planet?.gameObject?.GetComponent<PlanetSimulator>();
            if (planet is null || sim == null || !ReferenceEquals(player.planetData, planet)
                || !ReferenceEquals(player.factory, planet.factory) || !ReferenceEquals(sim.planetData, planet)
                || sim.surfaceCollider is not { Length: > 0 and <= NativeTerrainObservationPolicy.MaximumSurfaceColliders })
                contextFailure = "current_simulator_unavailable";
            else { surface = sim.surfaceCollider; ocean = sim.oceanCollider; sphere = sim.sphereCollider; }
        }
        catch (Exception exception) { contextFailure = exception.GetType().Name; }
        var size = Math.Min(Math.Max(count, 0), results.Length);
        var details = new List<NativeTerrainFootHitPhysicsSnapshot>(size);
        var complete = count >= 0 && count < results.Length && contextFailure is null;
        for (var i = 0; i < size; i++)
        {
            var row = new NativeTerrainFootHitPhysicsSnapshot();
            try
            {
                var c = results[i];
                if (c == null) row.ReasonType = "null_hit";
                else
                {
                    row.Type = c is MeshCollider ? "mesh" : c is SphereCollider ? "sphere"
                        : c is CapsuleCollider ? "capsule" : c is BoxCollider ? "box" : "other";
                    row.Enabled = c.enabled; row.ActiveInHierarchy = c.gameObject.activeInHierarchy;
                    row.Layer = c.gameObject.layer; row.IsTrigger = c.isTrigger;
                    row.SharedMeshPresent = c is MeshCollider mesh ? mesh.sharedMesh != null : (bool?)null;
                    row.SameDefaultPhysicsScene = c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded
                        && c.gameObject.scene.GetPhysicsScene().Equals(Physics.defaultPhysicsScene);
                    row.WorldPosition = TerrainVector(c.transform.position);
                    row.WorldRotation = NativePhysicsRotation(c.transform.rotation);
                    row.WorldLossyScale = TerrainVector(c.transform.lossyScale);
                    if (surface is null) row.ReasonType = contextFailure ?? "current_simulator_unavailable";
                    else
                    {
                        var matches = 0; var slot = -1;
                        for (var j = 0; j < surface.Length; j++)
                            if (ReferenceEquals(c, surface[j])) { matches++; slot = j; }
                        row.IsCurrentSurface = matches > 0;
                        row.UniqueSurfaceIndex = matches == 1 ? slot : (int?)null;
                        row.SameCurrentOcean = ReferenceEquals(c, ocean); row.SameCurrentSphere = ReferenceEquals(c, sphere);
                        row.CurrentSurfaceUnityMatchCount = surface.Count(o => o != null && c == o);
                        row.SameCurrentOceanUnity = ocean != null && c == ocean;
                        row.SameCurrentSphereUnity = sphere != null && c == sphere;
                        row.UnknownOwner = matches == 0 && row.SameCurrentOcean != true && row.SameCurrentSphere != true;
                        if (matches > 1) row.ReasonType = "non_unique_current_surface";
                        else if (row.UnknownOwner == true) row.ReasonType = "owner_outside_current_simulator";
                        else row.State = "observed";
                    }
                }
            }
            catch (Exception exception) { row.ReasonType = exception.GetType().Name; }
            if (row.State != "observed") complete = false;
            details.Add(row);
        }
        terrain.FootHits = details; terrain.FootHitDetailsComplete = complete;
    }

    private static Vector3Snapshot TerrainVector(Vector3 v)
    {
        if (!TerrainFinite(v.x) || !TerrainFinite(v.y) || !TerrainFinite(v.z)) throw new ArithmeticException();
        return new() { X = v.x, Y = v.y, Z = v.z };
    }
    private static QuaternionSnapshot TerrainRotation(Quaternion q)
    {
        if (!TerrainFinite(q.x) || !TerrainFinite(q.y) || !TerrainFinite(q.z) || !TerrainFinite(q.w)
            || !(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w is > .99f and < 1.01f)) throw new ArithmeticException();
        return new() { X = q.x, Y = q.y, Z = q.z, W = q.w };
    }
    private static bool TerrainFinite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
}
