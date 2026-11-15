using Spherewright.Contracts.Celestial;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests
{
    public sealed class DysonSwarmOrbitNativeSnapshotTests
    {
        [Fact]
        public void RealReaderCopiesTheOrdinaryPoolAndReservedOrbitWithoutChangingNativeState()
        {
            var sphere = Native(); var snapshot = DysonSwarmOrbitNativeSnapshot.Capture(sphere, Scope());
            Assert.NotNull(snapshot); Assert.True(snapshot!.CoverageComplete); Assert.Single(snapshot.Orbits);
            Assert.Equal(1, snapshot.Orbits[0].OrbitId); Assert.Equal(2, snapshot.Orbits[0].SailCount);
            Assert.Equal(30, snapshot.ReservedOrbit!.OrbitId); Assert.Equal(1, snapshot.ReservedOrbit.SailCount);
            Assert.Equal(3, snapshot.ActiveSailCount); Assert.Equal(1, snapshot.InFlightSailCount);
            Assert.StartsWith("sha256:", snapshot.StateHash);
            snapshot.Orbits[0].Radius++;
            Assert.Equal(8000, sphere.swarm!.orbits[1].radius);
        }

        [Theory]
        [InlineData("world")] [InlineData("star")] [InlineData("swarm")] [InlineData("pool_length")]
        [InlineData("cursor")] [InlineData("reserved")] [InlineData("wrong_slot")] [InlineData("untracked_slot")]
        [InlineData("nonempty_hole")] [InlineData("bullet_recycle")] [InlineData("bad_rotation")]
        public void MissingOrInconsistentNativePoolCannotProduceCoverage(string fault)
        {
            var sphere = Native(); var swarm = sphere.swarm!;
            switch (fault)
            {
                case "world": swarm.gameData = new object(); break;
                case "star": swarm.starData = new object(); break;
                case "swarm": swarm.dysonSphere = new DysonSphere(); break;
                case "pool_length": swarm.orbitCapacity++; break;
                case "cursor": swarm.orbitCursor = 22; break;
                case "reserved": swarm.orbits[30].id = 0; break;
                case "wrong_slot": swarm.orbits[1].id = 2; break;
                case "untracked_slot": swarm.orbits[2] = Orbit(2, 0, 8000); break;
                case "nonempty_hole": swarm.orbits[2].count = 1; break;
                case "bullet_recycle": swarm.bulletRecycleCursor = swarm.bulletCursor; break;
                case "bad_rotation": swarm.orbits[1].rotation = default; break;
            }
            Assert.Null(DysonSwarmOrbitNativeSnapshot.Capture(sphere, Scope()));
        }

        [Fact]
        public void LinkedNativeExecutionInvokesOneAllocationAndChecksItsActualReadback()
        {
            var sphere = Native(); var swarm = sphere.swarm!; var before = DysonSwarmOrbitNativeSnapshot.Capture(sphere, Scope())!;
            var plan = Plan(before); var readback = new DysonSwarmOrbitReadback();
            Assert.True(DysonSwarmOrbitNativeExecution.Execute(sphere, swarm, swarm.orbits, swarm.bulletPool, before, plan, readback));
            Assert.Equal(1, swarm.NewOrbitCalls); Assert.True(readback.NativeOperationInvoked);
            Assert.Equal(2, readback.OrbitId); Assert.True(readback.ExactNewOrbitVerified);
            Assert.True(readback.ExistingOrbitsAndSailCountsPreserved); Assert.NotEmpty(readback.AfterStateHash);
        }

        [Theory]
        [InlineData("state_changed")] [InlineData("swarm_changed")] [InlineData("pool_changed")]
        public void LinkedNativeExecutionDoesNotInvokeAllocationAfterAnySourceChange(string fault)
        {
            var sphere = Native(); var swarm = sphere.swarm!; var before = DysonSwarmOrbitNativeSnapshot.Capture(sphere, Scope())!;
            var plan = Plan(before); var originalPool = swarm.orbits; var bulletPool = swarm.bulletPool;
            if (fault == "state_changed") swarm.orbits[1].radius++;
            if (fault == "swarm_changed") sphere.swarm = Native().swarm;
            if (fault == "pool_changed") swarm.orbits = swarm.orbits.ToArray();
            var readback = new DysonSwarmOrbitReadback();
            Assert.False(DysonSwarmOrbitNativeExecution.Execute(sphere, swarm, originalPool, bulletPool, before, plan, readback));
            Assert.Equal(0, swarm.NewOrbitCalls); Assert.False(readback.NativeOperationInvoked);
        }

        [Theory]
        [InlineData("wrong_id")] [InlineData("old_modified")] [InlineData("replacement_pool")]
        public void LinkedNativeExecutionRetainsInvocationButRejectsUnprovedResult(string fault)
        {
            var sphere = Native(); var swarm = sphere.swarm!; var before = DysonSwarmOrbitNativeSnapshot.Capture(sphere, Scope())!;
            var plan = Plan(before); swarm.NewOrbitFault = fault; var readback = new DysonSwarmOrbitReadback();
            Assert.False(DysonSwarmOrbitNativeExecution.Execute(sphere, swarm, swarm.orbits, swarm.bulletPool, before, plan, readback));
            Assert.Equal(1, swarm.NewOrbitCalls); Assert.True(readback.NativeOperationInvoked);
        }

        [Fact]
        public void LinkedNativeExecutionDoesNotEraseInvocationEvidenceWhenNativeThrowsAfterMutation()
        {
            var sphere = Native(); var swarm = sphere.swarm!; var before = DysonSwarmOrbitNativeSnapshot.Capture(sphere, Scope())!;
            var plan = Plan(before); swarm.NewOrbitFault = "throw_after"; var readback = new DysonSwarmOrbitReadback();
            Assert.Throws<InvalidOperationException>(() => DysonSwarmOrbitNativeExecution.Execute(
                sphere, swarm, swarm.orbits, swarm.bulletPool, before, plan, readback));
            Assert.Equal(1, swarm.NewOrbitCalls); Assert.True(readback.NativeOperationInvoked);
            Assert.Equal(2, swarm.orbits[2].id);
        }

        private static DysonSphere Native()
        {
            var sphere = new DysonSphere { minOrbitRadius = 4000, maxOrbitRadius = 20000, avoidOrbitRadius = 3000 };
            var swarm = new DysonSwarm { dysonSphere = sphere, gameData = sphere.gameData, starData = sphere.starData,
                orbitCursor = 2, orbitCapacity = 32, orbits = new SailOrbit[32], bulletCursor = 2,
                bulletRecycleCursor = 0, bulletPool = new SailBullet[32], sailCount = 3 };
            swarm.orbits[1] = Orbit(1, 2, 8000); swarm.orbits[30] = Orbit(30, 1, 1);
            sphere.swarm = swarm; return sphere;
        }
        private static SailOrbit Orbit(int id, int count, float radius) => new()
        { id = id, count = count, enabled = true, radius = radius, rotation = new(0, 0, 0, 1), up = new(0, 1, 0) };
        private static DysonStateSnapshot Scope() => new() { SessionId = "session", PlanetId = 7, StarId = 2 };
        private static DysonSwarmOrbitPlan Plan(DysonSwarmOrbitState before) => new()
        {
            SessionId = "session", PlanetId = 7, StarId = 2, BeforeStateHash = before.StateHash,
            PlannedOrbitId = 2, NativeRadius = 9000, NativeRotation = new() { W = 1 }, NativeUp = new() { Y = 1 },
        };
    }
}

// Test-only copies of the Native fields read by the production adapter; they
// do not execute Unity, mutate a real swarm or prove in-game timing.
namespace Spherewright.Plugin.Game
{
    internal sealed class DysonSphere
    {
        internal object gameData = new(), starData = new();
        internal DysonSwarm? swarm;
        internal float minOrbitRadius, maxOrbitRadius, avoidOrbitRadius;
    }
    internal sealed class DysonSwarm
    {
        internal DysonSphere? dysonSphere;
        internal object? gameData, starData;
        internal SailOrbit[] orbits = Array.Empty<SailOrbit>();
        internal int orbitCursor, orbitCapacity, bulletCursor, bulletRecycleCursor, sailCount;
        internal SailBullet[] bulletPool = Array.Empty<SailBullet>();
        internal int NewOrbitCalls;
        internal string NewOrbitFault = "";
        internal int NewOrbit(float radius, UnityEngine.Quaternion rotation)
        {
            NewOrbitCalls++;
            var id = 1;
            while (id < orbitCursor && orbits[id].id != 0) id++;
            orbits[id] = new SailOrbit { id = id, radius = radius, rotation = rotation, up = new(0, 1, 0), enabled = true };
            if (id == orbitCursor) orbitCursor++;
            if (NewOrbitFault == "old_modified") orbits[1].radius++;
            if (NewOrbitFault == "replacement_pool") orbits = orbits.ToArray();
            if (NewOrbitFault == "throw_after") throw new InvalidOperationException("synthetic-native-failure-after-allocation");
            return NewOrbitFault == "wrong_id" ? id + 1 : id;
        }
    }
    internal struct SailOrbit
    {
        internal int id, count;
        internal float radius;
        internal UnityEngine.Quaternion rotation;
        internal UnityEngine.Vector3 up;
        internal bool enabled;
    }
    internal struct SailBullet { }
}
