namespace Spherewright.Bridge.Core.Factory;

// Copied Native ports checked against the union of immutable paid build
// receipts. A later paid connection may extend an older machine's ports;
// an unreceipted edge, missing edge or one-sided native link never qualifies.
public static class SavedFactoryTopologyPolicy
{
    public static string? Validate(IReadOnlyCollection<int> objectIds,
        IReadOnlyCollection<SavedFactoryEdge> expected, IReadOnlyCollection<SavedFactoryPort> observed)
    {
        if (objectIds is null || expected is null || observed is null || objectIds.Count < 1 || objectIds.Count > 512
            || expected.Count > 8192 || observed.Count > 8192 || objectIds.Any(id => id == 0)
            || objectIds.Distinct().Count() != objectIds.Count) return "saved_factory_topology_bounds";
        var ids = new HashSet<int>(objectIds);
        var ports = new Dictionary<(int, int), SavedFactoryPort>();
        foreach (var port in observed)
        {
            if (port is null || !ids.Contains(port.ObjectId) || !ids.Contains(port.OtherObjectId)
                || port.ObjectId == port.OtherObjectId || port.Slot < 0 || port.Slot >= 16
                || port.OtherSlot < 0 || port.OtherSlot >= 16 || ports.ContainsKey((port.ObjectId, port.Slot)))
                return "saved_factory_native_port_invalid";
            ports.Add((port.ObjectId, port.Slot), port);
        }
        var wanted = new Dictionary<(int, int), List<SavedFactoryEdge>>();
        foreach (var edge in expected)
        {
            if (edge is null || !ids.Contains(edge.From) || !ids.Contains(edge.To) || edge.From == edge.To
                || edge.FromSlot < -1 || edge.FromSlot >= 16 || edge.ToSlot < -1 || edge.ToSlot >= 16)
                return "saved_factory_receipt_edge_invalid";
            if (!wanted.TryGetValue((edge.From, edge.To), out var list)) wanted[(edge.From, edge.To)] = list = new();
            list.Add(edge);
        }
        var actual = new Dictionary<(int, int), List<SavedFactoryPort>>();
        foreach (var port in observed)
        {
            if (!ports.TryGetValue((port.OtherObjectId, port.OtherSlot), out var reverse)
                || reverse.OtherObjectId != port.ObjectId || reverse.OtherSlot != port.Slot || reverse.IsOutput == port.IsOutput)
                return "saved_factory_native_edge_not_reciprocal";
            if (!port.IsOutput) continue;
            if (!wanted.TryGetValue((port.ObjectId, port.OtherObjectId), out var list)
                || !list.Any(e => Matches(e, port))) return "saved_factory_unreceipted_edge";
            if (!actual.TryGetValue((port.ObjectId, port.OtherObjectId), out var values))
                actual[(port.ObjectId, port.OtherObjectId)] = values = new();
            values.Add(port);
        }
        foreach (var edge in expected)
            if (!actual.TryGetValue((edge.From, edge.To), out var list) || !list.Any(p => Matches(edge, p)))
                return "saved_factory_receipted_edge_missing";
        return null;
    }

    private static bool Matches(SavedFactoryEdge edge, SavedFactoryPort port) =>
        (edge.FromSlot < 0 || edge.FromSlot == port.Slot) && (edge.ToSlot < 0 || edge.ToSlot == port.OtherSlot);
}

public sealed class SavedFactoryEdge
{
    public int From { get; set; }
    public int FromSlot { get; set; }
    public int To { get; set; }
    public int ToSlot { get; set; }
}

public sealed class SavedFactoryPort
{
    public int ObjectId { get; set; }
    public int Slot { get; set; }
    public bool IsOutput { get; set; }
    public int OtherObjectId { get; set; }
    public int OtherSlot { get; set; }
}
