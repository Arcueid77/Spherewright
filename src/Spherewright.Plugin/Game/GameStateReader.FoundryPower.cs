using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    // Called only within an already-owned local main-thread read. This is advisory
    // evidence, not a replacement for native build conditions or a write token.
    internal static FoundryPowerAssessment AssessFoundryPowerOnMainThread(PlanetFactory factory,
        IReadOnlyList<BlueprintSiteObject> objects, IReadOnlyList<BuildCatalogItem> catalog)
    {
        try { return FoundryPowerPlanner.Assess(objects, catalog, CaptureFoundryPowerContext(factory)); }
        catch (FoundryPlanningException error) { return Unavailable(error.Reason); }
        catch (OverflowException) { return Unavailable("power_native_budget_overflow"); }

        FoundryPowerAssessment Unavailable(string reason) => new FoundryPowerAssessment
        {
            CapturedAtGameTick = GameMain.gameTick,
            AssessmentHash = CanonicalStateHash.Combine("foundry-power-unavailable-v1", GameMain.gameTick, reason),
            Blockers = new List<string> { reason },
        };
    }

    private static FoundryPowerContext CaptureFoundryPowerContext(PlanetFactory factory)
    {
        var power = factory.powerSystem;
        if (power?.netPool is null || power.nodePool is null || power.consumerPool is null
            || power.netCursor < 1 || power.netCursor > Math.Min(1024, power.netPool.Length)
            || power.nodeCursor < 1 || power.nodeCursor > Math.Min(8193, power.nodePool.Length)
            || power.consumerCursor < 1 || power.consumerCursor > Math.Min(8193, power.consumerPool.Length)
            || factory.entityPool is null || factory.entityCursor < 1 || factory.entityCursor > factory.entityPool.Length
            || factory.prebuildPool is null || factory.prebuildCursor < 1
            || factory.prebuildCursor > Math.Min(131072, factory.prebuildPool.Length))
            throw Reject("power_native_pool_limit_or_unavailable");
        for (var i = 1; i < factory.prebuildCursor; i++)
            if (factory.prebuildPool[i].id != 0)
                throw Reject("power_existing_prebuilds_require_reassessment");
        var context = new FoundryPowerContext
        {
            PlacementShellRadius = factory.planet.realRadius + .2f, CapturedAtGameTick = GameMain.gameTick,
        };
        var nodes = new HashSet<int>(); var consumers = new HashSet<int>(); var generatorCount = 0;
        for (var networkId = 0; networkId < power.netCursor; networkId++)
        {
            var network = power.netPool[networkId];
            if (networkId > 0 && (network is null || network.id == 0)) continue;
            if (network is null || network.id != networkId || network.nodes is null || network.consumers is null
                || network.nodes.Count > 512 - nodes.Count || network.consumers.Count > 8192 - consumers.Count
                || context.Networks.Count >= 256 || networkId == 0 && network.nodes.Count != 0)
                throw Reject("power_native_network_identity_or_limit");
            long demand = 0;
            foreach (var node in network.nodes)
            {
                if (node is null || node.id <= 0 || node.id >= power.nodeCursor || !nodes.Add(node.id))
                    throw Reject("power_native_node_identity");
                ref var component = ref power.nodePool[node.id];
                if (component.id != node.id || component.networkId != networkId || !Entity(component.entityId)
                    || factory.entityPool[component.entityId].powerNodeId != node.id)
                    throw Reject("power_native_node_entity_identity");
                context.Nodes.Add(new FoundryExistingPowerNode
                {
                    NodeId = node.id, NetworkId = networkId,
                    ProjectedPosition = new Vector3Snapshot { X = node.x, Y = node.y, Z = node.z },
                    ConnectionDistanceSquared = node.connDistance2, CoverRadiusSquared = node.coverRadius2,
                });
                if (component.isCharger)
                    demand = checked(demand + Peak(component.workEnergyPerTick, component.idleEnergyPerTick, component.requiredEnergy));
            }
            foreach (var consumerId in network.consumers)
            {
                if (consumerId <= 0 || consumerId >= power.consumerCursor || !consumers.Add(consumerId))
                    throw Reject("power_native_consumer_identity");
                ref var consumer = ref power.consumerPool[consumerId];
                if (consumer.id != consumerId || consumer.networkId != networkId || !Entity(consumer.entityId)
                    || factory.entityPool[consumer.entityId].powerConId != consumerId)
                    throw Reject("power_native_consumer_entity_identity");
                var peak = Peak(consumer.workEnergyPerTick, consumer.idleEnergyPerTick, consumer.requiredEnergy);
                if (networkId > 0) demand = checked(demand + peak);
                else
                {
                    if (context.UnassignedConsumers.Count >= 2048 || float.IsNaN(consumer.plugAlt)
                        || float.IsInfinity(consumer.plugAlt) || consumer.plugAlt < 1 || consumer.plugAlt > 10000)
                        throw Reject("power_unassigned_consumer_limit_or_position");
                    var scale = consumer.plugAlt / context.PlacementShellRadius;
                    context.UnassignedConsumers.Add(new FoundryUnassignedPowerConsumer
                    {
                        ConsumerId = consumerId, ReservedDemandPerTick = peak,
                        ProjectedPosition = new Vector3Snapshot
                        { X = consumer.plugPos.x / scale, Y = consumer.plugPos.y / scale, Z = consumer.plugPos.z / scale },
                    });
                }
            }
            if (networkId > 0)
            {
                // Reuse the existing native counter/generator identity audit. Never
                // manufacture headroom from only the planet's instantaneous idle load.
                if (network.generators is null || network.generators.Count > 4096 - generatorCount
                    || TryCapturePowerNetwork(power, networkId, network, out _) is not null)
                    throw Reject("power_native_generation_evidence_invalid");
                generatorCount += network.generators.Count;
                context.Networks.Add(new FoundryExistingPowerNetwork
                {
                    NetworkId = networkId, GenerationCapacityPerTick = network.energyCapacity,
                    ReservedDemandPerTick = Math.Max(demand, network.energyRequired), ExportPerTick = network.energyExport,
                });
            }
        }
        for (var i = 1; i < power.consumerCursor; i++)
            if (power.consumerPool[i].id != 0 && (power.consumerPool[i].id != i || !consumers.Contains(i)))
                throw Reject("power_native_unlisted_consumer");
        for (var i = 1; i < power.nodeCursor; i++)
            if (power.nodePool[i].id != 0 && (power.nodePool[i].id != i || !nodes.Contains(i)))
                throw Reject("power_native_unlisted_node");
        return context;

        bool Entity(int id) => id > 0 && id < factory.entityCursor && factory.entityPool[id].id == id;
        long Peak(long work, long idle, long current)
        {
            if (work < 0 || idle < 0 || current < 0 || Math.Max(work, Math.Max(idle, current)) > 1000000000000L)
                throw Reject("power_native_demand_invalid");
            return Math.Max(work, Math.Max(idle, current));
        }
        FoundryPlanningException Reject(string reason) => new FoundryPlanningException(reason, "Native bounded power evidence is unavailable; do not assume coverage or spare capacity.");
    }

    // Invoked only by the owned main-thread catalog reader. Prefab scalars are
    // base ratings; EnergyCap_Fuel/GenEnergyByFuel mutate live state and must not run here.
    private static FuelPowerCatalogProfile? CaptureFuelPowerProfile(int itemId, PrefabDesc prefab) =>
        FuelPowerCatalogPolicy.Capture(itemId,
            prefab.isPowerGen && !prefab.windForcedPower && !prefab.photovoltaic
            && !prefab.gammaRayReceiver && !prefab.geothermal,
            prefab.genEnergyPerTick, prefab.useFuelPerTick, prefab.fuelMask);

    internal static long? CaptureFoundryWindGeneration(PrefabDesc prefab, float windStrength)
    {
        if (!prefab.isPowerGen || !prefab.windForcedPower || float.IsNaN(windStrength) || float.IsInfinity(windStrength)
            || windStrength < 0 || prefab.genEnergyPerTick < 0) return null;
        // Same float multiply and truncation as EnergyCap_Wind, never call that
        // mutating component method from a read-only assessment.
        var predicted = windStrength * (float)prefab.genEnergyPerTick;
        return float.IsNaN(predicted) || float.IsInfinity(predicted) || predicted > 1000000000000L ? (long?)null : (long)predicted;
    }
}
