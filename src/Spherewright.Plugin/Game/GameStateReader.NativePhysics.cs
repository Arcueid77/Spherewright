using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Players;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    // Called solely by the existing main-thread player capture. No queries of
    // world colliders, simulation, material instantiation or Native writes.
    private static NativePlayerPhysicsSnapshot CaptureNativePlayerPhysicsOnMainThread(Player player)
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
