using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    internal MovementGeometrySnapshot CaptureMovementGeometry(Player player, Vector3 target, double radius,
        string playerStateHash, bool requireIdleWalk)
    {
        var result = new MovementGeometrySnapshot
        {
            SessionId = _sessions.SessionId!, PlanetId = player.planetId, CapturedAtGameTick = GameMain.gameTick,
            Origin = CaptureVector(player.position), EffectiveTarget = CaptureVector(target),
            RadiusMetres = radius, PlayerStateHash = playerStateHash,
        };
        try
        {
            var planet = GameMain.localPlanet;
            var factory = planet?.factory;
            var body = player.controller?.rigid;
            if (planet is null || factory is null || body == null || !ReferenceEquals(player.factory, factory)
                || !ReferenceEquals(planet, player.planetData) || !ReferenceEquals(factory, GameMain.data.localLoadedPlanetFactory)
                || !ReferenceEquals(factory.planet.physics?.planet, planet) || planet.gameObject == null
                || radius <= 0 || radius > WalkingRoutePlanner.MaximumGoalDistanceMetres
                || Physics.simulationMode == SimulationMode.Script || !Physics.autoSyncTransforms
                || !body.gameObject.scene.GetPhysicsScene().Equals(Physics.defaultPhysicsScene))
                throw new InvalidOperationException("walking_physics_context_unavailable");
            if (!MovementFinite(player.position) || !MovementFinite(target) || player.position.magnitude < 120)
                throw new InvalidOperationException("walking_player_pose_unavailable");
            var transform = planet.gameObject.transform;
            if (transform.position.sqrMagnitude > 1e-8 || Quaternion.Angle(transform.rotation, Quaternion.identity) > .001f
                || (transform.lossyScale - Vector3.one).sqrMagnitude > 1e-8)
                throw new InvalidOperationException("walking_planet_frame_noncanonical");
            if (requireIdleWalk && (player.movementState != EMovementState.Walk || player.speed > .05f
                || body.velocity.sqrMagnitude > .0025f || player.currentOrder is not null || player.orders.orderCount != 0))
                throw new InvalidOperationException("walking_preview_requires_stationary_walk");
            var actorColliders = body.GetComponentsInChildren<Collider>(true).Where(collider => collider != null
                && collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger
                && ReferenceEquals(collider.attachedRigidbody, body)).ToArray();
            if (actorColliders.Length is < 1 or > 16 || !body.detectCollisions)
                throw new InvalidOperationException("walking_actor_collider_coverage_unavailable");
            CaptureWalkingBody(player.position, actorColliders, result.Body);
            var actorLayers = actorColliders.Select(collider => collider.gameObject.layer).Distinct().ToArray();
            var physics = planet.physics;
            var chunks = physics.colChunks;
            if (chunks is null || chunks.Length != 600 || physics.nearColliderLogic is null
                || !ReferenceEquals(physics.nearColliderLogic.planet, planet))
                throw new InvalidOperationException("walking_native_chunk_table_unavailable");
            var scanned = 0;
            var actorHeight = result.Body.Max(shape => Math.Max(Math.Abs(shape.LowerCentreHeight), Math.Abs(shape.UpperCentreHeight)))
                + result.Body.Max(shape => shape.Radius);
            for (var chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                var chunk = chunks[chunkIndex];
                if (chunk?.colliderPool is null || chunk.cursor < 1 || chunk.cursor > chunk.colliderPool.Length
                    || chunk.cursor > 0x100000)
                    throw new InvalidOperationException("walking_native_pool_unavailable");
                scanned += chunk.cursor - 1;
                if (scanned > 262144) throw new InvalidOperationException("walking_native_scan_limit");
                for (var index = 1; index < chunk.cursor; index++)
                {
                    var data = chunk.colliderPool[index];
                    if (data.idType == 0 || data.usage != EColliderUsage.Physics && data.usage != EColliderUsage.Belt) continue;
                    var bound = data.ext.magnitude + data.radius;
                    if (!MovementFinite(data.pos) || !MovementFinite(data.ext) || !FiniteMovement(data.radius)
                        || data.radius < 0 || !FiniteMovement(bound))
                        throw new InvalidOperationException("walking_native_collider_nonfinite");
                    if (Vector3.Distance(data.pos, player.position) > radius + actorHeight + 4 + bound) continue;
                    var layer = NativeMovementLayer(data.objType);
                    if (layer < 0)
                    {
                        AddMovementBlocker(result, "walking_dynamic_or_unknown_native_object");
                        continue;
                    }
                    if (actorLayers.All(actorLayer => Physics.GetIgnoreLayerCollision(actorLayer, layer))) continue;
                    var id = (chunkIndex << 20) | index;
                    if (!MovementObjectOwnsCollider(factory, physics, id, data, out var kind))
                    {
                        AddMovementBlocker(result, "walking_collider_owner_unavailable");
                        continue;
                    }
                    if (result.Colliders.Count == WalkingRoutePlanner.MaximumColliders)
                        throw new InvalidOperationException("walking_collider_output_limit");
                    var shape = data.shape == EColliderShape.Box ? "box"
                        : data.shape == EColliderShape.Sphere ? "sphere"
                        : data.shape == EColliderShape.Capsule ? "capsule" : "unknown";
                    if (shape == "unknown") throw new InvalidOperationException("walking_collider_shape_unavailable");
                    var rotationNorm = data.q.x * data.q.x + data.q.y * data.q.y + data.q.z * data.q.z + data.q.w * data.q.w;
                    if (shape == "box" && (data.ext.x <= 0 || data.ext.y <= 0 || data.ext.z <= 0
                            || !FiniteMovement(rotationNorm) || Math.Abs(rotationNorm - 1) > .001)
                        || shape != "box" && data.radius <= 0)
                        throw new InvalidOperationException("walking_native_shape_invalid");
                    result.Colliders.Add(new WalkingColliderSnapshot
                    {
                        NativeColliderId = id, ObjectKind = kind,
                        ObjectId = data.objType == EObjectType.Prebuild ? -data.objId : data.objId,
                        Shape = shape, Centre = CaptureVector(data.pos), Extents = CaptureVector(data.ext),
                        Rotation = CaptureQuaternion(data.q), Radius = data.radius,
                    });
                }
            }
            var simulator = planet.gameObject.GetComponent<PlanetSimulator>();
            if (simulator == null || !ReferenceEquals(simulator.planetData, planet)
                || simulator.surfaceCollider is not { Length: > 0 and <= 256 })
                throw new InvalidOperationException("walking_terrain_owner_unavailable");
            CaptureWalkingSceneCoverage(player, actorColliders, actorLayers, simulator, result);
            var ground = CaptureWalkingTerrain(planet, player.position, 0, 0, simulator);
            if (ground.GroundRadiusMetres.HasValue)
                result.GroundOffsetMetres = player.position.magnitude - ground.GroundRadiusMetres.Value;
            if (requireIdleWalk && (!ground.Observed || !ground.Dry || result.GroundOffsetMetres < -.05
                || result.GroundOffsetMetres > .5))
                AddMovementBlocker(result, "walking_origin_not_on_observed_dry_terrain");
            var factoryHash = string.Empty;
            if (requireIdleWalk)
            {
                using var tool = NativeFactoryGeometry.Open(factory);
                factoryHash = NativeFactoryGeometry.Fingerprint(factory, _sessions.SessionId!, tool, out _, out _);
            }
            var fields = new List<object?> { _sessions.SessionId, factory.planetId, factoryHash,
                player.position.x, player.position.y, player.position.z, target.x, target.y, target.z, radius,
                result.GroundOffsetMetres };
            foreach (var shape in result.Body)
            {
                fields.Add(shape.LowerCentreHeight); fields.Add(shape.UpperCentreHeight); fields.Add(shape.Radius);
            }
            foreach (var collider in result.Colliders)
            {
                fields.Add(collider.NativeColliderId); fields.Add(collider.ObjectKind); fields.Add(collider.ObjectId);
                fields.Add(collider.Shape); fields.Add(collider.Centre.X); fields.Add(collider.Centre.Y); fields.Add(collider.Centre.Z);
                fields.Add(collider.Extents.X); fields.Add(collider.Extents.Y); fields.Add(collider.Extents.Z);
                fields.Add(collider.Rotation.X); fields.Add(collider.Rotation.Y); fields.Add(collider.Rotation.Z); fields.Add(collider.Rotation.W);
                fields.Add(collider.Radius);
            }
            result.GeometryHash = CanonicalStateHash.Combine("native-walking-geometry-v1", fields.ToArray());
            result.Complete = result.Blockers.Count == 0;
            result.State = result.Complete ? "observed" : "partial";
        }
        catch (Exception error)
        {
            AddMovementBlocker(result, error is InvalidOperationException ? error.Message : "walking_capture_" + error.GetType().Name);
            result.State = "partial";
        }
        return result;
    }

    private static void CaptureWalkingBody(Vector3 position, IReadOnlyList<Collider> colliders, List<WalkingBodyCapsule> result)
    {
        var up = position.normalized;
        foreach (var collider in colliders)
        {
            var scale = collider.transform.lossyScale;
            Vector3 centre;
            double radius;
            double half;
            if (collider is CapsuleCollider capsule)
            {
                if (capsule.direction != 1 || Vector3.Dot(collider.transform.up, up) < .9999f)
                    throw new InvalidOperationException("walking_actor_capsule_axis_unavailable");
                centre = collider.transform.TransformPoint(capsule.center);
                radius = capsule.radius * Math.Max(Math.Abs(scale.x), Math.Abs(scale.z));
                half = Math.Max(0, capsule.height * Math.Abs(scale.y) / 2 - radius);
            }
            else if (collider is SphereCollider sphere)
            {
                centre = collider.transform.TransformPoint(sphere.center);
                radius = sphere.radius * Math.Max(Math.Abs(scale.x), Math.Max(Math.Abs(scale.y), Math.Abs(scale.z)));
                half = 0;
            }
            else throw new InvalidOperationException("walking_actor_shape_unavailable");
            var offset = centre - position;
            var height = Vector3.Dot(offset, up);
            if (!MovementFinite(centre) || Vector3.ProjectOnPlane(offset, up).magnitude > .01f
                || !FiniteMovement(height) || Math.Abs(height) + half > 8
                || !FiniteMovement(radius) || radius <= 0 || radius > 4 || !FiniteMovement(half) || half > 8)
                throw new InvalidOperationException("walking_actor_dimensions_unavailable");
            result.Add(new WalkingBodyCapsule
            {
                LowerCentreHeight = height - half, UpperCentreHeight = height + half, Radius = radius,
            });
        }
    }

    private static void CaptureWalkingSceneCoverage(Player player, IReadOnlyList<Collider> actor,
        IReadOnlyList<int> actorLayers, PlanetSimulator simulator, MovementGeometrySnapshot result)
    {
        var mask = 0;
        for (var layer = 0; layer < 32; layer++)
            if (actorLayers.Any(actorLayer => !Physics.GetIgnoreLayerCollision(actorLayer, layer))) mask |= 1 << layer;
        var hits = new Collider[WalkingRoutePlanner.MaximumColliders];
        var count = Physics.OverlapSphereNonAlloc(player.position, (float)result.RadiusMetres + 12,
            hits, mask, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) AddMovementBlocker(result, "walking_scene_query_truncated");
        var physics = GameMain.localPlanet.physics;
        for (var index = 0; index < count; index++)
        {
            var hit = hits[index];
            if (hit == null || actor.Contains(hit) || ReferenceEquals(hit.attachedRigidbody, player.controller.rigid)
                || simulator.surfaceCollider.Any(surface => ReferenceEquals(surface, hit))
                || ReferenceEquals(simulator.sphereCollider, hit) || ReferenceEquals(simulator.oceanCollider, hit)) continue;
            if (!physics.GetColliderData(hit, out var data) || data.usage != EColliderUsage.Physics && data.usage != EColliderUsage.Belt)
                AddMovementBlocker(result, "walking_foreign_scene_collider");
        }
        foreach (var capsule in result.Body)
        {
            var up = player.position.normalized;
            count = Physics.OverlapCapsuleNonAlloc(player.position + up * (float)capsule.LowerCentreHeight,
                player.position + up * (float)capsule.UpperCentreHeight, (float)capsule.Radius + .02f,
                hits, mask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) AddMovementBlocker(result, "walking_contact_query_truncated");
            for (var index = 0; index < count; index++)
            {
                if (hits[index] == null || !physics.GetColliderData(hits[index], out var data)) continue;
                foreach (var candidate in result.Colliders.Where(candidate => SameMovementCollider(candidate, data)))
                    candidate.CurrentSceneProximityObserved = true;
            }
        }
    }

    internal List<WalkingTerrainPoint> CaptureWalkingTerrainPoints(Player player, MovementGeometrySnapshot geometry,
        IReadOnlyList<Vector3Snapshot> points)
    {
        var planet = GameMain.localPlanet;
        var simulator = planet.gameObject.GetComponent<PlanetSimulator>();
        return points.Select((point, index) =>
        {
            var requested = new Vector3(point.X, point.Y, point.Z);
            return Vector3.Distance(requested, player.position) <= geometry.RadiusMetres + .05
                ? CaptureWalkingTerrain(planet, requested, index, geometry.GroundOffsetMetres, simulator)
                : new WalkingTerrainPoint { Index = index, RequestedPosition = point, ReasonCode = "walking_sample_outside_geometry_cut" };
        }).ToList();
    }

    private static WalkingTerrainPoint CaptureWalkingTerrain(PlanetData planet, Vector3 point, int index,
        double footOffset, PlanetSimulator simulator)
    {
        var result = new WalkingTerrainPoint { Index = index, RequestedPosition = CaptureVector(point) };
        var up = point.normalized;
        var ray = new Ray(up * (planet.realRadius + 80), -up);
        if (!Physics.Raycast(ray, out var ground, 160, 8704, QueryTriggerInteraction.Ignore)
            || !simulator.surfaceCollider.Any(surface => ReferenceEquals(surface, ground.collider)))
        {
            result.ReasonCode = "walking_ground_ray_owner_unavailable";
            return result;
        }
        var waterHit = Physics.Raycast(ray, out var water, 160, 16, QueryTriggerInteraction.Ignore);
        if (planet.waterItemId > 0 && (simulator.oceanCollider == null || !simulator.oceanCollider.enabled
            || !simulator.oceanCollider.gameObject.activeInHierarchy || !waterHit))
        {
            result.ReasonCode = "walking_water_coverage_unavailable";
            return result;
        }
        if (waterHit && !ReferenceEquals(water.collider, simulator.oceanCollider))
        {
            result.ReasonCode = "walking_water_owner_unavailable";
            return result;
        }
        result.Observed = MovementFinite(ground.point) && MovementFinite(ground.normal);
        result.GroundRadiusMetres = ground.point.magnitude;
        result.WaterRadiusMetres = waterHit ? water.point.magnitude : null;
        result.GroundNormalDotUp = Vector3.Dot(ground.normal, up);
        result.Dry = result.Observed && (!waterHit || result.GroundRadiusMetres - result.WaterRadiusMetres >= .1);
        result.Position = CaptureVector(up * (float)(ground.point.magnitude + footOffset));
        return result;
    }

    private static bool MovementObjectOwnsCollider(PlanetFactory factory, PlanetPhysics physics, int colliderId,
        ColliderData data, out string kind)
    {
        kind = string.Empty;
        var id = data.objId;
        int root;
        if (data.objType == EObjectType.Entity && id > 0 && id < factory.entityCursor
            && id < factory.entityPool.Length && factory.entityPool[id].id == id)
        { kind = "entity"; root = factory.entityPool[id].colliderId; }
        else if (data.objType == EObjectType.Prebuild && id > 0 && id < factory.prebuildCursor
            && id < factory.prebuildPool.Length && factory.prebuildPool[id].id == id)
        { kind = "prebuild"; root = factory.prebuildPool[id].colliderId; }
        else if (data.objType == EObjectType.Vegetable && id > 0 && id < factory.vegeCursor
            && id < factory.vegePool.Length && factory.vegePool[id].id == id)
        { kind = "vegetation"; root = factory.vegePool[id].colliderId; }
        else if (data.objType == EObjectType.Vein && id > 0 && id < factory.veinCursor
            && id < factory.veinPool.Length && factory.veinPool[id].id == id)
        { kind = "vein"; root = factory.veinPool[id].colliderId; }
        else return false;
        var seen = new HashSet<int>();
        while (root > 0 && seen.Count < 64 && seen.Add(root))
        {
            var chunk = root >> 20;
            var offset = root & 0xFFFFF;
            if (chunk >= physics.colChunks.Length || offset <= 0) return false;
            var container = physics.colChunks[chunk];
            if (container?.colliderPool is null || container.cursor < 1 || container.cursor > container.colliderPool.Length
                || offset >= container.cursor) return false;
            var current = container.colliderPool[offset];
            if (current.objId != id || current.objType != data.objType || current.idType == 0) return false;
            if (root == colliderId) return true;
            root = current.link;
        }
        return false;
    }

    private static int NativeMovementLayer(EObjectType kind) => kind == EObjectType.Vegetable ? 10
        : kind == EObjectType.Vein ? 11 : kind == EObjectType.Prebuild ? 1 : kind == EObjectType.Entity ? 12 : -1;

    private static bool SameMovementCollider(WalkingColliderSnapshot candidate, ColliderData data) =>
        candidate.ObjectId == (data.objType == EObjectType.Prebuild ? -data.objId : data.objId)
        && candidate.ObjectKind == (data.objType == EObjectType.Entity ? "entity" : data.objType == EObjectType.Prebuild ? "prebuild"
            : data.objType == EObjectType.Vegetable ? "vegetation" : data.objType == EObjectType.Vein ? "vein" : "unknown")
        && (new Vector3(candidate.Centre.X, candidate.Centre.Y, candidate.Centre.Z) - data.pos).sqrMagnitude < 1e-8;

    private static bool FiniteMovement(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool MovementFinite(Vector3 value) => FiniteMovement(value.x) && FiniteMovement(value.y) && FiniteMovement(value.z);
    private static void AddMovementBlocker(MovementGeometrySnapshot result, string reason)
    {
        if (result.Blockers.Count < 64 && !result.Blockers.Contains(reason)) result.Blockers.Add(reason);
    }
}
