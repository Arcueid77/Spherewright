using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private string? RemoteResumeChargeEnvironmentBlocker(Player p, string? journeyId = null,
        NativeResumeDecisionEvidence? evidence = null)
    {
        var h = GameMain.history; var factory = p.factory; var module = p.mecha.constructionModule;
        if (p.controller.actionWalk.rtsVelocity.sqrMagnitude != 0f || p.controller.actionWalk.moveVelocity.sqrMagnitude != 0f
            || p.controller.actionDrift.rtsVelocity.sqrMagnitude != 0f || p.controller.actionDrift.moveVelocity.sqrMagnitude != 0f)
            return "remote_resume_charge_rts_or_manual_velocity_nonzero";
        if (h is null || h.currentTech != 0 || h.techQueue is null || h.techQueue.Length != 32 || h.techQueue.Any(id => id != 0))
            return "remote_resume_charge_global_research_active";
        if (factory is null
            || module.buildTargetTotalCount != 0 || module.repairTargetTotalCount != 0 || module.autoReconstructTargetTotalCount != 0
            || module.buildTargets is { } bt && (bt.Length > 256 || bt.Any(t => t.objectId != 0))
            || module.repairTargets is { } rt && (rt.Length > 256 || rt.Any(t => t.objectId != 0))
            || module.autoReconstructTargets is { } at && (at.Length > 256 || at.Any(t => t.objectId != 0)))
            return "remote_resume_charge_native_construction_not_empty";
        // Preserve the original empty-factory path. Only recovery callers pass
        // an exact journey; an ordinary landing cannot inherit this exception.
        if (factory.entityCount == 0 && factory.entityCursor <= 1
            && factory.prebuildCount == 0 && factory.prebuildCursor <= 1) return null;
        var retainedIls = RetainedUnpoweredRecoveryIls(p, factory, journeyId, evidence);
        return RetainedUnpoweredRecoveryMinerMatches(p, factory, journeyId, evidence, retainedIls)
            ? null : "remote_resume_charge_native_construction_not_empty";
    }

    private bool RetainedUnpoweredRecoveryMinerMatches(Player p, PlanetFactory factory, string? journeyId,
        NativeResumeDecisionEvidence? evidence, BlueprintBuildState? retainedIls = null)
    {
        bool Proved(string predicate, bool value) => evidence?.Require(predicate, value) ?? value;
        evidence?.Number("retained_miner_entity_count", factory.entityCount);
        evidence?.Number("retained_miner_entity_cursor", factory.entityCursor);
        evidence?.Number("retained_miner_prebuild_count", factory.prebuildCount);
        evidence?.Number("retained_miner_prebuild_cursor", factory.prebuildCursor);
        if (!Proved("retained_miner_owned_journey", journeyId is not null && _sessions.IsCurrentSessionOwned)
            || !Proved("retained_miner_loaded_factory_reference", ReferenceEquals(GameMain.data?.localLoadedPlanetFactory, factory))
            || !Proved("retained_miner_local_factory_reference", ReferenceEquals(GameMain.localPlanet?.factory, factory))) return false;
        var entities = factory.entityPool; var prebuilds = factory.prebuildPool;
        if (entities is null || prebuilds is null) return Proved("retained_miner_factory_pool_bounds", false);
        if (!Proved("retained_miner_factory_pool_bounds", !(factory.planetId != p.planetId || factory.entityCount != (retainedIls is null ? 1 : 2)
            || factory.entityCursor < 2 || factory.entityCursor > 131072
            || factory.entityCursor > entities.Length || factory.prebuildCount != 0
            || factory.prebuildCursor < 1 || factory.prebuildCursor > 131072
            || factory.prebuildCursor > prebuilds.Length))) return false;
        if (!Proved("retained_miner_native_drone_idle", TryObserveOwnedConstructionDrones(factory, p.mecha.constructionModule, out var idle, out _)
                && (idle || retainedIls is not null && SavedConstructionTailMatches(p, journeyId)))
            || !Proved("retained_miner_owned_progress_store", _blueprints.TryRead(out var builds))) return false;
        var local = builds.Where(b => b.Site.PlanetId == factory.planetId).ToArray();
        if (!Proved("retained_miner_single_local_receipt", local.Length == (retainedIls is null ? 1 : 2))) return false;
        var miners = local.Where(b => b.Site.Objects.Count == 1 && b.Site.Objects[0].ItemId == 2301).ToArray();
        if (miners.Length != 1) return false;
        var build = miners[0];
        if (build.Phase != "completed" || build.LastEvidenceGameTick > GameMain.gameTick
            || build.Objects.Count != 1 || build.Site.Objects.Count != 1 || build.Site.Objects[0].ItemId != 2301
            || build.StructuredConstruction is not { Kind: "resource" } provenance || provenance.JourneyId != journeyId
            || build.ConstructionControl is not { Phase: "completed_idle", PhysicalIdleVerified: true,
                BindingLost: false, OriginalDroneEnabled: true, StopReason: "construction_complete_draining" } control
            || !control.PhysicalIdleVerifiedAtGameTick.HasValue || !control.DisabledNativeFactoryPassGameTick.HasValue
            || control.NativeCreateActionId is null || control.NativeCreateActionId != build.ActionId
            || control.PreexistingEntityIds.Count != 0
            // DispatchDisabled is the retained stop/pass receipt, not a fresh
            // observation of the imported Native switch. Both sealed record
            // forms are valid; prove the current switch and idle independently.
            || p.mecha.constructionModule.droneEnabled != control.OriginalDroneEnabled
                && !(retainedIls is not null && !p.mecha.constructionModule.droneEnabled))
            return Proved("retained_miner_sealed_completion", false);
        Proved("retained_miner_sealed_completion", true);
        var step = build.Objects[0];
        var id = step.EntityId.GetValueOrDefault();
        var retired = step.PrebuildId.GetValueOrDefault();
        if (step.State != BlueprintObjectStates.Completed || !step.CompletedAtGameTick.HasValue
            || !step.SubmittedAtGameTick.HasValue || step.InventoryBefore.GetValueOrDefault() < 1
            || step.InventoryAfter != step.InventoryBefore - 1
            || string.IsNullOrWhiteSpace(step.BeforeInventoryHash) || string.IsNullOrWhiteSpace(step.AfterInventoryHash)
            || retired < 1 || retired >= prebuilds.Length
            || !StructuredEntityMatches(factory, id, build, build.Site.Objects[0])
            || !StructuredConnectionsMatch(factory, build)) return Proved("retained_miner_original_entity", false);
        Proved("retained_miner_original_entity", true);
        // Retired cursors are not active work. Inspect every slot rather than
        // dropping a count/cursor condition or treating a missing slot as empty.
        // Native RemovePrebuildWithComponents resets cursor to1 when the last
        // prebuild is removed. Its historical ID must name an empty allocated
        // slot, even when that slot is now beyond the reset live cursor.
        if (!Proved("retained_miner_retired_slot_empty", prebuilds[retired].id == 0)) return false;
        for (var index = 1; index < factory.prebuildCursor; index++)
            if (prebuilds[index].id != 0) return Proved("retained_miner_no_live_prebuilds", false);
        for (var index = 1; index < factory.entityCursor; index++)
            if (index != id && index != retainedIls?.Objects[0].EntityId && entities[index].id != 0)
                return Proved("retained_miner_no_other_entities", false);
        ref var entity = ref entities[id];
        var system = factory.factorySystem; var power = factory.powerSystem;
        if (system?.minerPool is null || entity.minerId < 1 || entity.minerId >= system.minerCursor
            || entity.minerId >= system.minerPool.Length || power?.consumerPool is null
            || entity.powerConId < 1 || entity.powerConId >= power.consumerCursor
            || entity.powerConId >= power.consumerPool.Length
            || power.netCursor != 1 || power.nodeCursor != 1 || power.genCursor != 1)
            return Proved("retained_miner_unpowered_component_binding", false);
        ref var miner = ref system.minerPool[entity.minerId];
        ref var consumer = ref power.consumerPool[entity.powerConId];
        return Proved("retained_miner_unpowered_empty_output", miner.id == entity.minerId && miner.entityId == id && miner.pcId == entity.powerConId
            && miner.productId == 0 && miner.productCount == 0
            && consumer.id == entity.powerConId && consumer.entityId == id && consumer.networkId == 0);
    }
}
