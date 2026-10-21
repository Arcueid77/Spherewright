using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Progression;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Progression;

public static class ResearchQueuePolicy
{
    public static ResearchQueueReadback ProveEnqueue(ProgressionStateSnapshot before,
        ProgressionStateSnapshot after, PlayerStateSnapshot playerBefore,
        PlayerStateSnapshot playerAfter, int targetTech)
    {
        if (before is null || after is null || playerBefore is null || playerAfter is null
            || string.IsNullOrWhiteSpace(before.SessionId) || before.PlanetId <= 0 || targetTech <= 0
            || after.SessionId != before.SessionId || playerBefore.SessionId != before.SessionId
            || playerAfter.SessionId != before.SessionId || after.PlanetId != before.PlanetId
            || playerBefore.PlanetId != before.PlanetId || playerAfter.PlanetId != before.PlanetId
            || before.CapturedAtGameTick < 0 || after.CapturedAtGameTick != before.CapturedAtGameTick
            || playerBefore.CapturedAtGameTick != before.CapturedAtGameTick
            || playerAfter.CapturedAtGameTick != before.CapturedAtGameTick
            || before.TechQueue is null || after.TechQueue is null || before.TechQueue.Count >= 64
            || before.TechQueue.Any(id => id <= 0) || before.CurrentTechId < 0
            || (before.TechQueue.Count == 0 && before.CurrentTechId != 0)
            || (before.CurrentTechId > 0 && before.TechQueue[0] != before.CurrentTechId)
            || !after.TechQueue.SequenceEqual(before.TechQueue.Concat(new[] { targetTech }))
            || after.CurrentTechId != (before.TechQueue.Count == 0 ? targetTech : before.CurrentTechId)
            || ProgressHash(before.Technologies) != ProgressHash(after.Technologies)
            || ResearchMaterialHash(playerBefore) != ResearchMaterialHash(playerAfter))
            throw new InvalidOperationException("Native enqueue queue, progress or material preservation could not be proved.");

        return new ResearchQueueReadback
        {
            CapturedAtGameTick = after.CapturedAtGameTick,
            BeforeQueue = before.TechQueue.ToList(), AfterQueue = after.TechQueue.ToList(),
            PreviousCurrentTechId = before.CurrentTechId, CurrentTechId = after.CurrentTechId,
            ResearchProgressPreserved = true, InventoryPreserved = true,
        };
    }

    private static string ResearchMaterialHash(PlayerStateSnapshot player)
    {
        var fields = new List<object?> { player.InventorySlotCount, player.InventoryOccupiedSlotCount,
            player.FuelStorageSlotCount, player.FuelStorageOccupiedSlotCount,
            player.ReactorItemId, player.ReactorItemInc, player.AutoManageResearchItems };
        foreach (var item in player.Inventory.OrderBy(item => item.ItemId))
            fields.Add(CanonicalStateHash.Combine("inventory", item.ItemId, item.Count, item.Inc, item.SlotCount));
        foreach (var item in player.FuelStorage.OrderBy(item => item.ItemId))
            fields.Add(CanonicalStateHash.Combine("fuel", item.ItemId, item.Count, item.Inc, item.SlotCount));
        fields.Add(player.InHandItem is null ? "no-hand-item" : CanonicalStateHash.Combine("hand",
            player.InHandItem.ItemId, player.InHandItem.Count, player.InHandItem.Inc, player.InHandItem.SlotCount));
        foreach (var item in player.MechaResearchItemBuffer.OrderBy(item => item.ItemId))
            fields.Add(CanonicalStateHash.Combine("research-buffer", item.ItemId,
                item.PointCount, item.WholeItemCount, item.RemainderPoints));
        return CanonicalStateHash.Combine("research-material-preservation-v1", fields.ToArray());
    }

    // The native SortTechQueue argument is a permutation of EVERY array index,
    // including trailing empty slots. Never write the queue or research fields directly.
    public static int[] Prioritize(IReadOnlyList<int> queue, int currentTech, int targetTech, bool prerequisitesUnlocked)
    {
        if (queue is null || queue.Count < 2 || queue.Count > 64 || targetTech <= 0
            || currentTech <= 0 || currentTech != queue[0] || !prerequisitesUnlocked
            || queue.Any(id => id < 0))
            throw new ArgumentException("An active bounded queue and completed native prerequisites are required.");
        var nonempty = queue.Where(id => id > 0).ToArray();
        if (nonempty.Distinct().Count() != nonempty.Length
            || !queue.Take(nonempty.Length).SequenceEqual(nonempty))
            throw new ArgumentException("Ambiguous repeated levels or holes in the native queue are unsupported.");
        var index = Array.IndexOf(nonempty, targetTech);
        if (index <= 0) throw new ArgumentException("Choose an already queued technology behind the active head.");
        return new[] { index }.Concat(Enumerable.Range(0, queue.Count).Where(i => i != index)).ToArray();
    }

    public static string ProgressHash(IEnumerable<TechStateSnapshot> technologies) => CanonicalStateHash.Combine(
        "research-progress-preservation-v1", technologies.OrderBy(t => t.TechId).Select(t => (object)CanonicalStateHash.Combine(
            "technology", t.TechId, t.HashUploaded, t.HashRequired, t.CurrentLevel, t.MaximumLevel, t.Unlocked, t.UnlockTick)).ToArray());
}
