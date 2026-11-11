using Spherewright.Bridge.Core.Celestial;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    public GameCallResult<DysonStateSnapshot> GetDysonStateOnMainThread(
        string? requestedSessionId, GetDysonStateRequest request)
    {
        var accessError = ValidateOwnedPlanetOnMainThread(requestedSessionId, request.PlanetId, out var factory);
        if (accessError is not null) return GameCallResult<DysonStateSnapshot>.Failed(accessError);
        if (request.StateHashVersion != 1 || request.LayerId < 0 || request.LayerId > DysonObservationPolicy.MaximumLayers)
            return GameCallResult<DysonStateSnapshot>.Failed(InvalidRequest(
                "Dyson observations require state-hash version 1 and layerId 0 through 10.",
                "Use 0 for all existing layers or one exact Native layer ID."));

        var data = GameMain.data;
        var star = factory?.planet?.star;
        if (data is null || star is null || !ReferenceEquals(GameMain.localStar, star)
            || data.galaxy?.stars is null || star.index < 0 || star.index >= data.galaxy.stars.Length
            || !ReferenceEquals(data.galaxy.stars[star.index], star))
            return GameCallResult<DysonStateSnapshot>.Failed(NotReady("The current local star binding is unavailable."));

        var result = new DysonStateSnapshot
        {
            SessionId = _sessions.SessionId!, PlanetId = factory!.planetId, StarId = star.id,
            SelectedLayerId = request.LayerId, CapturedAtGameTick = GameMain.gameTick,
        };
        var spheres = data.dysonSpheres;
        var sphere = spheres is not null && star.index < spheres.Length ? spheres[star.index] : null;
        if (sphere is not null && ReferenceEquals(sphere.gameData, data) && ReferenceEquals(sphere.starData, star))
        {
            result.SphereAvailable = true;
            result.NativeBindingVerified = true;
            if (sphere.energyGenCurrentTick >= 0 && sphere.energyReqCurrentTick >= 0)
            {
                result.SphereGeneratedEnergyPerTick = sphere.energyGenCurrentTick;
                result.SphereRequestedEnergyPerTick = sphere.energyReqCurrentTick;
            }
            CaptureDysonStructure(sphere, result);
            CaptureDysonSwarmCounts(sphere, result);
            var rockets = new SiloStateSnapshot();
            CaptureDysonRockets(sphere, rockets);
            result.RocketCoverageComplete = rockets.RocketCoverageComplete;
            result.InFlightRocketCount = rockets.InFlightRocketCount;
            result.Rockets = rockets.Rockets;
        }
        else
        {
            result.CoverageIssues.Add("native_dyson_sphere_unavailable");
        }

        CaptureStarRayReceivers(data, star, result);
        DysonObservationPolicy.Complete(result);
        return GameCallResult<DysonStateSnapshot>.Succeeded(result);
    }

    private static void CaptureDysonStructure(DysonSphere sphere, DysonStateSnapshot result)
    {
        var layers = sphere.layersIdBased;
        if (layers is null || layers.Length != DysonObservationPolicy.MaximumLayers + 1)
        {
            result.CoverageIssues.Add("native_dyson_layer_inventory_unavailable");
            return;
        }

        if (result.SelectedLayerId > 0)
            result.SelectedLayerPresent = layers[result.SelectedLayerId] is not null;
        var complete = true;
        var observedLayerCount = 0;
        var nodeBudget = DysonObservationPolicy.MaximumNodes;
        var frameBudget = DysonObservationPolicy.MaximumFrames;
        var shellBudget = DysonObservationPolicy.MaximumShells;
        var boundaryBudget = DysonObservationPolicy.MaximumShellBoundaryNodes;
        for (var index = 1; index < layers.Length; index++)
        {
            var layer = layers[index];
            if (layer is null) continue;
            observedLayerCount++;
            if (result.SelectedLayerId > 0 && result.SelectedLayerId != index) continue;
            if (layer.id != index || !ReferenceEquals(layer.dysonSphere, sphere)
                || !ReferenceEquals(layer.gameData, sphere.gameData) || !ReferenceEquals(layer.starData, sphere.starData)
                || !SiloFinite(layer.orbitRadius) || layer.orbitRadius <= 0 || layer.energyGenCurrentTick < 0
                || !SiloFinite(layer.orbitRotation.x) || !SiloFinite(layer.orbitRotation.y)
                || !SiloFinite(layer.orbitRotation.z) || !SiloFinite(layer.orbitRotation.w))
            {
                complete = false;
                result.CoverageIssues.Add("native_dyson_layer_binding_invalid");
                continue;
            }

            var snapshot = new DysonLayerSnapshot
            {
                LayerId = index, OrbitRadius = layer.orbitRadius,
                OrbitRotation = new QuaternionSnapshot { X = layer.orbitRotation.x, Y = layer.orbitRotation.y,
                    Z = layer.orbitRotation.z, W = layer.orbitRotation.w },
                GeneratedEnergyPerTick = layer.energyGenCurrentTick,
            };
            result.Layers.Add(snapshot);
            complete &= CaptureDysonLayerNodes(layer, snapshot, nodeBudget);
            complete &= CaptureDysonLayerFrames(layer, snapshot, frameBudget);
            complete &= CaptureDysonLayerShells(layer, snapshot, shellBudget, ref boundaryBudget);
            nodeBudget -= snapshot.Nodes.Count;
            frameBudget -= snapshot.Frames.Count;
            shellBudget -= snapshot.Shells.Count;
        }
        result.StructureCoverageComplete = complete && observedLayerCount == sphere.layerCount;
    }

    private static bool ValidDysonPool(int cursor, int recycled, int length) =>
        cursor >= 1 && cursor <= length && cursor <= DysonObservationPolicy.MaximumPoolSlots
        && recycled >= 0 && recycled < cursor;

    private static bool CaptureDysonLayerNodes(DysonSphereLayer layer, DysonLayerSnapshot result, int limit)
    {
        if (layer.nodePool is null || !ValidDysonPool(layer.nodeCursor, layer.nodeRecycleCursor, layer.nodePool.Length))
            return false;
        var complete = true;
        for (var index = 1; index < layer.nodeCursor; index++)
        {
            var node = layer.nodePool[index];
            if (node is null || node.id == 0) continue;
            if (result.Nodes.Count >= limit) return false;
            if (node.id != index || node.layerId != layer.id
                || !SiloFinite(node.pos.x) || !SiloFinite(node.pos.y) || !SiloFinite(node.pos.z))
            {
                complete = false;
                continue;
            }
            result.Nodes.Add(new DysonNodeSnapshot
            {
                NodeId = index, PrototypeId = node.protoId,
                Position = new Vector3Snapshot { X = node.pos.x, Y = node.pos.y, Z = node.pos.z },
                StructurePoints = node.sp, MaximumStructurePoints = node.spMax,
                OrderedStructurePoints = node.spOrdered, OrderedCellPoints = node.cpOrdered,
            });
        }
        return complete && result.Nodes.Count == layer.nodeCursor - layer.nodeRecycleCursor - 1;
    }

    private static bool CaptureDysonLayerFrames(DysonSphereLayer layer, DysonLayerSnapshot result, int limit)
    {
        if (layer.framePool is null || !ValidDysonPool(layer.frameCursor, layer.frameRecycleCursor, layer.framePool.Length))
            return false;
        var complete = true;
        for (var index = 1; index < layer.frameCursor; index++)
        {
            var frame = layer.framePool[index];
            if (frame is null || frame.id == 0) continue;
            if (result.Frames.Count >= limit) return false;
            if (frame.id != index || frame.layerId != layer.id
                || !DysonNodeBelongsToLayer(layer, frame.nodeA) || !DysonNodeBelongsToLayer(layer, frame.nodeB))
            {
                complete = false;
                continue;
            }
            result.Frames.Add(new DysonFrameSnapshot
            {
                FrameId = index, PrototypeId = frame.protoId, NodeAId = frame.nodeA.id, NodeBId = frame.nodeB.id,
                Euler = frame.euler, StructurePointsA = frame.spA, StructurePointsB = frame.spB,
                MaximumStructurePoints = frame.spMax,
            });
        }
        return complete && result.Frames.Count == layer.frameCursor - layer.frameRecycleCursor - 1;
    }

    private static bool DysonNodeBelongsToLayer(DysonSphereLayer layer, DysonNode? node) =>
        node is not null && node.id > 0 && node.layerId == layer.id && layer.nodePool is not null
        && node.id < layer.nodeCursor && node.id < layer.nodePool.Length
        && ReferenceEquals(layer.nodePool[node.id], node);

    private static bool CaptureDysonLayerShells(DysonSphereLayer layer, DysonLayerSnapshot result,
        int limit, ref int boundaryBudget)
    {
        if (layer.shellPool is null || !ValidDysonPool(layer.shellCursor, layer.shellRecycleCursor, layer.shellPool.Length))
            return false;
        var complete = true;
        for (var index = 1; index < layer.shellCursor; index++)
        {
            var shell = layer.shellPool[index];
            if (shell is null || shell.id == 0) continue;
            if (result.Shells.Count >= limit) return false;
            if (shell.id != index || shell.layerId != layer.id || !ReferenceEquals(shell.parentLayer, layer)
                || !ReferenceEquals(shell.dysonSphere, layer.dysonSphere) || shell.nodes is null || shell.frames is null
                || shell.nodes.Count > DysonObservationPolicy.MaximumNodes
                || shell.frames.Count != shell.nodes.Count || shell.nodecps is null
                || shell.nodecps.Length != shell.nodes.Count + 1 || shell.vertexCount < 0 || shell.cpPerVertex < 1
                || shell.nodecps.Any(points => points < 0)
                || shell.nodecps.Take(shell.nodes.Count).Sum(points => (long)points) != shell.nodecps[shell.nodecps.Length - 1]
                || shell.nodes.Any(node => !DysonNodeBelongsToLayer(layer, node))
                || shell.frames.Any(frame => frame is null || frame.id < 1 || layer.framePool is null
                    || frame.id >= layer.frameCursor || frame.id >= layer.framePool.Length
                    || !ReferenceEquals(layer.framePool[frame.id], frame)))
            {
                complete = false;
                continue;
            }
            if (shell.nodes.Count > boundaryBudget) return false;
            boundaryBudget -= shell.nodes.Count;
            result.Shells.Add(new DysonShellSnapshot
            {
                ShellId = index, PrototypeId = shell.protoId,
                NodeIds = shell.nodes.Select(node => node.id).ToList(), FrameIds = shell.frames.Select(frame => frame.id).ToList(),
                CellPoints = shell.nodecps[shell.nodecps.Length - 1],
                MaximumCellPoints = (long)shell.vertexCount * shell.cpPerVertex,
            });
        }
        return complete && result.Shells.Count == layer.shellCursor - layer.shellRecycleCursor - 1;
    }

    private static void CaptureDysonSwarmCounts(DysonSphere sphere, DysonStateSnapshot result)
    {
        var swarm = sphere.swarm;
        if (swarm is null || !ReferenceEquals(swarm.dysonSphere, sphere) || swarm.sailCount < 0
            || swarm.bulletPool is null || !ValidDysonPool(swarm.bulletCursor, swarm.bulletRecycleCursor, swarm.bulletPool.Length))
            return;
        result.ActiveSailCount = swarm.sailCount;
        result.InFlightSailCount = swarm.bulletCursor - swarm.bulletRecycleCursor - 1;
    }

    private static void CaptureStarRayReceivers(GameData data, StarData star, DysonStateSnapshot result)
    {
        var slots = 0;
        var complete = star.planets is not null && star.planets.Length <= 32;
        if (!complete) return;
        foreach (var planet in star.planets!)
        {
            if (planet is null || !ReferenceEquals(planet.star, star))
            {
                complete = false;
                continue;
            }
            if (planet.factoryIndex < 0) continue;
            var index = planet.factoryIndex;
            if (data.factories is null || index >= data.factoryCount || index >= data.factories.Length
                || data.factories[index] is not { } factory || factory.planetId != planet.id
                || !ReferenceEquals(factory.planet, planet) || factory.powerSystem?.genPool is null
                || factory.powerSystem.genCursor < 1 || factory.powerSystem.genCursor > factory.powerSystem.genPool.Length)
            {
                complete = false;
                continue;
            }
            var power = factory.powerSystem;
            if ((long)slots + power.genCursor - 1 > DysonObservationPolicy.MaximumPoolSlots)
            {
                result.CoverageIssues.Add("ray_receiver_scan_budget_exceeded");
                return;
            }
            slots += power.genCursor - 1;
            for (var generatorId = 1; generatorId < power.genCursor; generatorId++)
            {
                var generator = power.genPool[generatorId];
                if (generator.id == 0) continue;
                if (generator.id != generatorId)
                {
                    complete = false;
                    continue;
                }
                if (factory.entityPool is null || generator.entityId < 1 || generator.entityId >= factory.entityCursor
                    || generator.entityId >= factory.entityPool.Length
                    || factory.entityPool[generator.entityId].id != generator.entityId
                    || factory.entityPool[generator.entityId].powerGenId != generatorId
                    || LDB.models.Select(factory.entityPool[generator.entityId].modelIndex)?.prefabDesc is not { } descriptor
                    || descriptor.gammaRayReceiver != generator.gamma)
                {
                    complete = false;
                    continue;
                }
                if (!generator.gamma) continue;
                if (result.Receivers.Count >= DysonObservationPolicy.MaximumReceivers)
                {
                    result.CoverageIssues.Add("ray_receiver_result_budget_exceeded");
                    return;
                }
                var receiver = CaptureRayReceiverState(factory, generator.entityId);
                if (receiver is null)
                    complete = false;
                else
                    result.Receivers.Add(receiver);
            }
        }
        result.ReceiverCoverageComplete = complete;
    }

    private static RayReceiverStateSnapshot? CaptureRayReceiverState(PlanetFactory factory, int entityId)
    {
        var power = factory.powerSystem;
        if (factory.entityPool is null || entityId < 1 || entityId >= factory.entityCursor
            || entityId >= factory.entityPool.Length || power?.genPool is null || factory.planet?.star is null)
            return null;
        var entity = factory.entityPool[entityId];
        var generatorId = entity.powerGenId;
        if (entity.id != entityId || power.genCursor < 1 || power.genCursor > power.genPool.Length
            || generatorId < 1 || generatorId >= power.genCursor || generatorId >= power.genPool.Length
            || LDB.models.Select(entity.modelIndex)?.prefabDesc?.gammaRayReceiver != true
            || LDB.items.Select(entity.protoId)?.prefabDesc?.gammaRayReceiver != true)
            return null;
        var generator = power.genPool[generatorId];
        if (!generator.gamma || generator.id != generatorId || generator.entityId != entityId
            || generator.networkId < 0 || power.netPool is null || power.netCursor < 1 || power.netCursor > power.netPool.Length
            || generator.networkId >= power.netCursor || generator.networkId >= power.netPool.Length
            || (generator.networkId > 0 && (power.netPool[generator.networkId] is not { } network
                || network.id != generator.networkId || network.generators is null
                || network.generators.Count > DysonObservationPolicy.MaximumPoolSlots
                || network.generators.Count(id => id == generatorId) != 1)))
            return null;
        var snapshot = new RayReceiverStateSnapshot
        {
            NativeBindingVerified = true, PlanetId = factory.planetId, StarId = factory.planet.star.id,
            EntityId = entityId, ItemId = entity.protoId, GeneratorComponentId = generatorId,
            NetworkId = generator.networkId, CapturedAtGameTick = GameMain.gameTick,
            CapacityEnergyPerTick = generator.capacityCurrentTick, GeneratedEnergyPerTick = generator.generateCurrentTick,
            Warmup = generator.warmup, SunlightStrength = generator.currentStrength,
            ProductItemId = generator.productId, ProductCount = generator.productCount, ProductHeat = generator.productHeat,
            CatalystItemId = generator.catalystId, CatalystCount = generator.catalystCount,
            CatalystIncPoints = generator.catalystInc, ActiveCatalystItemId = generator.curCatalystId,
            CatalystPoints = generator.catalystPoint, BoostEnabled = generator.boost,
        };
        return DysonObservationPolicy.ValidReceiver(snapshot) ? snapshot : null;
    }
}
