using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    private SiloStateSnapshot? CaptureSiloState(PlanetFactory factory, FactoryEntitySnapshot snapshot)
    {
        var id = snapshot.ObjectId;
        var system = factory.factorySystem;
        if (factory.entityPool is null || id <= 0 || id >= factory.entityCursor || id >= factory.entityPool.Length
            || system?.siloPool is null) return null;
        var entity = factory.entityPool[id];
        var componentId = entity.siloId;
        if (entity.id != id || entity.protoId != 2312 || componentId <= 0
            || componentId >= system.siloCursor || componentId >= system.siloPool.Length) return null;
        var silo = system.siloPool[componentId];
        if (silo.id != componentId || silo.entityId != id || silo.planetId != factory.planetId
            || silo.chargeSpend <= 0 || silo.coldSpend <= 0 || silo.bulletId != 1503
            || silo.bulletCount < 0 || silo.bulletInc < 0 || (long)silo.bulletInc > (long)silo.bulletCount * 10
            || factory.powerSystem?.consumerPool is null || silo.pcId <= 0 || silo.pcId != entity.powerConId
            || silo.pcId >= factory.powerSystem.consumerCursor || silo.pcId >= factory.powerSystem.consumerPool.Length
            || factory.powerSystem.consumerPool[silo.pcId].id != silo.pcId
            || factory.powerSystem.consumerPool[silo.pcId].entityId != id || factory.planet?.star is null) return null;
        var star = factory.planet.star;
        var result = new SiloStateSnapshot
        {
            NativeBindingVerified = true, SiloComponentId = componentId, StarId = star.id,
            RocketItemId = silo.bulletId, RocketCount = silo.bulletCount, RocketIncPoints = silo.bulletInc,
            Direction = silo.direction, CycleProgressRaw = silo.time, ChargeDurationRaw = silo.chargeSpend,
            CooldownDurationRaw = silo.coldSpend, NativeHasReceivingNode = silo.hasNode,
            BoostEnabled = silo.boost, FiredAnimationActive = silo.fired,
        };
        var data = GameMain.data;
        var spheres = data?.dysonSpheres;
        var sphere = spheres is not null && star.index >= 0 && star.index < spheres.Length ? spheres[star.index] : null;
        // Never initialize a sphere, create a layer/node, order points or advance a rocket.
        if (sphere is not null && ReferenceEquals(sphere.starData, star) && ReferenceEquals(sphere.gameData, data))
        {
            result.SphereAvailable = true;
            CaptureReceivingNodes(sphere, result);
            CaptureDysonRockets(sphere, result);
        }
        var count = 0;
        var complete = star.planets is not null;
        foreach (var planet in star.planets ?? Array.Empty<PlanetData>())
        {
            if (planet is null) { complete = false; continue; }
            if (planet.factoryIndex < 0) continue;
            var index = planet.factoryIndex;
            if (data?.factories is null || index >= data.factoryCount || index >= data.factories.Length
                || data.factories[index] is not { } existing || existing.planetId != planet.id
                || existing.entityPool is null || existing.entityCursor < 1 || existing.entityCursor > existing.entityPool.Length
                || existing.factorySystem is not { } resident || resident.siloPool is null
                || resident.siloCursor < 1 || resident.siloCursor > 8192 || resident.siloCursor > resident.siloPool.Length)
            { complete = false; continue; }
            for (var i = 1; i < resident.siloCursor; i++)
            {
                var known = resident.siloPool[i];
                if (known.id == 0) continue;
                if (known.id != i || known.planetId != planet.id || known.entityId <= 0 || known.entityId >= existing.entityCursor
                    || existing.entityPool[known.entityId].id != known.entityId
                    || existing.entityPool[known.entityId].protoId != 2312 || existing.entityPool[known.entityId].siloId != i)
                { complete = false; continue; }
                count++;
            }
        }
        result.StarSiloCoverageComplete = complete;
        if (complete) result.StarSiloCount = count;
        return result;
    }

    private static void CaptureReceivingNodes(DysonSphere sphere, SiloStateSnapshot result)
    {
        var layers = sphere.layersIdBased;
        if (layers is null || layers.Length < 1 || layers.Length > 11) return;
        var slots = 0;
        for (var i = 1; i < layers.Length; i++)
        {
            var layer = layers[i];
            if (layer is null) continue;
            if (layer.id != i || layer.nodePool is null || layer.nodeCursor < 1
                || layer.nodeCursor > layer.nodePool.Length || layer.nodeCursor > 4096
                || (slots += layer.nodeCursor - 1) > 4096) return;
            for (var j = 1; j < layer.nodeCursor; j++)
            {
                var node = layer.nodePool[j];
                if (node is null || node.id == 0) continue;
                if (node.id != j || node.layerId != i || node.sp < 0 || node.spMax < node.sp
                    || node.spOrdered < 0 || result.Nodes.Count >= 256) return;
                result.Nodes.Add(new DysonReceivingNodeSnapshot
                {
                    LayerId = i, NodeId = j, StructurePoints = node.sp, MaximumStructurePoints = node.spMax,
                    OrderedStructurePoints = node.spOrdered, RemainingOrderableStructurePoints = node.spReqOrder,
                });
            }
        }
        result.NodeCoverageComplete = true;
    }

    private static void CaptureDysonRockets(DysonSphere sphere, SiloStateSnapshot result)
    {
        if (sphere.rocketPool is null || sphere.rocketCursor < 1 || sphere.rocketCursor > 8192
            || sphere.rocketCursor > sphere.rocketPool.Length || sphere.rocketRecycleCursor < 0
            || sphere.rocketRecycleCursor >= sphere.rocketCursor) return;
        for (var i = 1; i < sphere.rocketCursor; i++)
        {
            var rocket = sphere.rocketPool[i];
            if (rocket.id == 0) continue;
            if (rocket.id != i || rocket.node is null || rocket.node.id != rocket.nodeId || rocket.node.layerId != rocket.nodeLayerId
                || sphere.layersIdBased is null || rocket.nodeLayerId <= 0 || rocket.nodeLayerId >= sphere.layersIdBased.Length
                || sphere.layersIdBased[rocket.nodeLayerId] is not { } layer || layer.id != rocket.nodeLayerId
                || layer.nodePool is null || rocket.nodeId <= 0 || rocket.nodeId >= layer.nodeCursor || rocket.nodeId >= layer.nodePool.Length
                || !ReferenceEquals(rocket.node, layer.nodePool[rocket.nodeId])
                || sphere.starData.planets is null || !sphere.starData.planets.Any(p => p is not null && p.id == rocket.planetId)
                || !SiloFinite(rocket.t) || !SiloFinite(rocket.uSpeed) || rocket.uSpeed < 0
                || !SiloFinite(rocket.uPos.x) || !SiloFinite(rocket.uPos.y) || !SiloFinite(rocket.uPos.z)
                || result.Rockets.Count >= 128) return;
            result.Rockets.Add(new DysonRocketSnapshot
            {
                RocketId = i, SourcePlanetId = rocket.planetId, TargetLayerId = rocket.nodeLayerId, TargetNodeId = rocket.nodeId,
                NativeFlightProgress = rocket.t, UniversalX = rocket.uPos.x, UniversalY = rocket.uPos.y,
                UniversalZ = rocket.uPos.z, Speed = rocket.uSpeed,
            });
        }
        var expected = sphere.rocketCursor - sphere.rocketRecycleCursor - 1;
        if (result.Rockets.Count != expected) return;
        result.RocketCoverageComplete = true;
        result.InFlightRocketCount = expected;
    }

    private static bool SiloFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
