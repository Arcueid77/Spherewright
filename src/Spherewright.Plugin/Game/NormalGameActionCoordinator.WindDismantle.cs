using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;
using System.Globalization;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private const int WindMaximumEntityEntries = 131072;

    private GameCallResult<PreparedNormalAction> PrepareWindDismantlePreview(
        FactoryEntitySnapshot target, PlayerStateSnapshot player, string expectedEndpointHash)
    {
        WindTurbineDismantlePreview preview;
        try
        {
            preview = CreateWindDismantlePreview(target, player, CaptureWindDismantleBoundary(target, false), true);
            var readiness = ValidateDismantleTarget(player, target, expectedEndpointHash);
            if (readiness is not null && !preview.Blockers.Contains(readiness.Code))
                preview.Blockers.Add(readiness.Code);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            preview = new WindTurbineDismantlePreview
            {
                TargetEntityId = target.ObjectId, CapturedAtGameTick = GameMain.gameTick, PreviewOnly = true,
                Blockers = new List<string> { "wind_power_evidence_unavailable", error.Message },
            };
        }
        return GameCallResult<PreparedNormalAction>.Succeeded(new PreparedNormalAction
        {
            Prepared = true, ActionKind = NormalActionKinds.Dismantle, CommitAllowedNow = false,
            TargetObjectId = target.ObjectId, WindTurbineDismantle = preview,
            ItemBudget = new List<ActionItemBudget>
            {
                new ActionItemBudget { ItemId = WindTurbineDismantlePolicy.ItemId, Name = target.Name, Count = 1, Direction = "dismantle-recovery" },
            },
            CommitBlockers = preview.Blockers.Select(reason => new WriteBlocker
            {
                Code = "wind_preview_condition", Message = reason,
            }).ToList(),
            CompletionCondition = "Read the power impact and conditions. Ordinary removal requires a separate executable prepare and commit.",
        });
    }

    private static WindDismantleBoundary CaptureWindDismantleBoundary(FactoryEntitySnapshot target, bool retainWorld)
    {
        var factory = GameMain.data?.localLoadedPlanetFactory;
        if (!WindTurbineDismantlePolicy.SupportsSnapshot(target) || factory is null
            || !ReferenceEquals(factory, GameMain.localPlanet?.factory)
            || factory.entityPool is null || factory.prebuildPool is null
            || factory.entityConnPool is null || factory.prebuildConnPool is null
            || factory.factorySystem?.inserterPool is null
            || factory.entityCursor <= target.ObjectId || factory.entityCursor > WindMaximumEntityEntries
            || factory.entityCursor > factory.entityPool.Length || factory.prebuildCursor < 1
            || factory.prebuildCursor > WindMaximumEntityEntries || factory.prebuildCursor > factory.prebuildPool.Length
            || factory.factorySystem.inserterCursor < 1 || factory.factorySystem.inserterCursor > WindMaximumEntityEntries
            || factory.factorySystem.inserterCursor > factory.factorySystem.inserterPool.Length
            || factory.entityConnPool.Length < factory.entityCursor * 16
            || factory.prebuildConnPool.Length < factory.prebuildCursor * 16)
            throw new InvalidOperationException("Wind recovery requires complete bounded local entity and connection evidence.");

        var entity = factory.entityPool[target.ObjectId];
        var power = factory.powerSystem;
        var prefab = LDB.items.Select(target.ItemId)?.prefabDesc;
        if (entity.id != target.ObjectId || entity.protoId != WindTurbineDismantlePolicy.ItemId
            || prefab is null || !prefab.isPowerGen || !prefab.windForcedPower || !prefab.isPowerNode
            || power?.nodePool is null || power.genPool is null
            || entity.powerNodeId <= 0 || entity.powerNodeId >= power.nodeCursor
            || entity.powerNodeId >= power.nodePool.Length || entity.powerGenId <= 0
            || entity.powerGenId >= power.genCursor || entity.powerGenId >= power.genPool.Length
            || !power.genPool[entity.powerGenId].wind || power.genPool[entity.powerGenId].photovoltaic
            || power.genPool[entity.powerGenId].gamma || power.genPool[entity.powerGenId].geothermal
            || power.genPool[entity.powerGenId].genEnergyPerTick != prefab.genEnergyPerTick || entity.powerConId != 0
            || entity.powerAccId != 0 || entity.powerExcId != 0
            || entity.minerId != 0 || entity.assemblerId != 0 || entity.labId != 0
            || entity.beltId != 0 || entity.inserterId != 0 || entity.storageId != 0 || entity.tankId != 0
            || entity.stationId != 0 || entity.dispenserId != 0 || entity.splitterId != 0
            || entity.fractionatorId != 0 || entity.ejectorId != 0 || entity.siloId != 0
            || entity.monitorId != 0 || entity.speakerId != 0 || entity.spraycoaterId != 0 || entity.pilerId != 0
            || entity.turretId != 0 || entity.beaconId != 0 || entity.fieldGenId != 0 || entity.battleBaseId != 0
            || entity.constructionModuleId != 0 || entity.combatModuleId != 0)
            throw new InvalidOperationException("The selected entity must contain one ordinary wind generator and its power node.");

        var generation = GameStateReader.CaptureFoundryWindGeneration(prefab, factory.planet.windStrength)
            ?? throw new InvalidOperationException("Native wind generation capacity is unavailable.");
        var topology = CaptureWindPowerTopology(factory);
        var projection = WindTurbineDismantlePolicy.Project(topology.State, entity.powerNodeId);
        if (projection.Target.EntityId != target.ObjectId || projection.Target.GeneratorId != entity.powerGenId)
            throw new InvalidOperationException("Wind entity, node and generator bindings disagree.");
        var extraNetworks = Math.Max(0, projection.Components.Count - topology.State.Nodes.Select(node => node.NetworkId).Distinct().Count());
        if (power.netCursor + extraNetworks > WindTurbineDismantlePolicy.MaximumNetworks)
            throw new InvalidOperationException("The projected power network allocation exceeds the bounded readback capacity.");

        for (var id = 1; id < factory.entityCursor; id++)
        {
            var other = factory.entityPool[id];
            if (other.id == 0) continue;
            if (other.id != id || id != target.ObjectId
                && (other.powerNodeId == entity.powerNodeId || other.powerGenId == entity.powerGenId))
                throw new InvalidOperationException("Wind component ownership is ambiguous.");
            for (var slot = 0; slot < 16; slot++)
            {
                factory.ReadObjectConn(id, slot, out _, out var endpoint, out _);
                if (endpoint == target.ObjectId || id == target.ObjectId
                    && (endpoint != 0 || factory.entityConnPool[id * 16 + slot] != 0))
                    throw new InvalidOperationException("The wind target has a factory connection or inbound reference.");
            }
        }
        for (var id = 1; id < factory.prebuildCursor; id++)
        {
            if (factory.prebuildPool[id].id == 0) continue;
            for (var slot = 0; slot < 16; slot++)
            {
                factory.ReadObjectConn(-id, slot, out _, out var endpoint, out _);
                if (endpoint == target.ObjectId)
                    throw new InvalidOperationException("A prebuild still references the wind target.");
            }
        }

        foreach (var inserter in factory.factorySystem.inserterPool.Take(factory.factorySystem.inserterCursor).Skip(1))
            if (inserter.id != 0 && (inserter.pickTarget == target.ObjectId || inserter.insertTarget == target.ObjectId))
                throw new InvalidOperationException("A cached sorter target still references the wind turbine.");

        return new WindDismantleBoundary
        {
            Topology = topology,
            Projection = projection,
            GenerationCapacityPerTick = generation,
            NativeHash = CanonicalStateHash.Combine("wind-dismantle-v1", target.EndpointStateHash,
                topology.NativeHash, entity.powerNodeId, entity.powerGenId, generation),
            Entities = retainWorld ? factory.entityPool.Take(factory.entityCursor).ToArray() : null,
            Prebuilds = retainWorld ? factory.prebuildPool.Take(factory.prebuildCursor).ToArray() : null,
            EntityConnections = retainWorld ? factory.entityConnPool.Take(factory.entityCursor * 16).ToArray() : null,
            PrebuildConnections = retainWorld ? factory.prebuildConnPool.Take(factory.prebuildCursor * 16).ToArray() : null,
        };
    }

    private static WindPowerTopology CaptureWindPowerTopology(PlanetFactory factory)
    {
        var power = factory.powerSystem;
        if (power?.netPool is null || power.nodePool is null || power.genPool is null
            || power.consumerPool is null || power.accPool is null || power.excPool is null
            || !WindPoolBounded(power.netCursor, power.netPool.Length, WindTurbineDismantlePolicy.MaximumNetworks)
            || !WindPoolBounded(power.nodeCursor, power.nodePool.Length, WindTurbineDismantlePolicy.MaximumNodes + 1)
            || !WindPoolBounded(power.genCursor, power.genPool.Length, WindTurbineDismantlePolicy.MaximumNodes + 1)
            || !WindPoolBounded(power.consumerCursor, power.consumerPool.Length, WindTurbineDismantlePolicy.MaximumNodes + 1)
            || !WindPoolBounded(power.accCursor, power.accPool.Length, WindTurbineDismantlePolicy.MaximumNodes + 1)
            || !WindPoolBounded(power.excCursor, power.excPool.Length, WindTurbineDismantlePolicy.MaximumNodes + 1))
            throw new InvalidOperationException("Complete bounded native power pools are unavailable.");

        var result = new WindPowerTopology();
        var nativeNodes = new Dictionary<int, PowerNetworkStructures.Node>();
        var generators = new HashSet<int>();
        var consumers = new HashSet<int>();
        var accumulators = new HashSet<int>();
        var exchangers = new HashSet<int>();
        var connectionCount = 0;
        for (var networkId = 0; networkId < power.netCursor; networkId++)
        {
            var network = power.netPool[networkId];
            if (networkId > 0 && (network is null || network.id == 0)) continue;
            if (network is null || network.id != networkId || network.nodes is null || network.consumers is null
                || network.generators is null || network.accumulators is null || network.exchangers is null
                || network.nodes.Count > WindTurbineDismantlePolicy.MaximumNodes - nativeNodes.Count
                || network.consumers.Count > WindTurbineDismantlePolicy.MaximumNodes
                || networkId == 0 && network.nodes.Count != 0)
                throw new InvalidOperationException("Native power network membership is incomplete.");

            foreach (var node in network.nodes)
            {
                if (node is null || node.id <= 0 || node.id >= power.nodeCursor || nativeNodes.ContainsKey(node.id)
                    || node.conns is null || node.lines is null || node.consumers is null
                    || node.conns.Count > WindTurbineDismantlePolicy.MaximumNodes
                    || node.lines.Count > WindTurbineDismantlePolicy.MaximumNodes
                    || node.consumers.Count > WindTurbineDismantlePolicy.MaximumNodes
                    || node.conns.Any(other => other is null) || node.lines.Any(other => other is null))
                    throw new InvalidOperationException("Native power node membership is invalid.");
                connectionCount = checked(connectionCount + node.conns.Count);
                if (connectionCount > WindTurbineDismantlePolicy.MaximumConnections)
                    throw new InvalidOperationException("The complete native power connection limit is exceeded.");
                var component = power.nodePool[node.id];
                if (component.id != node.id || component.networkId != networkId || !Entity(component.entityId)
                    || factory.entityPool[component.entityId].powerNodeId != node.id
                    || node.genId != factory.entityPool[component.entityId].powerGenId
                    || node.accId != factory.entityPool[component.entityId].powerAccId
                    || node.excId != factory.entityPool[component.entityId].powerExcId)
                    throw new InvalidOperationException("Native power node component identity is inconsistent.");
                var geometry = new[] { component.powerPoint.x, component.powerPoint.y, component.powerPoint.z,
                    component.connectDistance, component.coverRadius, node.x, node.y, node.z, node.connDistance2, node.coverRadius2 };
                if (geometry.Any(value => float.IsNaN(value) || float.IsInfinity(value))
                    || component.connectDistance < 0 || component.coverRadius < 0 || node.connDistance2 < 0 || node.coverRadius2 < 0)
                    throw new InvalidOperationException("Native power node geometry is invalid.");
                nativeNodes.Add(node.id, node);
                result.State.Nodes.Add(new WindPowerNode
                {
                    NodeId = node.id, EntityId = component.entityId, NetworkId = networkId,
                    GeneratorId = node.genId, AccumulatorId = node.accId, ExchangerId = node.excId,
                    ConnectedNodeIds = node.conns.Select(other => other.id).OrderBy(id => id).ToList(),
                    ConsumerIds = node.consumers.OrderBy(id => id).ToList(),
                });
                result.NodeMetadata[node.id] = CanonicalStateHash.Combine("wind-native-node-v1", component.id, component.entityId,
                    component.isCharger, string.Join(",", geometry.Select(value => value.ToString("R", CultureInfo.InvariantCulture))));
                result.Lines[node.id] = node.lines.Select(other => other.id).OrderBy(id => id).ToArray();
            }
            foreach (var id in network.consumers)
            {
                if (id <= 0 || id >= power.consumerCursor || !consumers.Add(id))
                    throw new InvalidOperationException("Native power consumer membership is duplicated or invalid.");
                var component = power.consumerPool[id];
                if (component.id != id || component.networkId != networkId || !Entity(component.entityId)
                    || factory.entityPool[component.entityId].powerConId != id)
                    throw new InvalidOperationException("Native power consumer component identity is inconsistent.");
                result.ConsumerEntities.Add(id, component.entityId);
                if (networkId == 0) result.State.UnpoweredConsumerIds.Add(id);
            }
            foreach (var id in network.generators)
            {
                if (id <= 0 || id >= power.genCursor || !generators.Add(id))
                    throw new InvalidOperationException("Native generator membership is duplicated or invalid.");
                var component = power.genPool[id];
                if (component.id != id || component.networkId != networkId || !Entity(component.entityId)
                    || factory.entityPool[component.entityId].powerGenId != id)
                    throw new InvalidOperationException("Native generator component identity is inconsistent.");
                result.GeneratorEntities.Add(id, component.entityId);
            }
            foreach (var id in network.accumulators)
            {
                if (id <= 0 || id >= power.accCursor || !accumulators.Add(id)
                    || power.accPool[id].id != id || power.accPool[id].networkId != networkId
                    || !Entity(power.accPool[id].entityId) || factory.entityPool[power.accPool[id].entityId].powerAccId != id)
                    throw new InvalidOperationException("Native accumulator component identity is inconsistent.");
            }
            foreach (var id in network.exchangers)
            {
                if (id <= 0 || id >= power.excCursor || !exchangers.Add(id)
                    || power.excPool[id].id != id || power.excPool[id].networkId != networkId
                    || !Entity(power.excPool[id].entityId) || factory.entityPool[power.excPool[id].entityId].powerExcId != id)
                    throw new InvalidOperationException("Native exchanger component identity is inconsistent.");
            }
            var members = result.State.Nodes.Where(node => node.NetworkId == networkId).ToList();
            if (networkId > 0 && (!SameIds(network.consumers, members.SelectMany(node => node.ConsumerIds).Distinct())
                || !SameIds(network.generators, members.Where(node => node.GeneratorId > 0).Select(node => node.GeneratorId))
                || !SameIds(network.accumulators, members.Where(node => node.AccumulatorId > 0).Select(node => node.AccumulatorId))
                || !SameIds(network.exchangers, members.Where(node => node.ExchangerId > 0).Select(node => node.ExchangerId))))
                throw new InvalidOperationException("Native power network lists disagree with their complete nodes.");
        }

        for (var id = 1; id < power.nodeCursor; id++)
            if (power.nodePool[id].id != 0 && (power.nodePool[id].id != id || !nativeNodes.ContainsKey(id)))
                throw new InvalidOperationException("An active power node is absent from its network.");
        for (var id = 1; id < power.genCursor; id++)
            if (power.genPool[id].id != 0 && (power.genPool[id].id != id || !generators.Contains(id)))
                throw new InvalidOperationException("An active generator is absent from its network.");
        for (var id = 1; id < power.consumerCursor; id++)
            if (power.consumerPool[id].id != 0 && (power.consumerPool[id].id != id || !consumers.Contains(id)))
                throw new InvalidOperationException("An active consumer is absent from its network.");
        for (var id = 1; id < power.accCursor; id++)
            if (power.accPool[id].id != 0 && (power.accPool[id].id != id || !accumulators.Contains(id)))
                throw new InvalidOperationException("An active accumulator is absent from its network.");
        for (var id = 1; id < power.excCursor; id++)
            if (power.excPool[id].id != 0 && (power.excPool[id].id != id || !exchangers.Contains(id)))
                throw new InvalidOperationException("An active exchanger is absent from its network.");
        for (var id = 1; id < factory.entityCursor; id++)
        {
            var entity = factory.entityPool[id];
            if (entity.id == 0) continue;
            if (entity.id != id || entity.powerNodeId < 0 || entity.powerGenId < 0 || entity.powerConId < 0
                || entity.powerAccId < 0 || entity.powerExcId < 0
                || entity.powerNodeId > 0 && (!nativeNodes.ContainsKey(entity.powerNodeId)
                    || power.nodePool[entity.powerNodeId].entityId != id)
                || entity.powerGenId > 0 && (!generators.Contains(entity.powerGenId)
                    || power.genPool[entity.powerGenId].entityId != id)
                || entity.powerConId > 0 && (!consumers.Contains(entity.powerConId)
                    || power.consumerPool[entity.powerConId].entityId != id)
                || entity.powerAccId > 0 && (!accumulators.Contains(entity.powerAccId)
                    || power.accPool[entity.powerAccId].entityId != id)
                || entity.powerExcId > 0 && (!exchangers.Contains(entity.powerExcId)
                    || power.excPool[entity.powerExcId].entityId != id))
                throw new InvalidOperationException("An entity has a dangling or shared native power component.");
        }
        foreach (var pair in nativeNodes)
        {
            foreach (var neighbor in pair.Value.conns.Concat(pair.Value.lines))
                if (neighbor is null || !nativeNodes.TryGetValue(neighbor.id, out var same) || !ReferenceEquals(neighbor, same))
                    throw new InvalidOperationException("A power connection references an unobserved native node.");
            if (pair.Value.lines.Any(line => !pair.Value.conns.Contains(line) || !line.lines.Contains(pair.Value)))
                throw new InvalidOperationException("A native power line is nonreciprocal or outside its connections.");
        }
        WindTurbineDismantlePolicy.Validate(result.State);
        var proof = new List<object?> { WindTurbineDismantlePolicy.Fingerprint(result.State) };
        foreach (var pair in result.NodeMetadata.OrderBy(pair => pair.Key))
            proof.Add(CanonicalStateHash.Combine("wind-node-metadata-v1", pair.Key, pair.Value));
        foreach (var pair in result.Lines.OrderBy(pair => pair.Key))
            proof.Add(CanonicalStateHash.Combine("wind-native-lines-v1", pair.Key, string.Join(",", pair.Value)));
        foreach (var pair in result.ConsumerEntities.OrderBy(pair => pair.Key))
            proof.Add(CanonicalStateHash.Combine("wind-consumer-entity-v1", pair.Key, pair.Value));
        foreach (var pair in result.GeneratorEntities.OrderBy(pair => pair.Key))
            proof.Add(CanonicalStateHash.Combine("wind-generator-entity-v1", pair.Key, pair.Value));
        result.NativeHash = CanonicalStateHash.Combine("wind-native-power-topology-v1", proof.ToArray());
        return result;

        bool Entity(int id) => id > 0 && id < factory.entityCursor && factory.entityPool[id].id == id;
    }

    private static bool WindPoolBounded(int cursor, int length, int maximum) => cursor >= 1 && cursor <= maximum && cursor <= length;

    private static bool SameIds(IEnumerable<int> first, IEnumerable<int> second) =>
        first.OrderBy(id => id).SequenceEqual(second.OrderBy(id => id));

    private static List<WindPowerNetworkSnapshot> WindNetworkSnapshots(WindPowerTopology topology,
        IEnumerable<WindPowerComponent> components, ISet<int> affectedNodeIds, bool includeNativeIds)
    {
        var nodes = topology.State.Nodes.ToDictionary(node => node.NodeId);
        return components.Where(group => group.NodeIds.Any(affectedNodeIds.Contains)).Select(group => new WindPowerNetworkSnapshot
        {
            NativeNetworkId = includeNativeIds ? nodes[group.NodeIds[0]].NetworkId : (int?)null,
            NodeEntityIds = group.NodeIds.Select(id => nodes[id].EntityId).OrderBy(id => id).ToList(),
            GeneratorEntityIds = group.GeneratorIds.Select(id => topology.GeneratorEntities[id]).OrderBy(id => id).ToList(),
            AccumulatorEntityIds = group.NodeIds.Where(id => nodes[id].AccumulatorId > 0).Select(id => nodes[id].EntityId).OrderBy(id => id).ToList(),
            ExchangerEntityIds = group.NodeIds.Where(id => nodes[id].ExchangerId > 0).Select(id => nodes[id].EntityId).OrderBy(id => id).ToList(),
            ConsumerEntityIds = group.ConsumerIds.Select(id => topology.ConsumerEntities[id]).OrderBy(id => id).ToList(),
        }).ToList();
    }

    private WindTurbineDismantlePreview CreateWindDismantlePreview(FactoryEntitySnapshot target,
        PlayerStateSnapshot player, WindDismantleBoundary boundary, bool previewOnly)
    {
        var affected = new HashSet<int>(boundary.Topology.State.Nodes
            .Where(node => node.NetworkId == boundary.Projection.Target.NetworkId).Select(node => node.NodeId));
        var nativePlayer = GameMain.mainPlayer;
        var result = new WindTurbineDismantlePreview
        {
            CapturedAtGameTick = GameMain.gameTick, TargetEntityId = target.ObjectId,
            PowerNodeId = boundary.Projection.Target.NodeId, GeneratorId = boundary.Projection.Target.GeneratorId,
            PowerNetworkId = boundary.Projection.Target.NetworkId,
            GenerationCapacityLostPerTick = boundary.GenerationCapacityPerTick,
            GenerationCapacityLostWatts = checked(boundary.GenerationCapacityPerTick * 60),
            PowerTopologyHash = boundary.NativeHash, Complete = true, PreviewOnly = previewOnly,
            NativeDismantleAvailable = nativePlayer?.controller?.actionBuild is not null
                && !(GameMain.sandboxToolsEnabled && GameMain.preferences.instantDismantle),
            PlayerSettled = nativePlayer is not null && player.IsAlive && player.IsOnPlanet
                && player.MovementState == "Walk" && player.Speed <= .1f && BuildUiIsIdle(nativePlayer),
            TargetWithinReach = Vector3.Distance(ToVector(player.Position), ToVector(target.Position)) <= player.BuildArea + .1f,
            InventoryRoomAvailable = player.InventorySlotCount - player.InventoryOccupiedSlotCount >= CountConservativeRecoverySlots(target),
            AffectedConsumerEntityIds = boundary.Topology.State.Nodes.Where(node => affected.Contains(node.NodeId))
                .SelectMany(node => node.ConsumerIds).Distinct().Select(id => boundary.Topology.ConsumerEntities[id]).OrderBy(id => id).ToList(),
            DisconnectedConsumerEntityIds = boundary.Projection.DisconnectedConsumerIds
                .Select(id => boundary.Topology.ConsumerEntities[id]).OrderBy(id => id).ToList(),
            ProjectedNetworks = WindNetworkSnapshots(boundary.Topology, boundary.Projection.Components, affected, false),
        };
        if (!result.NativeDismantleAvailable) result.Blockers.Add("wind_native_dismantle_unavailable");
        if (!result.PlayerSettled) result.Blockers.Add("wind_player_not_settled");
        if (!result.TargetWithinReach) result.Blockers.Add("wind_target_out_of_range");
        if (!result.InventoryRoomAvailable) result.Blockers.Add("wind_inventory_room_unavailable");
        return result;
    }

    private static WindTurbineDismantleReadback VerifyWindDismantleBoundary(int removedId, WindDismantleBoundary before)
    {
        var factory = GameMain.data.localLoadedPlanetFactory;
        var target = before.Projection.Target;
        if (factory is null || before.Entities is null || before.Prebuilds is null
            || before.EntityConnections is null || before.PrebuildConnections is null
            || factory.entityCursor != before.Entities.Length || factory.prebuildCursor != before.Prebuilds.Length
            || factory.entityPool[removedId].id != 0 || factory.powerSystem.nodePool[target.NodeId].id != 0
            || factory.powerSystem.genPool[target.GeneratorId].id != 0)
            throw new InvalidOperationException("The exact wind entity, node and generator disappearance is unproved.");
        for (var id = 1; id < before.Entities.Length; id++)
            if (id != removedId && !before.Entities[id].Equals(factory.entityPool[id]))
                throw new InvalidOperationException("Wind recovery changed another native entity.");
        for (var id = 1; id < before.Prebuilds.Length; id++)
            if (!before.Prebuilds[id].Equals(factory.prebuildPool[id]))
                throw new InvalidOperationException("Wind recovery changed a prebuild.");
        if (!before.EntityConnections.SequenceEqual(factory.entityConnPool.Take(before.EntityConnections.Length))
            || !before.PrebuildConnections.SequenceEqual(factory.prebuildConnPool.Take(before.PrebuildConnections.Length)))
            throw new InvalidOperationException("Wind recovery changed factory entity or prebuild connections.");

        var after = CaptureWindPowerTopology(factory);
        if (!WindTurbineDismantlePolicy.Matches(before.Projection, after.State)
            || !before.Topology.ConsumerEntities.OrderBy(pair => pair.Key).SequenceEqual(after.ConsumerEntities.OrderBy(pair => pair.Key))
            || !before.Topology.GeneratorEntities.Where(pair => pair.Key != target.GeneratorId).OrderBy(pair => pair.Key)
                .SequenceEqual(after.GeneratorEntities.OrderBy(pair => pair.Key)))
            throw new InvalidOperationException("Native power connectivity differs from the prepared wind removal projection.");
        var afterNodes = after.State.Nodes.ToDictionary(node => node.NodeId);
        foreach (var node in before.Topology.State.Nodes.Where(node => node.NodeId != target.NodeId))
        {
            if (before.Topology.NodeMetadata[node.NodeId] != after.NodeMetadata[node.NodeId])
                throw new InvalidOperationException("Wind recovery changed surviving power node geometry or identity.");
            if (node.NetworkId != target.NetworkId && (afterNodes[node.NodeId].NetworkId != node.NetworkId
                || !before.Topology.Lines[node.NodeId].SequenceEqual(after.Lines[node.NodeId])))
                throw new InvalidOperationException("Wind recovery changed an unrelated native power network.");
        }
        var affected = new HashSet<int>(before.Topology.State.Nodes.Where(node => node.NetworkId == target.NetworkId).Select(node => node.NodeId));
        return new WindTurbineDismantleReadback
        {
            TargetEntityId = removedId, PreparedPowerTopologyHash = before.NativeHash, ResultPowerTopologyHash = after.NativeHash,
            TargetAbsent = true, SurvivingEntitiesPreserved = true, FactoryConnectionsPreserved = true,
            PowerConnectivityVerified = true, UnrelatedPowerNetworksPreserved = true,
            DisconnectedConsumerEntityIds = before.Projection.DisconnectedConsumerIds
                .Select(id => before.Topology.ConsumerEntities[id]).OrderBy(id => id).ToList(),
            ResultNetworks = WindNetworkSnapshots(after, WindTurbineDismantlePolicy.Components(after.State.Nodes), affected, true),
        };
    }

    private sealed class WindPowerTopology
    {
        public WindPowerState State { get; } = new WindPowerState();
        public Dictionary<int, int> ConsumerEntities { get; } = new Dictionary<int, int>();
        public Dictionary<int, int> GeneratorEntities { get; } = new Dictionary<int, int>();
        public Dictionary<int, string> NodeMetadata { get; } = new Dictionary<int, string>();
        public Dictionary<int, int[]> Lines { get; } = new Dictionary<int, int[]>();
        public string NativeHash { get; set; } = string.Empty;
    }

    private sealed class WindDismantleBoundary
    {
        public WindPowerTopology Topology { get; set; } = new WindPowerTopology();
        public WindPowerProjection Projection { get; set; } = new WindPowerProjection();
        public long GenerationCapacityPerTick { get; set; }
        public string NativeHash { get; set; } = string.Empty;
        public EntityData[]? Entities { get; set; }
        public PrebuildData[]? Prebuilds { get; set; }
        public int[]? EntityConnections { get; set; }
        public int[]? PrebuildConnections { get; set; }
    }
}
