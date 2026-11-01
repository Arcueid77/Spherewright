using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    private EjectorStateSnapshot? CaptureEjectorState(PlanetFactory factory, FactoryEntitySnapshot snapshot)
    {
        var id = snapshot.ObjectId;
        var system = factory.factorySystem;
        if (id <= 0 || id >= factory.entityCursor || id >= factory.entityPool.Length || system is null)
            return null;
        var entity = factory.entityPool[id];
        var componentId = entity.ejectorId;
        if (entity.id != id || entity.protoId != 2311 || componentId <= 0
            || componentId >= system.ejectorCursor || componentId >= system.ejectorPool.Length)
            return null;
        var ejector = system.ejectorPool[componentId];
        if (ejector.id != componentId || ejector.entityId != id || ejector.planetId != factory.planetId
            || ejector.chargeSpend <= 0 || ejector.coldSpend <= 0
            || factory.powerSystem?.consumerPool is null || ejector.pcId <= 0
            || ejector.pcId != entity.powerConId || ejector.pcId >= factory.powerSystem.consumerCursor
            || ejector.pcId >= factory.powerSystem.consumerPool.Length
            || factory.powerSystem.consumerPool[ejector.pcId].id != ejector.pcId
            || factory.powerSystem.consumerPool[ejector.pcId].entityId != id
            || ejector.bulletId != 1501 || ejector.bulletCount < 0 || ejector.bulletInc < 0
            || (long)ejector.bulletInc > (long)ejector.bulletCount * 10 || factory.planet?.star is null)
            return null;
        var star = factory.planet.star;
        var result = new EjectorStateSnapshot
        {
            NativeBindingVerified = true, EjectorComponentId = componentId, StarId = star.id, SailItemId = ejector.bulletId,
            SailCount = ejector.bulletCount, SailIncPoints = ejector.bulletInc,
            OrbitId = ejector.orbitId, RuntimeOrbitId = ejector.runtimeOrbitId,
            AutomaticOrbitSelection = ejector.autoOrbit, BoostEnabled = ejector.boost,
            Direction = ejector.direction, CycleProgressRaw = ejector.time,
            ChargeDurationRaw = ejector.chargeSpend, CooldownDurationRaw = ejector.coldSpend,
            NativeTargetState = ejector.targetState.ToString(), FiredAnimationActive = ejector.fired,
        };
        var spheres = GameMain.data?.dysonSpheres;
        // Read existing state only. The ordinary game creates its default orbit;
        // inspection must never instantiate a sphere, factory, orbit or sail.
        var sphere = spheres is not null && star.index >= 0 && star.index < spheres.Length
            ? spheres[star.index] : null;
        var swarm = sphere?.swarm;
        if (swarm is not null && ReferenceEquals(swarm.dysonSphere, sphere) && ReferenceEquals(sphere!.starData, star)
            && swarm.orbits is not null && swarm.orbitCursor >= 1
            && swarm.orbitCursor <= swarm.orbits.Length && swarm.orbitCursor <= 32
            && swarm.bulletCursor >= 1 && swarm.bulletRecycleCursor >= 0
            && swarm.bulletRecycleCursor < swarm.bulletCursor && swarm.sailCount >= 0)
        {
            result.SwarmAvailable = true;
            result.OrbitCoverageComplete = true;
            result.ActiveSailCount = swarm.sailCount;
            result.InFlightSailCount = swarm.bulletCursor - swarm.bulletRecycleCursor - 1;
            for (var i = 1; i < swarm.orbitCursor; i++)
            {
                var native = swarm.orbits[i];
                if (native.id == 0) continue;
                if (native.id != i) { result.OrbitCoverageComplete = false; continue; }
                var orbit = new SwarmOrbitSnapshot { OrbitId = i, Enabled = native.enabled,
                    Radius = native.radius, SailCount = swarm.SailCountOnOrbit(i),
                    Rotation = new QuaternionSnapshot { X = native.rotation.x, Y = native.rotation.y,
                        Z = native.rotation.z, W = native.rotation.w } };
                orbit.StateHash = EjectorOrbitConfigurationPolicy.OrbitHash(star.id, orbit);
                result.Orbits.Add(orbit);
            }
        }
        var count = 0;
        var complete = star.planets is not null;
        foreach (var planet in star.planets ?? Array.Empty<PlanetData>())
        {
            if (planet is null) { complete = false; continue; }
            var index = planet.factoryIndex;
            if (index < 0) continue;
            var data = GameMain.data;
            if (data?.factories is null || index >= data.factoryCount || index >= data.factories.Length
                || data.factories[index] is not { } existing || existing.planetId != planet.id
                || existing.factorySystem is not { } resident || resident.ejectorPool is null
                || resident.ejectorCursor < 1 || resident.ejectorCursor > resident.ejectorPool.Length)
            { complete = false; continue; }
            for (var i = 1; i < resident.ejectorCursor; i++)
            {
                var known = resident.ejectorPool[i];
                if (known.id == 0) continue;
                if (known.id != i || known.entityId <= 0 || known.entityId >= existing.entityCursor
                    || known.entityId >= existing.entityPool.Length
                    || existing.entityPool[known.entityId].ejectorId != i)
                { complete = false; continue; }
                count++;
            }
        }
        result.StarEjectorCoverageComplete = complete;
        if (complete) result.StarEjectorCount = count;
        result.ConfigurationStateHash = EjectorOrbitConfigurationPolicy.ConfigurationHash(snapshot, result);
        return result;
    }
}
