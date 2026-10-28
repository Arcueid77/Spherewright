using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    // Completed ordinary Tesla poles carry no fuel, cargo or generation. Prove
    // their original paid receipts and the whole zero-source power graph; this
    // does not admit wind generators, batteries, belts or powered production.
    private bool TryBindSavedUnpoweredPoles(Player player, PlanetFactory factory, string? journeyId,
        out HashSet<int> ids, bool requireUnpoweredGraph = true)
    {
        ids = new HashSet<int>();
        if (!_blueprints.TryRead(out var builds)) return false;
        var poles = builds.Where(b => b.Site.PlanetId == factory.planetId
            && b.Site.Objects.Count == 1 && b.Site.Objects[0].ItemId == 2201).ToArray();
        if (poles.Length == 0) return true;
        if (journeyId is null || poles.Length > 2 || factory.entityPool is null || factory.prebuildPool is null)
            return false;
        foreach (var b in poles)
        {
            if (b.Phase != "completed" || b.LastEvidenceGameTick > GameMain.gameTick
                || b.StructuredConstruction is not { Kind: "core" } provenance || provenance.JourneyId != journeyId
                || b.ConstructionControl is not { Phase: "completed_idle", PhysicalIdleVerified: true, BindingLost: false } control
                || control.OriginalDroneEnabled != player.mecha.constructionModule.droneEnabled
                || control.NativeCreateActionId != b.ActionId || string.IsNullOrEmpty(control.NativeCreateActionId)
                || control.StopReason != "construction_complete_draining"
                || !control.PhysicalIdleVerifiedAtGameTick.HasValue || control.PhysicalIdleVerifiedAtGameTick > GameMain.gameTick
                || !control.DisabledNativeFactoryPassGameTick.HasValue || control.DisabledNativeFactoryPassGameTick > GameMain.gameTick
                || b.Objects.Count != 1) return false;
            var step = b.Objects[0]; var id = step.EntityId.GetValueOrDefault();
            var retired = step.PrebuildId.GetValueOrDefault();
            if (step.State != BlueprintObjectStates.Completed || !step.SubmittedAtGameTick.HasValue
                || !step.CompletedAtGameTick.HasValue || step.CompletedAtGameTick > GameMain.gameTick
                || step.InventoryBefore.GetValueOrDefault() < 1 || step.InventoryAfter != step.InventoryBefore - 1
                || string.IsNullOrEmpty(step.BeforeInventoryHash) || string.IsNullOrEmpty(step.AfterInventoryHash)
                || retired < 1 || retired >= factory.prebuildPool.Length || factory.prebuildPool[retired].id != 0
                || control.PreexistingEntityIds.Contains(id) || !ids.Add(id)
                || !StructuredEntityMatches(factory, id, b, b.Site.Objects[0])
                || !StructuredConnectionsMatch(factory, b)) return false;
        }
        if (!requireUnpoweredGraph) return true; // Caller separately proves every known wind/node/network.
        var power = factory.powerSystem;
        if (power?.nodePool is null || power.netPool is null || power.genCursor != 1
            || power.nodeCursor != ids.Count + 1 || power.nodeCursor > power.nodePool.Length
            || power.netCursor < 1 || power.netCursor > ids.Count + 2 || power.netCursor > power.netPool.Length)
            return false;
        var nodes = new HashSet<int>();
        foreach (var id in ids)
        {
            var nodeId = factory.entityPool[id].powerNodeId;
            if (nodeId < 1 || nodeId >= power.nodeCursor || !nodes.Add(nodeId)
                || power.nodePool[nodeId].id != nodeId || power.nodePool[nodeId].entityId != id) return false;
        }
        var seen = new HashSet<int>();
        for (var n = 1; n < power.netCursor; n++)
        {
            var net = power.netPool[n];
            if (net is null) continue;
            if (net.nodes is null || net.generators is null || net.accumulators is null || net.exchangers is null
                || net.generators.Count != 0 || net.accumulators.Count != 0 || net.exchangers.Count != 0
                || net.energyStored != 0 || net.energyServed != 0 || net.energyCapacity != 0 || net.energyExport != 0
                || net.nodes.Any(node => node is null || !nodes.Contains(node.id) || !seen.Add(node.id)
                    || node.genId != 0 || node.accId != 0 || node.excId != 0
                    || node.conns is null || node.lines is null
                    || node.conns.Any(other => other is null || !nodes.Contains(other.id))
                    || node.lines.Any(other => other is null || !nodes.Contains(other.id)))) return false;
        }
        return seen.SetEquals(nodes);
    }

    private static bool SavedUnpoweredConsumerNetwork(PlanetFactory factory, int networkId, HashSet<int> poles)
    {
        if (networkId == 0) return true;
        var power = factory.powerSystem;
        return poles.Count > 0 && networkId > 0 && networkId < power.netCursor && power.netPool is not null
            && networkId < power.netPool.Length && power.netPool[networkId] is not null;
    }
}
