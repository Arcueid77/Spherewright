using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Factory;

public static class BlueprintSitePolicy
{
    public const int MaximumPlacementObjects = 32;

    public static void ValidateRequest(BlueprintSiteRequest request)
    {
        if (request is null || request.StateHashVersion != 1
            || request.QuarterTurns < 0 || request.QuarterTurns > 3 || !Finite(request.Position)
            || string.IsNullOrWhiteSpace(request.ExpectedPlayerStateHash))
            throw new BlueprintReadException("blueprint_site_request_invalid");
    }

    // Native output/input fields are a graph, not arbitrary function calls. Restrict this
    // first placement subset to closed sorter ends and ordinary belt-to-belt connections.
    public static List<BlueprintPlanConnection> BuildConnections(BlueprintInspection inspection,
        IReadOnlyDictionary<int, int> nativeSlotCounts)
    {
        var objects = inspection.Objects;
        if (objects.Count < 1 || objects.Count > MaximumPlacementObjects
            || objects.Where((o, i) => o is null || o.Index != i).Any())
            throw new BlueprintReadException("blueprint_site_object_limit");
        var edges = new List<BlueprintPlanConnection>();
        foreach (var obj in objects)
        {
            if (!BoundedBlueprintReader.SupportsItem(obj.ItemId))
                throw new BlueprintReadException("blueprint_site_type_unsupported");
            if (obj.ItemId >= 2011 && obj.ItemId <= 2013)
            {
                if (obj.InputObjectIndex < 0 || obj.OutputObjectIndex < 0
                    || obj.InputToSlot != 1 || obj.OutputFromSlot != 0)
                    throw new BlueprintReadException("blueprint_site_open_sorter_unsupported");
                Add(obj.InputObjectIndex, obj.InputFromSlot, obj.Index, 1);
                Add(obj.Index, 0, obj.OutputObjectIndex, obj.OutputToSlot);
            }
            else if (IsBelt(obj.ItemId))
            {
                if (obj.InputObjectIndex != -1 || (obj.OutputObjectIndex >= 0
                    && (obj.OutputFromSlot != 0 || obj.OutputObjectIndex >= objects.Count
                        || !IsBelt(objects[obj.OutputObjectIndex].ItemId)
                        || obj.OutputToSlot < 1 || obj.OutputToSlot > 3)))
                    throw new BlueprintReadException("blueprint_site_belt_endpoint_unsupported");
                if (obj.OutputObjectIndex >= 0) Add(obj.Index, 0, obj.OutputObjectIndex, obj.OutputToSlot);
            }
            else if (obj.InputObjectIndex != -1 || obj.OutputObjectIndex != -1)
                throw new BlueprintReadException("blueprint_site_device_link_unsupported");
        }
        var occupied = new HashSet<string>(StringComparer.Ordinal);
        var virtualCounts = new Dictionary<int, int>();
        foreach (var edge in edges)
        {
            Claim(edge.FromIndex, edge.FromSlot);
            Claim(edge.ToIndex, edge.ToSlot);
        }
        // A belt loop is valid data but not supported by the dependency-ordered executor.
        _ = ExecutionOrder(inspection);
        return edges;

        void Add(int from, int fromSlot, int to, int toSlot)
        {
            if (from < 0 || to < 0 || from >= objects.Count || to >= objects.Count || from == to)
                throw new BlueprintReadException("blueprint_site_connection_invalid");
            var fromSorter = IsSorter(objects[from].ItemId);
            var toSorter = IsSorter(objects[to].ItemId);
            if (fromSorter && toSorter) throw new BlueprintReadException("blueprint_site_sorter_to_sorter_unsupported");
            if (fromSorter || toSorter)
            {
                var other = fromSorter ? objects[to] : objects[from];
                var slot = fromSorter ? toSlot : fromSlot;
                if (IsBelt(other.ItemId) ? slot != -1
                    : (other.ItemId != 2101 && (other.ItemId < 2302 || other.ItemId > 2305))
                        || !nativeSlotCounts.TryGetValue(other.ItemId, out var count) || slot < 0
                        || slot >= Math.Min(other.ItemId == 2101 ? 12 : 16, count))
                    throw new BlueprintReadException("blueprint_site_native_slot_invalid");
            }
            edges.Add(new BlueprintPlanConnection { FromIndex = from, FromSlot = fromSlot, ToIndex = to, ToSlot = toSlot });
        }

        void Claim(int index, int slot)
        {
            if (slot == -1)
            {
                virtualCounts.TryGetValue(index, out var count);
                if (!IsBelt(objects[index].ItemId) || count >= 8)
                    throw new BlueprintReadException("blueprint_site_virtual_slots_exhausted");
                virtualCounts[index] = count + 1; // Native WriteObjectConn searches slots4..11.
            }
            else if (slot < 0 || slot >= 16 || !occupied.Add(index + ":" + slot))
                throw new BlueprintReadException("blueprint_site_slot_conflict");
        }
    }

    public static List<int> Dependencies(BlueprintInspection inspection, int index)
    {
        var obj = inspection.Objects[index];
        return IsSorter(obj.ItemId) ? new[] { obj.InputObjectIndex, obj.OutputObjectIndex }.Distinct().OrderBy(i => i).ToList()
            : IsBelt(obj.ItemId) && obj.OutputObjectIndex >= 0 ? new List<int> { obj.OutputObjectIndex } : new List<int>();
    }

    // A connection graph includes links installed by OTHER objects. In particular a
    // belt's virtual pickup links belong to sorter creation, never its native output.
    public static BlueprintPlanConnection? CreationOutput(BlueprintSiteSnapshot site, int index) =>
        CreationConnection(site, index, input: false);

    public static BlueprintPlanConnection? CreationInput(BlueprintSiteSnapshot site, int index) =>
        CreationConnection(site, index, input: true);

    private static BlueprintPlanConnection? CreationConnection(BlueprintSiteSnapshot site, int index, bool input)
    {
        if (site is null || site.Objects is null || site.Connections is null
            || index < 0 || index >= site.Objects.Count || site.Objects.Count > MaximumPlacementObjects
            || site.Objects.Any(o => o is null))
            throw new BlueprintReadException("blueprint_site_creation_graph_invalid");
        var itemId = site.Objects[index].ItemId;
        BlueprintPlanConnection? result = null;
        foreach (var edge in site.Connections)
        {
            if (edge is null || edge.FromIndex < 0 || edge.FromIndex >= site.Objects.Count
                || edge.ToIndex < 0 || edge.ToIndex >= site.Objects.Count)
                throw new BlueprintReadException("blueprint_site_creation_graph_invalid");
            var installsEdge = input ? IsSorter(itemId) && edge.ToIndex == index
                : edge.FromIndex == index && (IsSorter(itemId)
                    || IsBelt(itemId) && IsBelt(site.Objects[edge.ToIndex].ItemId));
            if (!installsEdge) continue;
            if (result is not null)
                throw new BlueprintReadException("blueprint_site_creation_edge_ambiguous");
            result = edge;
        }
        return result;
    }

    public static List<int> ExecutionOrder(BlueprintInspection inspection)
    {
        var result = new List<int>();
        var pending = new HashSet<int>(Enumerable.Range(0, inspection.Objects.Count));
        while (pending.Count > 0)
        {
            var ready = pending.OrderBy(i => i).Where(i => Dependencies(inspection, i).All(result.Contains)).ToArray();
            if (ready.Length == 0) throw new BlueprintReadException("blueprint_site_dependency_cycle_unsupported");
            foreach (var i in ready) { pending.Remove(i); result.Add(i); }
        }
        return result;
    }

    public static string AssessmentHash(BlueprintSiteSnapshot site, string playerHash)
    {
        var parts = new List<object> { "blueprint-site-v1", site.SessionId, site.PlanetId, site.Revision,
            site.BlueprintHash, playerHash, site.Position.X, site.Position.Y, site.Position.Z,
            site.QuarterTurns, site.NativeCheckPerformed, site.NativeCheckPassed, site.TechnologySatisfied, site.NativeBlueprintObjectLimit };
        foreach (var obj in site.Objects.OrderBy(o => o.Index))
        {
              if (obj.NativeModelIndex.HasValue)
                  parts.Add(CanonicalStateHash.Combine("native-site-model-v1", obj.NativeModelIndex.Value));
            parts.AddRange(new object[] { obj.Index, obj.ItemId, obj.RecipeId, obj.FilterItemId,
                obj.Position.X, obj.Position.Y, obj.Position.Z, obj.Position2.X, obj.Position2.Y, obj.Position2.Z,
                obj.Rotation.X, obj.Rotation.Y, obj.Rotation.Z, obj.Rotation.W,
                obj.Rotation2.X, obj.Rotation2.Y, obj.Rotation2.Z, obj.Rotation2.W,
                obj.Tilt, obj.InputOffset, obj.OutputOffset, obj.NativeCondition, obj.OccupiedObjectId ?? 0 });
            parts.Add(CanonicalStateHash.Combine("parameters", obj.Parameters.Cast<object>().ToArray()));
            parts.Add(CanonicalStateHash.Combine("dependencies", obj.Dependencies.Cast<object>().ToArray()));
        }
        foreach (var edge in site.Connections)
            parts.AddRange(new object[] { edge.FromIndex, edge.FromSlot, edge.ToIndex, edge.ToSlot });
        foreach (var item in site.ConstructionItems.OrderBy(i => i.ItemId))
            parts.AddRange(new object[] { item.ItemId, item.RequiredCount, item.PackageCount, item.MissingCount });
        foreach (var blocker in site.Blockers) parts.Add(blocker);
        return CanonicalStateHash.Combine("blueprint-site-assessment-v1", parts.ToArray());
    }

    private static bool IsBelt(int id) => id >= 2001 && id <= 2003;
    private static bool IsSorter(int id) => id >= 2011 && id <= 2013;
    private static bool Finite(Vector3Snapshot? p) => p is not null
        && new[] { p.X, p.Y, p.Z }.All(v => !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) <= 10000);
}
