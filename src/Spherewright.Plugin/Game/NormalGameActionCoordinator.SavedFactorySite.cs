using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    // This does not load, initialize, build, configure or run a factory. It
    // extends recovery only when every live local object and edge has its own
    // already-paid durable receipt. All first-frame/contact/energy gates remain.
    private bool SavedFactorySiteMatches(Player player, PlanetFactory factory, string? journeyId,
        IReadOnlyList<BlueprintBuildState> saved, NativeResumeDecisionEvidence? evidence)
    {
        bool Require(string key, bool value) => evidence?.Require(key, value) ?? value;
        var local = saved.Where(b => b.Site.PlanetId == factory.planetId && b.Objects.Any(o => o.SubmittedAtGameTick.HasValue)).ToArray();
        if (!Require("saved_factory_site_owned_binding", journeyId is not null && _sessions.IsCurrentSessionOwned
                && SavedFactoryPlayerMatches(player) && player.planetId == factory.planetId
                && ReferenceEquals(factory, GameMain.data?.localLoadedPlanetFactory) && ReferenceEquals(factory, GameMain.localPlanet?.factory))
            || !Require("saved_factory_site_native_bounds", local.Length >= 1 && local.Length <= BlueprintBuildStore.MaximumBuilds
                && factory.entityPool is not null && factory.entityCursor >= 1 && factory.entityCursor <= 131072
                && factory.entityCursor <= factory.entityPool.Length && factory.prebuildPool is not null
                && factory.prebuildCursor >= 1 && factory.prebuildCursor <= 131072 && factory.prebuildCursor <= factory.prebuildPool.Length
                && factory.constructionSystem?.constructionModules is not null && factory.constructionSystem.constructStats is not null
                && factory.constructionSystem.constructionModules.count == 0 && factory.constructionSystem.constructStats.count == 0)
            || !Require("saved_factory_site_physical_idle", TryObserveOwnedConstructionDrones(factory, player.mecha.constructionModule,
                out var idle, out _) && (idle || HasCurrentSavedFactoryTail(player,journeyId)))) return false;
        var ids = new HashSet<int>(); var entities = new HashSet<int>(); var pending = new HashSet<int>();
        var edges = new List<SavedFactoryEdge>();
        foreach (var build in local)
        {
            var binding = build.StructuredConstruction;
            var control = build.ConstructionControl;
            var tailCertified=build.SavedTailCompletion?.Matches(build,GameMain.gameTick)==true;
            var stopCertified=SavedPaidConstructionStop.Current(build,GameMain.gameTick) is not null;
            var currentTail=HasCurrentSavedFactoryTailBuild(player,journeyId,build);
            var complete = PaidFactoryCompletionPolicy.HasCompletedIdleProof(build, GameMain.gameTick)
                || currentTail && build.Objects.All(o=>o.State==BlueprintObjectStates.Completed);
            var legacyTower = binding is not null && build.Site.Objects.Count == 1 && build.Site.Objects[0].ItemId == 2104
                && HasSavedPassiveTowerReceipt(build, binding.JourneyId, GameMain.gameTick);
            if (!Require("saved_factory_site_paid_idle_receipts", binding is not null && control is not null
                    && build.LastEvidenceGameTick <= GameMain.gameTick && build.Objects.Count == build.Site.Objects.Count
                    && (tailCertified || stopCertified || currentTail || control.PhysicalIdleVerified && control.PhysicalIdleVerifiedAtGameTick <= GameMain.gameTick
                    && control.DisabledNativeFactoryPassGameTick <= GameMain.gameTick && (!control.BindingLost || legacyTower)
                    && Guid.TryParse(control.NativeCreateActionId, out _)
                    && (complete && control.Phase == "completed_idle"
                        || build.Phase == "paused" && control.Phase == "paused_idle" && control.DispatchDisabled
                            && binding.JourneyId == journeyId && !player.mecha.constructionModule.droneEnabled))
                    && build.Objects.All(o => o.State == BlueprintObjectStates.Completed || o.State == BlueprintObjectStates.PendingConstruction))) return false;
            var first = build.Objects[0]; var count = build.Objects.Count;
            if (!first.InventoryBefore.HasValue || first.InventoryBefore < count || first.InventoryAfter != first.InventoryBefore - count
                || string.IsNullOrEmpty(first.BeforeInventoryHash) || string.IsNullOrEmpty(first.AfterInventoryHash)) return false;
            var batchIds = new int[count];
            for (var i = 0; i < count; i++)
            {
                var step = build.Objects[i]; var spec = build.Site.Objects[step.Index];
                if (step.InventoryBefore != first.InventoryBefore || step.InventoryAfter != first.InventoryAfter
                    || step.BeforeInventoryHash != first.BeforeInventoryHash || step.AfterInventoryHash != first.AfterInventoryHash
                    || !step.SubmittedAtGameTick.HasValue || step.SubmittedAtGameTick > GameMain.gameTick || step.PrebuildId.GetValueOrDefault() <= 0) return false;
                if (step.State == BlueprintObjectStates.Completed)
                {
                    var id = step.EntityId.GetValueOrDefault();
                    if (!step.CompletedAtGameTick.HasValue || step.CompletedAtGameTick > GameMain.gameTick
                        || control!.PreexistingEntityIds.Contains(id) || !entities.Add(id)
                        || !StructuredEntityMatches(factory, id, build, spec, allowCurrentSorterFilter: true)) return false;
                    batchIds[i] = id;
                }
                else
                {
                    var id = step.PrebuildId!.Value;
                    if (complete || !pending.Add(id) || !BlueprintPrebuildMatches(factory, id, spec)) return false;
                    batchIds[i] = -id;
                }
                if (!ids.Add(batchIds[i]) || ids.Count > 512) return false;
            }
            for (var i = 0; i < count; i++)
            {
                var step = binding!.Steps[i];
                var input = step.InputStepIndex >= 0 ? batchIds[step.InputStepIndex] : step.InputObjectId;
                var output = step.OutputStepIndex >= 0 ? batchIds[step.OutputStepIndex] : step.OutputObjectId;
                if (input != 0) edges.Add(new SavedFactoryEdge { From = input, FromSlot = step.InputFromSlot, To = batchIds[i], ToSlot = step.InputToSlot });
                if (output != 0) edges.Add(new SavedFactoryEdge { From = batchIds[i], FromSlot = step.OutputFromSlot, To = output, ToSlot = step.OutputToSlot });
            }
            foreach (var endpoint in binding!.Endpoints)
            {
                if (endpoint.ObjectId <= 0 || endpoint.ObjectId >= factory.entityCursor) return false;
                ref var e = ref factory.entityPool![endpoint.ObjectId];
                if (e.id != endpoint.ObjectId || e.protoId != endpoint.ItemId || Vector3.Distance(e.pos, ToVector(endpoint.Position)) > .003f
                    || (endpoint.ItemId == 2001 ? !ProvesNativeBeltRotation(factory, endpoint.ObjectId)
                        : Quaternion.Angle(e.rot, BlueprintRotation(endpoint.Rotation)) > .1f)) return false;
                foreach (var old in endpoint.ExistingConnections)
                    edges.Add(new SavedFactoryEdge { From = old.IsOutput ? endpoint.ObjectId : old.OtherObjectId,
                        FromSlot = old.IsOutput ? old.Slot : old.OtherSlot, To = old.IsOutput ? old.OtherObjectId : endpoint.ObjectId,
                        ToSlot = old.IsOutput ? old.OtherSlot : old.Slot });
            }
        }
        if (!Require("saved_factory_site_exact_entity_membership", entities.Count == factory.entityCount
                && Enumerable.Range(1, factory.entityCursor - 1).All(i => factory.entityPool![i].id == 0
                    || factory.entityPool[i].id == i && entities.Contains(i)))
            || !Require("saved_factory_site_exact_prebuild_membership", pending.Count == factory.prebuildCount
                && Enumerable.Range(1, factory.prebuildCursor - 1).All(i => factory.prebuildPool![i].id == 0
                    || factory.prebuildPool[i].id == i && pending.Contains(i)))) return false;
        foreach (var id in entities)
        {
            var entity = factory.entityPool![id];
            if (entity.stationId <= 0) continue;
            var transport = factory.transport;
            if (!Require("saved_factory_site_passive_station", transport?.stationPool is not null
                    && entity.stationId < transport.stationCursor && entity.stationId < transport.stationPool.Length
                    && transport.stationPool[entity.stationId] is { } station && station.id == entity.stationId
                    && station.entityId == id && station.planetId == factory.planetId && station.isStellar
                    && !station.isCollector && !station.isVeinCollector && station.idleShipCount == 0 && station.workShipCount == 0
                    && station.idleDroneCount == 0 && station.workDroneCount == 0 && station.warperCount == 0
                    && station.storage is { Length: 5 } && station.slots is { Length: 12 }
                    && station.storage.All(s => s.itemId >= 0 && s.count >= 0 && s.inc >= 0 && (long)s.inc <= (long)s.count * 10
                        && s.max >= 0 && (s.itemId == 0 ? s.count == 0 && s.inc == 0 : SavedFactoryItemAvailable(s.itemId))
                        && (s.localLogic == ELogisticStorage.None || s.localLogic == ELogisticStorage.Supply || s.localLogic == ELogisticStorage.Demand)
                        && (s.remoteLogic == ELogisticStorage.None || s.remoteLogic == ELogisticStorage.Supply || s.remoteLogic == ELogisticStorage.Demand)))) return false;
        }
        var observed = new List<SavedFactoryPort>();
        foreach (var id in ids)
            for (var slot = 0; slot < 16; slot++)
            {
                factory.ReadObjectConn(id, slot, out var output, out var other, out var otherSlot);
                if (other != 0) observed.Add(new SavedFactoryPort { ObjectId = id, Slot = slot, IsOutput = output, OtherObjectId = other, OtherSlot = otherSlot });
            }
        var topology = SavedFactoryTopologyPolicy.Validate(ids, edges, observed);
        return Require("saved_factory_site_paid_reciprocal_topology", topology is null);
    }
}
