using Spherewright.Bridge.Core.Celestial;
using Spherewright.Contracts.Celestial;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal static class DysonSwarmOrbitNativeExecution
{
    // The caller has already proved the owned world/session and retains its
    // action/idempotency receipt. This operation cannot grant either authority.
    internal static bool Execute(DysonSphere sphere, DysonSwarm swarm, SailOrbit[] orbitPool, SailBullet[] bulletPool,
        DysonSwarmOrbitState before, DysonSwarmOrbitPlan plan, DysonSwarmOrbitReadback readback)
    {
        var scope = new DysonStateSnapshot { SessionId = plan.SessionId, PlanetId = plan.PlanetId, StarId = plan.StarId };
        if (!ReferenceEquals(sphere.swarm, swarm) || !ReferenceEquals(swarm.orbits, orbitPool) || !ReferenceEquals(swarm.bulletPool, bulletPool))
            return false;
        var fresh = DysonSwarmOrbitNativeSnapshot.Capture(sphere, scope);
        if (fresh is null || fresh.StateHash != before.StateHash || fresh.StateHash != plan.BeforeStateHash
            || DysonSwarmOrbitPolicy.NextOrbitId(fresh) != plan.PlannedOrbitId) return false;
        var q = plan.NativeRotation;
        readback.NativeOperationInvoked = true;
        var id = swarm.NewOrbit(plan.NativeRadius, new Quaternion(q.X, q.Y, q.Z, q.W));
        readback.OrbitId = id;
        if (id != plan.PlannedOrbitId || !ReferenceEquals(sphere.swarm, swarm)
            || !ReferenceEquals(swarm.orbits, orbitPool) || !ReferenceEquals(swarm.bulletPool, bulletPool)) return false;
        var after = DysonSwarmOrbitNativeSnapshot.Capture(sphere, scope);
        if (after is null) return false;
        readback.AfterStateHash = after.StateHash;
        var created = after.Orbits.SingleOrDefault(o => o.OrbitId == id);
        readback.ExactNewOrbitVerified = DysonSwarmOrbitPolicy.MatchesNewOrbit(created, plan);
        readback.ExistingOrbitsAndSailCountsPreserved = DysonSwarmOrbitPolicy.ExistingOrbitsAndSailCountsPreserved(before, after, id);
        return readback.ExactNewOrbitVerified && readback.ExistingOrbitsAndSailCountsPreserved;
    }
}
