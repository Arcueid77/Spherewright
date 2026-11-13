using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Factory;

public static class WindTurbineDismantlePolicy
{
    public const int ItemId = 2203;
    public const int MaximumNodes = 8192;
    public const int MaximumNetworks = 1024;
    public const int MaximumConnections = 65536;
    public const int MaximumNativeLineNeighbors = 2048;
    public const int MaximumNativeSplitComponents = 1021;

    public static bool SupportsSnapshot(FactoryEntitySnapshot? target) => target is not null
        && target.ObjectKind == FactoryObjectKinds.Entity && target.ObjectId > 0
        && target.ItemId == ItemId && target.ComponentKind == "power-generator"
        && target.RecipeId == 0 && target.Connections is not null && target.Connections.Count == 0
        && target.Buffers is not null && target.Buffers.Count <= 1
        && target.Buffers.All(IsWindGenerationObservation);

    private static bool IsWindGenerationObservation(FactoryBufferSnapshot buffer) => buffer is not null
        && buffer.Role == FactoryBufferSemantics.PowerGenerationRole && buffer.ItemId == 0
        && buffer.Count >= 0 && buffer.Inc == 0
        && (buffer.CountUnit == "joules_per_tick" && buffer.UnitsPerItem == 0
            || buffer.CountUnit == "items" && buffer.UnitsPerItem == 1);

    public static WindPowerProjection Project(WindPowerState before, int removedNodeId)
    {
        Validate(before);
        var target = before.Nodes.SingleOrDefault(node => node.NodeId == removedNodeId)
            ?? throw new ArgumentException("The wind power node is absent from the complete topology.");
        if (target.GeneratorId <= 0 || target.AccumulatorId != 0 || target.ExchangerId != 0)
            throw new ArgumentException("The wind target requires exactly one generator.");

        var neighboringNodeIds = new HashSet<int>(target.ConnectedNodeIds);
        var neighboringNodes = before.Nodes.Where(node => neighboringNodeIds.Contains(node.NodeId)).ToList();
        if (neighboringNodes.Any(node => node.ConnectedNodeIds.Count > MaximumNativeLineNeighbors))
            throw new ArgumentException("Native power-line rearrangement exceeds its neighbor scratch capacity.");
        var survivors = before.Nodes.Where(node => node.NodeId != removedNodeId)
            .Select(node => node.WithoutConnection(removedNodeId)).ToList();
        var neighboringCoverage = new HashSet<int>(neighboringNodes.SelectMany(node => node.ConsumerIds));
        var nativeDisconnected = new HashSet<int>(target.ConsumerIds.Except(neighboringCoverage));
        if (survivors.SelectMany(node => node.ConsumerIds).Any(nativeDisconnected.Contains))
            throw new ArgumentException("Native wind unlink would leave consumer coverage outside its neighboring power nodes.");
        var groups = Components(survivors);
        var affectedNodeIds = new HashSet<int>(survivors.Where(node => node.NetworkId == target.NetworkId).Select(node => node.NodeId));
        var affectedGroups = groups.Count(group => group.NodeIds.Any(affectedNodeIds.Contains));
        if (affectedGroups > MaximumNativeSplitComponents || groups.Count > MaximumNetworks)
            throw new ArgumentException("The projected wind removal exceeds native network split limits.");
        var assigned = groups.SelectMany(group => group.ConsumerIds).ToList();
        if (assigned.Distinct().Count() != assigned.Count)
            throw new ArgumentException("Consumer membership spans multiple projected power components.");

        var disconnected = target.ConsumerIds.Except(assigned).OrderBy(id => id).ToList();
        return new WindPowerProjection
        {
            Target = target,
            Nodes = survivors,
            Components = groups,
            UnpoweredConsumerIds = before.UnpoweredConsumerIds.Concat(disconnected).OrderBy(id => id).ToList(),
            DisconnectedConsumerIds = disconnected,
        };
    }

    public static void Validate(WindPowerState state)
    {
        if (state.Nodes.Count > MaximumNodes || state.UnpoweredConsumerIds.Count > MaximumNodes
            || state.Nodes.Select(node => node.NetworkId).Distinct().Count() > MaximumNetworks
            || state.Nodes.Sum(node => (long)node.ConnectedNodeIds.Count) > MaximumConnections)
            throw new ArgumentException("The complete power topology exceeds its bounded observation limits.");
        if (state.Nodes.Any(node => node.NodeId <= 0 || node.EntityId <= 0 || node.NetworkId <= 0)
            || state.Nodes.Select(node => node.NodeId).Distinct().Count() != state.Nodes.Count
            || state.Nodes.Select(node => node.EntityId).Distinct().Count() != state.Nodes.Count
            || state.UnpoweredConsumerIds.Any(id => id <= 0)
            || state.UnpoweredConsumerIds.Distinct().Count() != state.UnpoweredConsumerIds.Count)
            throw new ArgumentException("Power node or unpowered-consumer identity is incomplete.");

        var byId = state.Nodes.ToDictionary(node => node.NodeId);
        foreach (var node in state.Nodes)
        {
            if (node.ConnectedNodeIds.Distinct().Count() != node.ConnectedNodeIds.Count
                || node.ConsumerIds.Any(id => id <= 0)
                || node.ConsumerIds.Distinct().Count() != node.ConsumerIds.Count
                || node.ConsumerIds.Intersect(state.UnpoweredConsumerIds).Any())
                throw new ArgumentException("Power node membership contains invalid or repeated references.");
            foreach (var neighborId in node.ConnectedNodeIds)
            {
                if (neighborId == node.NodeId || !byId.TryGetValue(neighborId, out var neighbor)
                    || neighbor.NetworkId != node.NetworkId || !neighbor.ConnectedNodeIds.Contains(node.NodeId))
                    throw new ArgumentException("A power connection is dangling, cross-network or nonreciprocal.");
            }
        }

        var groups = Components(state.Nodes);
        if (groups.Count != state.Nodes.Select(node => node.NetworkId).Distinct().Count())
            throw new ArgumentException("A native power network contains disconnected node components.");
        var consumers = groups.SelectMany(group => group.ConsumerIds).ToList();
        if (consumers.Distinct().Count() != consumers.Count)
            throw new ArgumentException("A consumer belongs to multiple native power networks.");
    }

    public static bool Matches(WindPowerProjection expected, WindPowerState actual)
    {
        Validate(actual);
        return NodeFingerprint(expected.Nodes, false) == NodeFingerprint(actual.Nodes, false)
            && ComponentFingerprint(expected.Components) == ComponentFingerprint(Components(actual.Nodes))
            && expected.UnpoweredConsumerIds.SequenceEqual(actual.UnpoweredConsumerIds.OrderBy(id => id));
    }

    public static string Fingerprint(WindPowerState state) => CanonicalStateHash.Combine(
        "wind-power-state-v1", NodeFingerprint(state.Nodes, true),
        string.Join(",", state.UnpoweredConsumerIds.OrderBy(id => id)));

    public static List<WindPowerComponent> Components(IReadOnlyList<WindPowerNode> nodes)
    {
        var byId = nodes.ToDictionary(node => node.NodeId);
        var remaining = new HashSet<int>(byId.Keys);
        var result = new List<WindPowerComponent>();
        foreach (var first in nodes.OrderBy(node => node.NodeId))
        {
            if (!remaining.Remove(first.NodeId)) continue;
            var pending = new Queue<int>();
            var members = new List<WindPowerNode>();
            pending.Enqueue(first.NodeId);
            while (pending.Count > 0)
            {
                var node = byId[pending.Dequeue()];
                members.Add(node);
                foreach (var neighbor in node.ConnectedNodeIds)
                    if (remaining.Remove(neighbor)) pending.Enqueue(neighbor);
            }
            result.Add(new WindPowerComponent
            {
                NodeIds = members.Select(node => node.NodeId).OrderBy(id => id).ToList(),
                ConsumerIds = members.SelectMany(node => node.ConsumerIds).Distinct().OrderBy(id => id).ToList(),
                GeneratorIds = members.Where(node => node.GeneratorId > 0).Select(node => node.GeneratorId).OrderBy(id => id).ToList(),
                AccumulatorIds = members.Where(node => node.AccumulatorId > 0).Select(node => node.AccumulatorId).OrderBy(id => id).ToList(),
                ExchangerIds = members.Where(node => node.ExchangerId > 0).Select(node => node.ExchangerId).OrderBy(id => id).ToList(),
            });
        }
        return result;
    }

    private static string NodeFingerprint(IEnumerable<WindPowerNode> nodes, bool includeNetworkIds)
    {
        var rows = new List<object?>();
        foreach (var node in nodes.OrderBy(node => node.NodeId))
        {
            rows.Add(CanonicalStateHash.Combine("wind-power-node-v1", node.NodeId, node.EntityId,
                includeNetworkIds ? node.NetworkId : 0, node.GeneratorId, node.AccumulatorId, node.ExchangerId,
                string.Join(",", node.ConnectedNodeIds.OrderBy(id => id)),
                string.Join(",", node.ConsumerIds.OrderBy(id => id))));
        }
        return CanonicalStateHash.Combine("wind-power-nodes-v1", rows.ToArray());
    }

    private static string ComponentFingerprint(IEnumerable<WindPowerComponent> groups)
    {
        var rows = new List<object?>();
        foreach (var group in groups.OrderBy(group => group.NodeIds[0]))
        {
            rows.Add(CanonicalStateHash.Combine("wind-power-component-v1", string.Join(",", group.NodeIds),
                string.Join(",", group.ConsumerIds), string.Join(",", group.GeneratorIds),
                string.Join(",", group.AccumulatorIds), string.Join(",", group.ExchangerIds)));
        }
        return CanonicalStateHash.Combine("wind-power-components-v1", rows.ToArray());
    }
}

public sealed class WindPowerState
{
    public List<WindPowerNode> Nodes { get; set; } = new List<WindPowerNode>();
    public List<int> UnpoweredConsumerIds { get; set; } = new List<int>();
}

public sealed class WindPowerNode
{
    public int NodeId { get; set; }
    public int EntityId { get; set; }
    public int NetworkId { get; set; }
    public int GeneratorId { get; set; }
    public int AccumulatorId { get; set; }
    public int ExchangerId { get; set; }
    public List<int> ConnectedNodeIds { get; set; } = new List<int>();
    public List<int> ConsumerIds { get; set; } = new List<int>();

    internal WindPowerNode WithoutConnection(int removedNodeId) => new WindPowerNode
    {
        NodeId = NodeId, EntityId = EntityId, NetworkId = NetworkId, GeneratorId = GeneratorId,
        AccumulatorId = AccumulatorId, ExchangerId = ExchangerId,
        ConnectedNodeIds = ConnectedNodeIds.Where(id => id != removedNodeId).ToList(),
        ConsumerIds = ConsumerIds.ToList(),
    };
}

public sealed class WindPowerProjection
{
    public WindPowerNode Target { get; set; } = new WindPowerNode();
    public List<WindPowerNode> Nodes { get; set; } = new List<WindPowerNode>();
    public List<WindPowerComponent> Components { get; set; } = new List<WindPowerComponent>();
    public List<int> UnpoweredConsumerIds { get; set; } = new List<int>();
    public List<int> DisconnectedConsumerIds { get; set; } = new List<int>();
}

public sealed class WindPowerComponent
{
    public List<int> NodeIds { get; set; } = new List<int>();
    public List<int> ConsumerIds { get; set; } = new List<int>();
    public List<int> GeneratorIds { get; set; } = new List<int>();
    public List<int> AccumulatorIds { get; set; } = new List<int>();
    public List<int> ExchangerIds { get; set; } = new List<int>();
}
