using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Players;
using Spherewright.Bridge.Core.Safety;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    // Only copied parameters and the bounded Native foot query. Never synchronizes or simulates physics.
    private NativePlayerPhysicsSnapshot CaptureNativePlayerPhysicsOnMainThread(Player player)
    {
        var result = new NativePlayerPhysicsSnapshot();
        try
        {
            result.UnityVersion = Application.unityVersion;
            result.FixedDeltaTimeSeconds = NativePhysicsFinite(Time.fixedDeltaTime);
            // Read the exact installed legacy getter, without changing simulation mode.
#pragma warning disable CS0618
            result.AutoSimulation = Physics.autoSimulation;
#pragma warning restore CS0618
            result.SimulationMode = Physics.simulationMode.ToString();
            result.AutoSyncTransforms = Physics.autoSyncTransforms;
            result.GroundContact = CaptureNativeGroundContact(player, "current_player_read");
            result.Terrain = CaptureNativeTerrain(player, "current_player_read");
            _sessions.CopyRemoteResumeGroundDiagnostics(player, result);
            result.Gravity = NativePhysicsVector(Physics.gravity);
            var controller = player.controller;
            var body = controller?.rigid;
            if (body == null) { result.ReasonCode = "native_player_rigidbody_unavailable"; return result; }
            var nativeGravity = controller!.universalGravity;
            result.NativeGravity = new UniversalPositionSnapshot
            { X = NativePhysicsDouble(nativeGravity.x), Y = NativePhysicsDouble(nativeGravity.y), Z = NativePhysicsDouble(nativeGravity.z) };
            result.Velocity = NativePhysicsVector(body.velocity);
            result.UseGravity = body.useGravity;
            result.IsKinematic = body.isKinematic;
            result.DetectCollisions = body.detectCollisions;
            result.Constraints = body.constraints.ToString();
            result.CollisionDetectionMode = body.collisionDetectionMode.ToString();
            result.MaxDepenetrationVelocity = NativePhysicsFinite(body.maxDepenetrationVelocity);
            result.MaxLinearVelocity = NativePhysicsFinite(body.maxLinearVelocity);
            result.SolverIterations = body.solverIterations;
            result.SolverVelocityIterations = body.solverVelocityIterations;
            result.Drag = NativePhysicsFinite(body.drag);
            result.Mass = NativePhysicsFinite(body.mass);
            result.Sleeping = body.IsSleeping();
            var scene = body.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded) { result.ReasonCode = "native_player_physics_scene_unavailable"; return result; }
            result.SameDefaultPhysicsScene = scene.GetPhysicsScene().Equals(Physics.defaultPhysicsScene);
            var colliders = new List<NativePlayerColliderPhysicsSnapshot>();
            foreach (var collider in body.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy
                    || !ReferenceEquals(collider.attachedRigidbody, body)) continue;
                if (colliders.Count == 16)
                {
                    result.CollidersComplete = false; result.CollidersTruncated = true;
                    result.ReasonCode = "native_player_collider_limit_exceeded"; return result;
                }
                var row = new NativePlayerColliderPhysicsSnapshot
                {
                    IsTrigger = collider.isTrigger, Layer = collider.gameObject.layer,
                    AttachedRigidbodyMatches = true,
                    SameDefaultPhysicsScene = collider.gameObject.scene.GetPhysicsScene().Equals(Physics.defaultPhysicsScene),
                    WorldPosition = NativePhysicsVector(collider.transform.position),
                    WorldRotation = NativePhysicsRotation(collider.transform.rotation),
                    WorldLossyScale = NativePhysicsVector(collider.transform.lossyScale),
                };
                if (collider is CapsuleCollider capsule)
                {
                    row.Shape = "capsule"; row.LocalCenter = NativePhysicsVector(capsule.center);
                    row.Radius = NativePhysicsFinite(capsule.radius); row.Height = NativePhysicsFinite(capsule.height);
                    row.CapsuleDirection = capsule.direction;
                }
                else if (collider is SphereCollider sphere)
                { row.Shape = "sphere"; row.LocalCenter = NativePhysicsVector(sphere.center); row.Radius = NativePhysicsFinite(sphere.radius); }
                else if (collider is BoxCollider box)
                { row.Shape = "box"; row.LocalCenter = NativePhysicsVector(box.center); row.Size = NativePhysicsVector(box.size); }
                else
                { row.Shape = collider is MeshCollider ? "mesh" : "other"; row.ShapeReasonCode = "simple_dimensions_unavailable"; }
                var material = collider.sharedMaterial; // .material would instantiate: never use it here.
                if (material == null) row.MaterialReasonCode = "shared_material_null";
                else
                {
                    row.Material = new NativePlayerPhysicsMaterialSnapshot
                    {
                        Bounciness = NativePhysicsFinite(material.bounciness),
                        StaticFriction = NativePhysicsFinite(material.staticFriction),
                        DynamicFriction = NativePhysicsFinite(material.dynamicFriction),
                        FrictionCombine = material.frictionCombine.ToString(), BounceCombine = material.bounceCombine.ToString(),
                    };
                    row.MaterialState = "observed";
                }
                colliders.Add(row);
            }
            result.Colliders = colliders; result.CollidersComplete = true; result.CollidersTruncated = false;
            if (colliders.Any(row => row.ShapeReasonCode is not null)) result.ReasonCode = "native_player_collider_shape_unavailable";
            else result.State = "observed";
        }
        catch (ArithmeticException) { result.ReasonCode = "non_finite_native_physics_value"; }
        catch (Exception) { result.ReasonCode = "native_physics_read_failed"; }
        return result;
    }

    internal static NativePlayerGroundContactSnapshot CaptureNativeGroundContact(Player player, string stage,
        bool? observedSphereHit = null) => NativeGroundContactObservationPolicy.Capture(GameMain.gameTick, stage,
        result =>
        {
            var p = player.position;
            var planet = GameMain.localPlanet;
            if (planet is null || planet.type == EPlanetType.Gas || !planet.loaded || planet.loading
                || !planet.factoryLoaded || planet.factoryLoading || planet.factory is null || planet.gameObject == null
                || !ReferenceEquals(player.planetData, planet) || !ReferenceEquals(player.factory, planet.factory)
                || player.controller?.actionWalk is null || !player.gameObject.scene.IsValid()
                || !player.gameObject.scene.isLoaded || !player.gameObject.scene.GetPhysicsScene().Equals(Physics.defaultPhysicsScene)
                || float.IsNaN(p.sqrMagnitude) || float.IsInfinity(p.sqrMagnitude) || p.sqrMagnitude <= 1f)
                throw new InvalidOperationException("native_ground_query_context_unavailable");
            result.AutoSyncTransforms = Physics.autoSyncTransforms; result.QueriesHitTriggers = Physics.queriesHitTriggers;
            result.CachedWalkIsGrounded = player.controller.actionWalk.isGrounded;
            var cached = player.controller.actionWalk.groundDist;
            result.CachedWalkGroundDistance = float.IsNaN(cached) || float.IsInfinity(cached) ? null : cached;
        },
        () => observedSphereHit ?? Physics.CheckSphere(player.position + player.position.normalized * .15f, .35f, 15873),
        () =>
        {
            var hit = Physics.SphereCast(new Ray(player.position + player.position.normalized * .35f,
                -player.position.normalized), .35f, out var info, 20f, 15873);
            var c = hit ? info.collider : null;
            return (hit, info.distance, c == null ? null : new NativePlayerGroundColliderSnapshot
            {
                Type = c is MeshCollider ? "mesh" : c is CapsuleCollider ? "capsule" : c is SphereCollider ? "sphere"
                    : c is BoxCollider ? "box" : "other", Layer = c.gameObject.layer, IsTrigger = c.isTrigger, Enabled = c.enabled,
                ActiveInHierarchy = c.gameObject.activeInHierarchy,
                SameDefaultPhysicsScene = c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded
                    && c.gameObject.scene.GetPhysicsScene().Equals(Physics.defaultPhysicsScene),
            });
        });

    private static float NativePhysicsFinite(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArithmeticException();
        return value;
    }

    private static Vector3Snapshot NativePhysicsVector(Vector3 value) => new Vector3Snapshot
    { X = NativePhysicsFinite(value.x), Y = NativePhysicsFinite(value.y), Z = NativePhysicsFinite(value.z) };

    private static QuaternionSnapshot NativePhysicsRotation(Quaternion value) => new QuaternionSnapshot
    { X = NativePhysicsFinite(value.x), Y = NativePhysicsFinite(value.y), Z = NativePhysicsFinite(value.z), W = NativePhysicsFinite(value.w) };

    private static double NativePhysicsDouble(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArithmeticException();
        return value;
    }
}
