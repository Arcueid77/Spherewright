using System.Text.Json;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Contracts.Tests;

public sealed class NativePlayerPhysicsContractTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void OldPlayerAndUnavailableParametersDoNotInventNativeZeros()
    {
        Assert.Null(JsonSerializer.Deserialize<PlayerStateSnapshot>("{\"planetId\":1}", Options)!.NativePhysics);
        var unknown = new NativePlayerPhysicsSnapshot();
        Assert.Equal("unavailable", unknown.State); Assert.Null(unknown.Velocity);
        Assert.Null(unknown.FixedDeltaTimeSeconds); Assert.Null(unknown.AutoSimulation);
        Assert.Null(unknown.Colliders); Assert.Null(unknown.CollidersComplete);
        Assert.Null(unknown.Mass); Assert.Null(unknown.SolverIterations);
        var fromUntrustedJson = JsonSerializer.Deserialize<NativePlayerPhysicsSnapshot>("{\"safetyCertified\":true}", Options)!;
        Assert.False(fromUntrustedJson.SafetyCertified);
    }

    [Fact]
    public void ObservedParametersRoundTripAsIndependentDetachedData()
    {
        var facts = new NativePlayerPhysicsSnapshot
        {
            State = "observed", UnityVersion = "synthetic-runtime", FixedDeltaTimeSeconds = .02f, AutoSimulation = true,
            SimulationMode = "FixedUpdate", MaxLinearVelocity = 123,
            Gravity = new() { Y = -9.8f }, Velocity = new() { X = 3 }, UseGravity = false, IsKinematic = false,
            NativeGravity = new UniversalPositionSnapshot { X = 1.23456789012345, Y = -49.5, Z = .0000000123456789 },
            DetectCollisions = true, Constraints = "FreezeRotation", CollisionDetectionMode = "Discrete",
            MaxDepenetrationVelocity = 10, SolverIterations = 6, SolverVelocityIterations = 1, Drag = 0,
            Mass = 1, Sleeping = false, SameDefaultPhysicsScene = true, CollidersComplete = true, CollidersTruncated = false,
            Colliders = new() { new() { Shape = "capsule", IsTrigger = false, Layer = 0, AttachedRigidbodyMatches = true,
                SameDefaultPhysicsScene = true, LocalCenter = new() { Y = 1 }, Radius = .5f, Height = 2, CapsuleDirection = 1,
                WorldPosition = new() { X = 100, Y = 2, Z = -3 }, WorldRotation = new() { Z = .6f, W = .8f }, WorldLossyScale = new() { X = 2, Y = 3, Z = 4 },
                MaterialState = "observed", Material = new() { Bounciness = 0, StaticFriction = .6f, DynamicFriction = .5f,
                    FrictionCombine = "Average", BounceCombine = "Maximum" } } },
        };
        var json = JsonSerializer.Serialize(facts, Options);
        var copy = JsonSerializer.Deserialize<NativePlayerPhysicsSnapshot>(json, Options)!;
        Assert.Equal(json, JsonSerializer.Serialize(copy, Options)); Assert.False(copy.SafetyCertified);
        Assert.Equal(1.23456789012345, copy.NativeGravity!.X);
        Assert.Equal("FixedUpdate", copy.SimulationMode); Assert.Equal(123, copy.MaxLinearVelocity);
        Assert.Equal(-49.5, copy.NativeGravity.Y); Assert.Equal(.0000000123456789, copy.NativeGravity.Z);
        Assert.Equal(100, copy.Colliders![0].WorldPosition!.X); Assert.Equal(.8f, copy.Colliders[0].WorldRotation!.W);
        Assert.Equal(3, copy.Colliders[0].WorldLossyScale!.Y);
        facts.Velocity!.X = 99; facts.Colliders![0].Material!.Bounciness = 1; facts.Colliders.Clear();
        Assert.Equal(3, copy.Velocity!.X); Assert.Single(copy.Colliders!); Assert.Equal(0, copy.Colliders![0].Material!.Bounciness);
        Assert.All(typeof(NativePlayerPhysicsSnapshot).Assembly.GetReferencedAssemblies(), a => Assert.DoesNotContain("UnityEngine", a.Name!));
    }

    [Fact]
    public void NullSharedMaterialRemainsUnknownAndNonFiniteNumbersCannotCrossStrictJson()
    {
        var collider = new NativePlayerColliderPhysicsSnapshot { Shape = "box", Size = new Vector3Snapshot { X = 1, Y = 2, Z = 3 },
            MaterialReasonCode = "shared_material_null" };
        var copy = JsonSerializer.Deserialize<NativePlayerColliderPhysicsSnapshot>(JsonSerializer.Serialize(collider, Options), Options)!;
        Assert.Null(copy.Material); Assert.Equal("unknown", copy.MaterialState); Assert.Equal("shared_material_null", copy.MaterialReasonCode);
        Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(new NativePlayerPhysicsSnapshot { Mass = float.NaN }, Options));
        Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(new NativePlayerPhysicsSnapshot { Velocity = new() { X = float.PositiveInfinity } }, Options));
        Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(new NativePlayerPhysicsSnapshot { NativeGravity = new() { Z = double.NegativeInfinity } }, Options));
    }
}
