using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    // A completed connection can legitimately add ports to the older miner and
    // tower receipts. Its own immutable endpoint/topology certificate must prove
    // those changes. Exactly two paid ordinary winds may retain their mined1013
    // stock/cargo; no charger, other generator, battery, active work or Create.
    private bool SavedUnpoweredBeltChainMatches(Player player, PlanetFactory factory, string? journeyId,
        IReadOnlyList<BlueprintBuildState> saved, NativeResumeDecisionEvidence? evidence)
    {
        bool Require(string key, bool value) => evidence?.Require(key, value) ?? value;
        var local = saved.Where(b => b.Site.PlanetId == factory.planetId).ToArray();
        var belts = local.Where(b => b.StructuredConstruction?.Kind == "belt").ToArray();
        var miners = local.Where(b => b.Site.Objects.Count == 1 && b.Site.Objects[0].ItemId == 2301).ToArray();
        var towers = local.Where(b => b.Site.Objects.Count == 1 && b.Site.Objects[0].ItemId == 2104).ToArray();
        var winds = local.Where(b => b.Site.Objects.Count == 1 && b.Site.Objects[0].ItemId == 2203)
            .OrderBy(b => b.Objects[0].SubmittedAtGameTick).ToArray();
        var running = winds.Length == 2;
        if (!Require("saved_belt_chain_owned_factory", journeyId is not null && _sessions.IsCurrentSessionOwned
                && ReferenceEquals(GameMain.data?.localLoadedPlanetFactory, factory)
                && ReferenceEquals(GameMain.localPlanet?.factory, factory) && factory.planetId == player.planetId)
            || !Require("saved_belt_chain_receipt_shape", belts.Length == 1 && miners.Length == 1 && towers.Length == 1
                && (winds.Length == 0 || running))
            || !TryBindSavedUnpoweredPoles(player, factory, journeyId, out var poles, requireUnpoweredGraph: !running)) return false;
        var belt = belts[0]; var miner = miners[0]; var tower = towers[0];
        var sourceId = miner.Objects[0].EntityId.GetValueOrDefault();
        var destinationId = tower.Objects[0].EntityId.GetValueOrDefault();
        if (!Require("saved_belt_chain_closed_bounds", factory.entityPool is not null && factory.prebuildPool is not null
                && factory.entityCursor >= 1 && factory.entityCursor <= 131072 && factory.entityCursor <= factory.entityPool.Length
                && factory.prebuildCount == 0 && factory.prebuildCursor >= 1 && factory.prebuildCursor <= 131072
                && factory.prebuildCursor <= factory.prebuildPool.Length && local.Length == 3 + poles.Count + winds.Length
                && belt.Objects.Count >= 2 && belt.Objects.Count <= StructuredConstructionBinding.MaximumNewObjects(2001)
                && belt.Site.Objects.All(o => o.ItemId == 2001)
                && belt.StructuredConstruction!.SourceObjectId == sourceId
                && belt.StructuredConstruction.DestinationObjectId == destinationId
                && belt.StructuredConstruction.Endpoints.Count == 2
                && belt.StructuredConstruction.Endpoints.All(e => e.ExistingConnections.Count == 0))) return false;
        if (!Require("saved_belt_chain_physical_idle", TryObserveOwnedConstructionDrones(factory, player.mecha.constructionModule,
                out var idle, out _) && idle && belt.ConstructionControl is { } latest
                && latest.OriginalDroneEnabled == player.mecha.constructionModule.droneEnabled)) return false;
        var ids = new HashSet<int>();
        var submitted = belt.Objects.Min(o => o.SubmittedAtGameTick ?? long.MaxValue);
        foreach (var build in local)
        {
            var isTower = ReferenceEquals(build, tower);
            if (!Require("saved_belt_chain_sealed_paid_receipts", build.Phase == "completed" && build.LastEvidenceGameTick <= GameMain.gameTick
                    && build.StructuredConstruction?.JourneyId == journeyId
                    && build.ConstructionControl is { Phase: "completed_idle", PhysicalIdleVerified: true } control
                    && control.NativeCreateActionId == build.ActionId && !string.IsNullOrEmpty(control.NativeCreateActionId)
                    && control.PhysicalIdleVerifiedAtGameTick.HasValue && control.PhysicalIdleVerifiedAtGameTick <= GameMain.gameTick
                    && control.DisabledNativeFactoryPassGameTick.HasValue && control.DisabledNativeFactoryPassGameTick <= GameMain.gameTick
                    && (isTower ? HasSavedPassiveTowerReceipt(build, journeyId!, GameMain.gameTick)
                        : !control.BindingLost && control.StopReason == "construction_complete_draining"))) return false;
            var count = build.Objects.Count;
            if (count != build.Site.Objects.Count || count == 0) return false;
            var before = build.Objects[0].InventoryBefore; var after = build.Objects[0].InventoryAfter;
            var beforeHash = build.Objects[0].BeforeInventoryHash; var afterHash = build.Objects[0].AfterInventoryHash;
            if (!before.HasValue || after != before - count || before < count
                || string.IsNullOrEmpty(beforeHash) || string.IsNullOrEmpty(afterHash)) return false;
            foreach (var step in build.Objects)
            {
                var id = step.EntityId.GetValueOrDefault(); var retired = step.PrebuildId.GetValueOrDefault();
                if (step.State != BlueprintObjectStates.Completed || !step.SubmittedAtGameTick.HasValue
                    || !step.CompletedAtGameTick.HasValue || step.CompletedAtGameTick > GameMain.gameTick
                    || !ReferenceEquals(build, belt) && !winds.Contains(build) && step.CompletedAtGameTick > submitted
                    || step.InventoryBefore != before || step.InventoryAfter != after
                    || step.BeforeInventoryHash != beforeHash || step.AfterInventoryHash != afterHash
                    || retired < 1 || retired >= factory.prebuildPool!.Length || factory.prebuildPool[retired].id != 0
                    || id <= 0 || id >= factory.entityCursor || build.ConstructionControl!.PreexistingEntityIds.Contains(id)
                    || !ids.Add(id) || !StructuredEntityMatches(factory, id, build, build.Site.Objects[step.Index])) return false;
            }
        }
        // CaptureConstructionExistingEntities intentionally records only the
        // new batch's item kind for attribution. This one original belt batch
        // had no older belts; miner/tower/poles are proved by their own receipts.
        if (!Require("saved_belt_chain_no_prior_belts", belt.ConstructionControl!.PreexistingEntityIds.Count == 0)
            || !Require("saved_belt_chain_exact_native_membership", factory.entityCount == ids.Count
                && Enumerable.Range(1, factory.entityCursor - 1).All(i => factory.entityPool![i].id == 0 || ids.Contains(i)))
            || !Require("saved_belt_chain_retired_prebuilds", Enumerable.Range(1, factory.prebuildCursor - 1)
                .All(i => factory.prebuildPool![i].id == 0))
            || !Require("saved_belt_chain_exact_topology", StructuredConnectionsMatch(factory, belt))) return false;
        if (running && !Require("saved_belt_chain_exact_paid_winds", winds[0].ConstructionControl!.PreexistingEntityIds.Count == 0
                && new HashSet<int>(winds[1].ConstructionControl!.PreexistingEntityIds).SetEquals(new[] { winds[0].Objects[0].EntityId!.Value })
                && winds.All(w => w.Objects[0].SubmittedAtGameTick >= belt.Objects.Max(o => o.CompletedAtGameTick))
                && winds.All(w => StructuredConnectionsMatch(factory, w))
                && SavedKnownWindPowerGraph(factory, poles, winds))) return false;
        // The later connection proves every miner/tower port, while each older
        // receipt still proves that exact entity/pose/component/material debit.
        var source = factory.entityPool![sourceId]; var destination = factory.entityPool[destinationId];
        var power = factory.powerSystem; var system = factory.factorySystem; var transport = factory.transport;
        if (power?.consumerPool is null || system?.minerPool is null || transport?.stationPool is null
            || source.minerId <= 0 || source.minerId >= system.minerCursor || source.minerId >= system.minerPool.Length
            || source.powerConId <= 0 || source.powerConId >= power.consumerCursor || source.powerConId >= power.consumerPool.Length
            || destination.powerConId <= 0 || destination.powerConId >= power.consumerCursor || destination.powerConId >= power.consumerPool.Length
            || destination.stationId <= 0 || destination.stationId >= transport.stationCursor || destination.stationId >= transport.stationPool.Length
            || !running && (power.genCursor != 1 || poles.Count == 0 && (power.netCursor != 1 || power.nodeCursor != 1))) return false;
        var m = system.minerPool[source.minerId]; var station = transport.stationPool[destination.stationId];
        var mc = power.consumerPool[source.powerConId]; var sc = power.consumerPool[destination.powerConId];
        if (!Require("saved_belt_chain_known_source_and_supply", m.id == source.minerId && m.entityId == sourceId
                && m.pcId == source.powerConId && (running
                    ? (m.productId == 0 && m.productCount == 0 || m.productId == 1013 && m.productCount >= 0 && m.productCount <= 64)
                    : m.productId == 0 && m.productCount == 0)
                && mc.id == source.powerConId && mc.entityId == sourceId && SavedUnpoweredConsumerNetwork(factory, mc.networkId, poles)
                && sc.id == destination.powerConId && sc.entityId == destinationId && sc.networkId == 0
                && station is not null && station.id == destination.stationId && station.entityId == destinationId
                && station.isStellar && station.planetId == factory.planetId && station.gid > 0
                && !station.isCollector && !station.isVeinCollector && station.energy == 0 && station.warperCount == 0
                && station.idleShipCount == 0 && station.workShipCount == 0 && station.idleDroneCount == 0 && station.workDroneCount == 0
                && station.storage is { Length: 5 } && station.slots is { Length: 12 }
                && station.storage[0].itemId == 1013 && station.storage[0].max == 100
                && station.storage[0].localLogic == ELogisticStorage.None && station.storage[0].remoteLogic == ELogisticStorage.Supply
                && station.storage.All(s => s.inc == 0 && s.localOrder == 0)
                && (running ? station.storage[0].count >= 0 && station.storage[0].count <= 100
                    : station.storage[0].count == 0 && station.storage[0].remoteOrder == 0)
                && station.storage.Skip(1).All(s => s.itemId == 0 && s.max == 0 && s.count == 0 && s.remoteOrder == 0
                    && s.localLogic == ELogisticStorage.None && s.remoteLogic == ELogisticStorage.None))) return false;
        var beltIds = new HashSet<int>(belt.Objects.Select(o => o.EntityId!.Value));
        var inputPorts = station!.slots.Where(s => s.beltId != 0).ToArray();
        if (!Require("saved_belt_chain_one_empty_input_port", inputPorts.Length == 1 && inputPorts[0].dir == IODir.Input
                && inputPorts[0].counter == 0 && inputPorts[0].storageIdx == 0
                && station.slots.Where(s => s.beltId == 0).All(s => s.dir == IODir.None && s.counter == 0 && s.storageIdx == 0)
                && beltIds.Any(id => factory.entityPool[id].beltId == inputPorts[0].beltId))) return false;
        return Require("saved_belt_chain_complete_known_cargo_paths", SavedBeltPathsEmpty(factory, beltIds, running));
    }

    private static bool SavedBeltPathsEmpty(PlanetFactory factory, HashSet<int> expected, bool allowMinedCargo = false)
    {
        var traffic = factory.cargoTraffic;
        if (traffic?.beltPool is null || traffic.pathPool is null || traffic.beltCursor < 1 || traffic.beltCursor > 131072
            || traffic.beltCursor > traffic.beltPool.Length || traffic.pathCursor < 1 || traffic.pathCursor > 131072
            || traffic.pathCursor > traffic.pathPool.Length) return false;
        var paths = new HashSet<int>(); var seen = new HashSet<int>();
        foreach (var id in expected)
        {
            var beltId = factory.entityPool[id].beltId;
            if (beltId < 1 || beltId >= traffic.beltCursor) return false;
            var belt = traffic.beltPool[beltId];
            if (belt.id != beltId || belt.entityId != id || belt.segPathId <= 0 || belt.segPathId >= traffic.pathCursor) return false;
            if (!paths.Add(belt.segPathId)) continue;
            var path = traffic.GetCargoPath(belt.segPathId);
            if (path is null || path.id != belt.segPathId || path.closed || path.buffer is null
                || !ReferenceEquals(path.cargoContainer, factory.cargoContainer) || !Monitor.TryEnter(path.buffer)) return false;
            try
            {
                if (!NativeBeltPathCapture.TryRead(path, out var export, out _) || export!.Closed || export.OutputPathId != 0
                    || export.InputPathIds.Count != 0 || !BeltUpgradePathPolicy.TryLocateAllCargo(export, out var cargo, out _)
                    || !allowMinedCargo && cargo.Count != 0)
                    return false;
                foreach (var reference in cargo)
                    if (!path.GetCargoAtIndex(reference.ObservedPathCell, out var item, out var cargoId, out _)
                        || cargoId != reference.CargoId || item.item != 1013 || item.inc != 0 || item.stack < 1 || item.stack > 4) return false;
                foreach (var member in export.BeltIds)
                {
                    if (member < 1 || member >= traffic.beltCursor) return false;
                    var b = traffic.beltPool[member];
                    if (b.id != member || b.segPathId != path.id || b.segIndex < 0 || b.segLength <= 0
                        || (long)b.segIndex + b.segLength > export.Length || !expected.Contains(b.entityId)
                        || factory.entityPool[b.entityId].beltId != member || !seen.Add(b.entityId)) return false;
                }
            }
            finally { Monitor.Exit(path.buffer); }
        }
        return seen.SetEquals(expected) && Enumerable.Range(1, traffic.pathCursor - 1)
            .All(i => traffic.pathPool[i] is null || traffic.pathPool[i]!.id == 0 || paths.Contains(i));
    }

    private static bool SavedKnownWindPowerGraph(PlanetFactory factory, HashSet<int> poles, BlueprintBuildState[] winds)
    {
        var power = factory.powerSystem;
        if (power?.nodePool is null || power.genPool is null || power.netPool is null || power.genCursor != 3
            || power.genCursor > power.genPool.Length || power.nodeCursor != poles.Count + 3 || power.nodeCursor > power.nodePool.Length
            || power.netCursor < 2 || power.netCursor > poles.Count + 4 || power.netCursor > power.netPool.Length) return false;
        var nodes = new HashSet<int>(); var generators = new HashSet<int>();
        foreach (var id in poles.Concat(winds.Select(w => w.Objects[0].EntityId!.Value)))
        {
            var e = factory.entityPool[id]; var nodeId = e.powerNodeId;
            if (nodeId <= 0 || nodeId >= power.nodeCursor || !nodes.Add(nodeId)
                || power.nodePool[nodeId].id != nodeId || power.nodePool[nodeId].entityId != id) return false;
            if (poles.Contains(id)) continue;
            if (e.powerGenId <= 0 || e.powerGenId >= power.genCursor || !generators.Add(e.powerGenId)) return false;
            var g = power.genPool[e.powerGenId];
            if (g.id != e.powerGenId || g.entityId != id || !g.wind) return false;
        }
        var seen = new HashSet<int>(); var generatorCount = 0;
        for (var i = 1; i < power.netCursor; i++)
        {
            var net = power.netPool[i]; if (net is null) continue;
            if (net.nodes is null || net.generators is null || net.accumulators is null || net.exchangers is null
                || net.accumulators.Count != 0 || net.exchangers.Count != 0 || net.energyStored != 0 || net.energyExport != 0
                || net.nodes.Any(n => n is null || !nodes.Contains(n.id) || !seen.Add(n.id)
                    || n.genId != 0 && !generators.Contains(n.genId) || n.accId != 0 || n.excId != 0
                    || n.conns is null || n.lines is null || n.conns.Any(x => x is null || !nodes.Contains(x.id))
                    || n.lines.Any(x => x is null || !nodes.Contains(x.id)))) return false;
            generatorCount += net.generators.Count;
        }
        return seen.SetEquals(nodes) && generators.Count == 2 && generatorCount == 2;
    }
}
