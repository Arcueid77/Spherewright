using Spherewright.Bridge.Core.Celestial;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal static class DysonSwarmOrbitNativeSnapshot
{
    // Snapshot the existing pool only; do not create a sphere, orbit, sail or bullet.
    internal static DysonSwarmOrbitState? Capture(DysonSphere sphere, DysonStateSnapshot result)
    {
        var swarm = sphere.swarm;
        if (swarm is null || !ReferenceEquals(swarm.dysonSphere, sphere)
            || !ReferenceEquals(swarm.gameData, sphere.gameData) || !ReferenceEquals(swarm.starData, sphere.starData)
            || swarm.orbits is null || swarm.orbitCapacity != swarm.orbits.Length || swarm.orbitCapacity < 32
            || swarm.orbitCapacity > DysonObservationPolicy.MaximumPoolSlots
            || swarm.orbitCursor < 1 || swarm.orbitCursor > DysonSwarmOrbitPolicy.MaximumOrdinaryOrbitId + 1
            || swarm.bulletPool is null || swarm.bulletCursor < 1 || swarm.bulletCursor > swarm.bulletPool.Length
            || swarm.bulletCursor > DysonObservationPolicy.MaximumPoolSlots
            || swarm.bulletRecycleCursor < 0 || swarm.bulletRecycleCursor >= swarm.bulletCursor
            || swarm.sailCount < 0 || swarm.orbits[DysonSwarmOrbitPolicy.ReservedNativeOrbitId].id != DysonSwarmOrbitPolicy.ReservedNativeOrbitId)
            return null;
        var state = new DysonSwarmOrbitState
        {
            NativeBindingVerified = true, CoverageComplete = true, Cursor = swarm.orbitCursor, Capacity = swarm.orbitCapacity,
            MinimumRadius = sphere.minOrbitRadius, MaximumRadius = sphere.maxOrbitRadius, AvoidRadius = sphere.avoidOrbitRadius,
            ActiveSailCount = swarm.sailCount, InFlightSailCount = swarm.bulletCursor - swarm.bulletRecycleCursor - 1,
            ReservedOrbit = CopySwarmOrbit(swarm.orbits[DysonSwarmOrbitPolicy.ReservedNativeOrbitId], result.StarId),
        };
        for (var id = 1; id <= DysonSwarmOrbitPolicy.MaximumOrdinaryOrbitId; id++)
        {
            var native = swarm.orbits[id];
            if (native.id == 0)
            {
                if (native.count != 0) return null;
                continue;
            }
            if (id >= swarm.orbitCursor || native.id != id) return null;
            state.Orbits.Add(CopySwarmOrbit(native, result.StarId));
        }
        return DysonSwarmOrbitPolicy.Complete(result.SessionId, result.PlanetId, result.StarId, state) ? state : null;
    }

    private static SwarmOrbitSnapshot CopySwarmOrbit(SailOrbit native, int starId)
    {
        var result = new SwarmOrbitSnapshot { OrbitId = native.id, Enabled = native.enabled, Radius = native.radius,
            SailCount = native.count, Rotation = new QuaternionSnapshot
            { X = native.rotation.x, Y = native.rotation.y, Z = native.rotation.z, W = native.rotation.w },
            Up = new Vector3Snapshot { X = native.up.x, Y = native.up.y, Z = native.up.z } };
        result.StateHash = EjectorOrbitConfigurationPolicy.OrbitHash(starId, result);
        return result;
    }
}
