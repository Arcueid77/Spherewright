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

    internal static string? CaptureOwnedTerrainFootOverlap(Player player, NativePlanetTerrainPhysicsSnapshot terrain,
        Collider[] ownedSurface)
    {
        var blocker = NativeTerrainObservationPolicy.Blocker(terrain, 31);
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
            terrain.FootOverlapsOwnedSurfaceOnly = count > 0 && count < results.Length
                && results.Take(count).All(c => c != null && c is MeshCollider mesh && mesh.sharedMesh != null
                    && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && c.gameObject.layer == 31
                    && ownedSurface.Any(o => ReferenceEquals(c, o))
                    && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded
                    && c.gameObject.scene.GetPhysicsScene().Equals(Physics.defaultPhysicsScene));
            return NativeTerrainObservationPolicy.FootBlocker(terrain);
        }
        catch (Exception exception) { return "remote_resume_first_frame_foot_query_failed:" + exception.GetType().Name; }
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
