using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private SavedConstructionTail? _savedConstructionTail;

    // This certificate accepts only the already-built, unconfigured passive
    // tower next to the sealed unpowered miner. It never authorizes a create,
    // repair, buffer write, power generation or logistics operation on load.
    private BlueprintBuildState? RetainedUnpoweredRecoveryIls(Player p, PlanetFactory factory,
        string? journeyId, NativeResumeDecisionEvidence? evidence)
    {
        if (journeyId is null || factory.entityCount != 2 || !_sessions.IsCurrentSessionOwned
            || !ReferenceEquals(GameMain.data?.localLoadedPlanetFactory, factory)
            || !ReferenceEquals(GameMain.localPlanet?.factory, factory)
            || !_blueprints.TryRead(out var builds)) return null;
        var local = builds.Where(b => b.Site.PlanetId == factory.planetId).ToArray();
        if (local.Length != 2) return null;
        var towers = local.Where(b => b.Site.Objects.Count == 1 && b.Site.Objects[0].ItemId == 2104).ToArray();
        if (towers.Length != 1) return null;
        var build = CloneBlueprintState(towers[0]);
        if (!HasSavedPassiveTowerReceipt(build, journeyId, GameMain.gameTick) || p.mecha.constructionModule.droneEnabled
            || ReconcileStructuredConstructionState(build).Count != 0
            || build.Objects[0].State != BlueprintObjectStates.Completed) return null;
        var step = build.Objects[0]; var id = step.EntityId.GetValueOrDefault();
        var prebuild = step.PrebuildId.GetValueOrDefault();
        if (step.InventoryBefore.GetValueOrDefault() < 1 || step.InventoryAfter != step.InventoryBefore - 1
            || !step.SubmittedAtGameTick.HasValue || !step.CompletedAtGameTick.HasValue
            || string.IsNullOrEmpty(step.BeforeInventoryHash) || string.IsNullOrEmpty(step.AfterInventoryHash)
            || prebuild <= 0 || build.ConstructionControl!.PreexistingEntityIds.Contains(id)
            || factory.prebuildPool is null || prebuild >= factory.prebuildPool.Length
            || factory.prebuildPool[prebuild].id != 0 || !StructuredEntityMatches(factory, id, build, build.Site.Objects[0])
            || !StructuredConnectionsMatch(factory, build) || !ConstructionFactoryHasOnlyOwnedPending(factory, build)) return null;
        var entity = factory.entityPool[id]; var transport = factory.transport; var power = factory.powerSystem;
        if (entity.stationId <= 0 || transport?.stationPool is null || entity.stationId >= transport.stationCursor
            || entity.stationId >= transport.stationPool.Length || power?.consumerPool is null
            || entity.powerConId <= 0 || entity.powerConId >= power.consumerCursor || entity.powerConId >= power.consumerPool.Length)
            return null;
        var station = transport.stationPool[entity.stationId];
        if (station is null || station.energy != 0 || station.warperCount != 0 || station.idleShipCount != 0
            || station.workShipCount != 0 || station.idleDroneCount != 0 || station.workDroneCount != 0
            || station.storage is null || station.storage.Length != 5
            || station.storage.Any(s => s.itemId != 0 || s.count != 0 || s.inc != 0
                || s.localLogic != ELogisticStorage.None || s.remoteLogic != ELogisticStorage.None)
            || power.consumerPool[entity.powerConId].id != entity.powerConId
            || power.consumerPool[entity.powerConId].entityId != id || power.consumerPool[entity.powerConId].networkId != 0)
            return null;
        evidence?.Observe("retained_ils_exact_unpowered_empty_entity", true);
        return build;
    }

    private string? PrepareSavedConstructionTail(VoyagerRemoteResumeAdoption adoption, string journeyId)
    {
        if (_savedConstructionTail is { } retained)
            return ReferenceEquals(retained.Adoption, adoption) && SavedConstructionTailMatches(adoption.Player, journeyId)
                ? null : "remote_resume_saved_drone_tail_binding_changed";
        var p = adoption.Player; var f = p.factory;
        if (!TryObserveOwnedConstructionDrones(f, p.mecha.constructionModule, out var idle, out var ids))
            return "remote_resume_saved_drone_pool_unproved";
        if (idle) return null;
        if (!_sessions.ExactRemoteResumeLoadPreflight(adoption) || adoption.Authorization is null
            || adoption.Evidence.ReturnOnlyContinuation || adoption.Evidence.CompletedRemoteContinuation
            || RetainedUnpoweredRecoveryIls(p, f, journeyId, adoption.CurrentDecisionEvidence) is not { } build
            || !SavedConstructionTail.TryPower(p, out var power)) return "remote_resume_saved_drone_tail_source_unproved";
        var tail = new SavedConstructionTail(adoption, f, build, ids, power);
        _savedConstructionTail = tail;
        if (!SavedConstructionTailMatches(p, journeyId)) return "remote_resume_saved_drone_tail_motion_unproved";
        return null;
    }

    private bool SavedConstructionTailMatches(Player p, string? journeyId)
    {
        if (_savedConstructionTail is not { } t || journeyId != t.Build.StructuredConstruction!.JourneyId
            || !_sessions.IsCurrentSessionOwned || _sessions.SessionId != t.Adoption.SessionId
            || !ReferenceEquals(GameMain.data, t.Adoption.World) || !ReferenceEquals(p, t.Adoption.Player)
            || !ReferenceEquals(p.factory, t.Factory) || p.mecha.constructionModule.droneEnabled
            || !SavedConstructionTail.TryPower(p, out var power) || power != t.Power
            || GameMain.gameTick < t.Adoption.Evidence.SourceGameTick
            || GameMain.gameTick - t.Adoption.Evidence.SourceGameTick > NativeSavedConstructionTailPolicy.MaximumGameTicks
            || DateTimeOffset.UtcNow < t.StartedUtc || (DateTimeOffset.UtcNow - t.StartedUtc).TotalSeconds >= NativeSavedConstructionTailPolicy.MaximumWallSeconds
            || !TryObserveOwnedConstructionDrones(t.Factory, p.mecha.constructionModule, out _, out var ids)
            || ids.Any(id => !t.DroneIds.Contains(id)) || t.Factory.constructionSystem.drones.count != ids.Count) return false;
        var live = p.position.normalized * (p.position.magnitude + 2.8f);
        if (!t.AnchorHeld)
        {
            var displacement=p.position-t.SourceAnchor;var up=t.SourceAnchor.normalized;
            var inward=-Vector3.Dot(displacement,up);var tangent=(displacement+up*inward).magnitude;
            if (GameMain.gameTick-t.Adoption.Evidence.SourceGameTick>32 || float.IsNaN(inward) || float.IsInfinity(inward)
                || inward<-.01f || inward>.5f || !FiniteConstruction(tangent) || tangent>.05f) return false;
        }
        foreach (var id in ids)
        {
            var drone = t.Factory.constructionSystem.drones.buffer[id];
            if (drone.stage != 4 || new[] { drone.targetObjectId, drone.nextTarget1ObjectId, drone.nextTarget2ObjectId, drone.nextTarget3ObjectId }.Any(n => n != 0)) return false;
            var pos = t.Factory.craftPool[drone.craftId].pos;
            var motion = new NativeConstructionDroneMotionFacts { Stage = drone.stage, Progress = drone.progress,
                SpeedMetersPerSecond = GameMain.history.constructionDroneSpeed, Initial = ConstructionVector(drone.initialVector),
                Target = ConstructionVector(live), Eject = ConstructionVector(live),
                Position = new Spherewright.Contracts.Celestial.UniversalPositionSnapshot { X = pos.x, Y = pos.y, Z = pos.z } };
            if (!t.AnchorHeld)
            {
                var next=NativeConstructionDroneReservePolicy.PredictCopiedOrdinaryStep(motion);
                if (next is null || Vector3.Distance((Vector3)pos,t.Eject)>NativeSavedConstructionTailPolicy.MaximumDistanceMeters
                    || Vector3.Distance(new Vector3((float)next.X,(float)next.Y,(float)next.Z),t.Eject)>NativeSavedConstructionTailPolicy.MaximumDistanceMeters) return false;
            }
            else if (!NativeSavedConstructionTailPolicy.CopiedReturnStepProved(motion, t.Anchor.magnitude,
                Vector3.Distance(p.position, t.Anchor), Vector3.Distance(live, t.Eject), ConstructionVector(t.Eject))) return false;
        }
        return true;
    }

    private void ObserveSavedConstructionTailFactoryPass(GameData world)
    {
        if (_savedConstructionTail is not { } t || t.Sealed || GameMain.isPaused || !ReferenceEquals(world, t.Adoption.World)
            || world.factories is null || world.factoryCount < 1 || world.factoryCount > 512 || world.factoryCount > world.factories.Length
            || !world.factories.Take(world.factoryCount).Any(f => ReferenceEquals(f, t.Factory))
            || !SavedConstructionTailMatches(t.Adoption.Player, t.Build.StructuredConstruction!.JourneyId)) return;
        if (GameMain.gameTick > t.Adoption.Evidence.SourceGameTick) t.NativeFactoryPass = GameMain.gameTick;
        if (!t.AnchorHeld && t.LastPosePass!=GameMain.gameTick)
        {
            var p=t.Adoption.Player;
            t.StablePosePasses=t.LastPosePass.HasValue && Vector3.Distance(p.position,t.LastPose)<=.001f && ConstructionPlayerIdle(p)
                ? t.StablePosePasses+1:0;
            t.LastPose=p.position;t.LastPosePass=GameMain.gameTick;
            if (t.StablePosePasses>=3 && TryObserveOwnedConstructionDrones(t.Factory,p.mecha.constructionModule,out _,out var ids))
            {
                var eject=p.position.normalized*(p.position.magnitude+2.8f);var distance=0d;
                foreach(var id in ids)distance=Math.Max(distance,Vector3.Distance((Vector3)t.Factory.craftPool[t.Factory.constructionSystem.drones.buffer[id].craftId].pos,eject));
                var remaining=NativeSavedConstructionTailPolicy.MaximumGameTicks-(GameMain.gameTick-t.Adoption.Evidence.SourceGameTick);
                // Only the remaining stop-only budget is used. The original
                // build anchor/window/debit stay immutable; no work is issued.
                if (NativeSavedConstructionTailPolicy.StableReturnFitsRemaining(distance,GameMain.history.constructionDroneSpeed,remaining))
                {t.Anchor=p.position;t.Eject=eject;t.AnchorHeld=true;}
            }
        }
    }

    private string? SealSavedConstructionTail(VoyagerRemoteResumeAdoption adoption)
    {
        if (_savedConstructionTail is not { } t) return null;
        if (!ReferenceEquals(t.Adoption, adoption) || !SavedConstructionTailMatches(adoption.Player, t.Build.StructuredConstruction!.JourneyId))
            return "remote_resume_saved_drone_tail_binding_changed";
        if (t.Sealed) return null;
        if (!t.AnchorHeld) return VoyagerRecoveryWindowPolicy.ChargingPending;
        if (!TryObserveOwnedConstructionDrones(t.Factory, adoption.Player.mecha.constructionModule, out var idle, out _) || !idle
            || !ConstructionPlayerIdle(adoption.Player) || !t.NativeFactoryPass.HasValue) return VoyagerRecoveryWindowPolicy.ChargingPending;
        var b = t.Build; var c = b.ConstructionControl!;
        if (ReconcileStructuredConstructionState(b).Count != 0 || b.Objects.Any(o => o.State != BlueprintObjectStates.Completed))
            return "remote_resume_saved_drone_tail_entity_changed";
        c.RecordDisabledNativeFactoryPass(t.NativeFactoryPass.Value);
        if (!c.TryMarkPhysicalIdle(GameMain.gameTick, ConstructionOwnPrelaunchCacheClear(t.Factory, b), true))
            return "remote_resume_saved_drone_tail_physical_idle_unproved";
        // Keep original failure, lost binding, clocks, action and debit intact.
        // This is a later physical-state certificate, never an old action retry.
        c.Phase = "completed_idle"; b.LastEvidenceGameTick = GameMain.gameTick; b.Stop("completed"); b.Generation++;
        if (!_blueprints.TryPut(b)) return "remote_resume_saved_drone_tail_completion_persistence_failed";
        t.Sealed = true; return null;
    }

    private string? SavedConstructionTailFundingBlocker(VoyagerRemoteResumeAdoption adoption,
        Spherewright.Plugin.RuntimeDescriptor.VoyagerJourneyRecord journey, VoyagerRemoteIdleAdmissionFacts facts)
    {
        if (_savedConstructionTail is not { } t || !ReferenceEquals(t.Adoption, adoption)) return null;
        var f=facts.JourneyBudget; var perTick=facts.MovementReserve?.PerNativeTickEnergyJoules;
        var remaining=NativeSavedConstructionTailPolicy.MaximumGameTicks-(GameMain.gameTick-adoption.Evidence.SourceGameTick);
        if (f is null || !perTick.HasValue || !(perTick>0d) || !facts.CoreEnergyJoules.HasValue || remaining<0 || !f.LastLandedGameTick.HasValue
            || adoption.Authorization is not { } authorization || authorization.Proposal.MaximumChargeGameTicks<NativeSavedConstructionTailPolicy.MaximumGameTicks
            || (decimal)f.SpentGameTicks+f.CurrentGameTick-f.LastLandedGameTick.Value+f.ReturnReserveGameTicks+remaining+2>f.MaximumGameTicks
            || facts.CoreEnergyJoules<2d*perTick.Value
            || !f.AvailableLoadedEnergyJoules.HasValue || !f.LockedReturnFloorJoules.HasValue
            || f.AvailableLoadedEnergyJoules<f.LockedReturnFloorJoules+(remaining+2d)*perTick.Value
            || (f.JourneyDeadlineUtc-DateTimeOffset.UtcNow).TotalSeconds <= (f.ReturnReserveGameTicks+remaining+2d)/60d+1d)
            return "remote_resume_saved_drone_tail_remaining_reserve_unfunded";
        return null;
    }

    private sealed class SavedConstructionTail
    {
        internal readonly VoyagerRemoteResumeAdoption Adoption; internal readonly PlanetFactory Factory;
        internal readonly BlueprintBuildState Build; internal readonly HashSet<int> DroneIds;
        internal readonly Vector3 SourceAnchor; internal Vector3 Anchor,Eject,LastPose; internal readonly double Power;
        internal bool AnchorHeld;internal long? LastPosePass;internal int StablePosePasses;
        internal readonly DateTimeOffset StartedUtc = DateTimeOffset.UtcNow;
        internal long? NativeFactoryPass; internal bool Sealed;
        internal SavedConstructionTail(VoyagerRemoteResumeAdoption a, PlanetFactory f, BlueprintBuildState b, List<int> ids, double power)
        { Adoption=a; Factory=f; Build=b; DroneIds=new HashSet<int>(ids); Power=power; SourceAnchor=Anchor=a.Player.position; Eject=Anchor.normalized*(Anchor.magnitude+2.8f); }
        internal static bool TryPower(Player p, out double power)
        {
            var n=p.mecha.constructionModule.droneCount; var s=GameMain.history.constructionDroneSpeed;
            var rate=Configs.freeMode.droneEnergyPerMeter; var repair=Configs.freeMode.droneRepairHpPerTick*Configs.freeMode.droneRepairEnergyCostPerHP;
            power=n*(s*rate+repair*60d);
            return n>=1 && n<=32 && s>=10f && s<=12f && rate>=0d && repair>=0d
                && !double.IsNaN(power) && !double.IsInfinity(power) && power>=0d && power<=1e12d;
        }
    }
}
