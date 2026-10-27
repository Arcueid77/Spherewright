using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private string? RemoteResumeChargeEnvironmentBlocker(Player p, string? journeyId = null)
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
        return RetainedUnpoweredRecoveryMinerMatches(p, factory, journeyId)
            ? null : "remote_resume_charge_native_construction_not_empty";
    }

    private bool RetainedUnpoweredRecoveryMinerMatches(Player p, PlanetFactory factory, string? journeyId)
    {
        if (journeyId is null || !_sessions.IsCurrentSessionOwned
            || !ReferenceEquals(GameMain.data?.localLoadedPlanetFactory, factory)
            || !ReferenceEquals(GameMain.localPlanet?.factory, factory)
            || factory.planetId != p.planetId || factory.entityCount != 1
            || factory.entityPool is null || factory.entityCursor < 2 || factory.entityCursor > 131072
            || factory.entityCursor > factory.entityPool.Length || factory.prebuildCount != 0
            || factory.prebuildPool is null || factory.prebuildCursor < 2 || factory.prebuildCursor > 131072
            || factory.prebuildCursor > factory.prebuildPool.Length
            || !TryObserveOwnedConstructionDrones(factory, p.mecha.constructionModule, out var idle, out _) || !idle
            || !_blueprints.TryRead(out var builds)) return false;
        var local = builds.Where(b => b.Site.PlanetId == factory.planetId).ToArray();
        if (local.Length != 1) return false;
        var build = local[0];
        if (build.Phase != "completed" || build.LastEvidenceGameTick > GameMain.gameTick
            || build.Objects.Count != 1 || build.Site.Objects.Count != 1 || build.Site.Objects[0].ItemId != 2301
            || build.StructuredConstruction is not { Kind: "resource" } provenance || provenance.JourneyId != journeyId
            || build.ConstructionControl is not { Phase: "completed_idle", PhysicalIdleVerified: true,
                BindingLost: false, DispatchDisabled: false, StopReason: "construction_complete_draining" } control
            || !control.PhysicalIdleVerifiedAtGameTick.HasValue || !control.DisabledNativeFactoryPassGameTick.HasValue
            || control.NativeCreateActionId is null || control.NativeCreateActionId != build.ActionId
            || control.PreexistingEntityIds.Count != 0
            || p.mecha.constructionModule.droneEnabled != control.OriginalDroneEnabled) return false;
        var step = build.Objects[0];
        var id = step.EntityId.GetValueOrDefault();
        var retired = step.PrebuildId.GetValueOrDefault();
        if (step.State != BlueprintObjectStates.Completed || !step.CompletedAtGameTick.HasValue
            || !step.SubmittedAtGameTick.HasValue || step.InventoryBefore.GetValueOrDefault() < 1
            || step.InventoryAfter != step.InventoryBefore - 1
            || string.IsNullOrWhiteSpace(step.BeforeInventoryHash) || string.IsNullOrWhiteSpace(step.AfterInventoryHash)
            || retired < 1 || retired >= factory.prebuildCursor
            || !StructuredEntityMatches(factory, id, build, build.Site.Objects[0])
            || !StructuredConnectionsMatch(factory, build)) return false;
        // Retired cursors are not active work. Inspect every slot rather than
        // dropping a count/cursor condition or treating a missing slot as empty.
        for (var index = 1; index < factory.prebuildCursor; index++)
            if (factory.prebuildPool[index].id != 0) return false;
        for (var index = 1; index < factory.entityCursor; index++)
            if (index != id && factory.entityPool[index].id != 0) return false;
        ref var entity = ref factory.entityPool[id];
        var system = factory.factorySystem; var power = factory.powerSystem;
        if (system?.minerPool is null || entity.minerId < 1 || entity.minerId >= system.minerCursor
            || entity.minerId >= system.minerPool.Length || power?.consumerPool is null
            || entity.powerConId < 1 || entity.powerConId >= power.consumerCursor
            || entity.powerConId >= power.consumerPool.Length
            || power.netCursor != 1 || power.nodeCursor != 1 || power.genCursor != 1) return false;
        ref var miner = ref system.minerPool[entity.minerId];
        ref var consumer = ref power.consumerPool[entity.powerConId];
        return miner.id == entity.minerId && miner.entityId == id && miner.pcId == entity.powerConId
            && miner.productId == 0 && miner.productCount == 0
            && consumer.id == entity.powerConId && consumer.entityId == id && consumer.networkId == 0;
    }
}
