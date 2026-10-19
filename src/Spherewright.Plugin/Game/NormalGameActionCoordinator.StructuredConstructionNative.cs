using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Celestial;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private static bool ConstructionPlayerIdle(Player player) => NativeWalkModeIsBounded(player, stationary: true)
        && player.movementState == EMovementState.Walk && player.speed <= .1f
        && player.currentOrder is null && player.orders?.orderCount == 0
        && !player.warpCommand && player.warpState == 0f && !player.navigation.navigating
        && BuildUiIsIdle(player) && VoyagerMechaWorkIdle(player)
        && player.controller.input0.sqrMagnitude == 0f && player.controller.input1.sqrMagnitude == 0f;

    private bool ConstructionReferencesMatch(ActionRecord action) => action.ConstructionBinding is { } b
        && _sessions.IsCurrentSessionOwned && _sessions.SessionId == action.SessionId
        && VoyagerLocalActionBudgetPolicy.MatchesOwnedJourneyIdentity(b.OwnedSaveName, b.GameVersion, true,
            _sessions.OwnedSaveName, _sessions.CaptureOnMainThread().SaveName, _sessions.CaptureOnMainThread().GameVersion)
        && ReferenceEquals(GameMain.data, b.World) && ReferenceEquals(GameMain.mainPlayer, b.Player)
        && ReferenceEquals(b.Player.controller, b.Controller) && ReferenceEquals(b.Player.mecha, b.Mecha)
        && ReferenceEquals(b.Mecha.constructionModule, b.Module)
        && ReferenceEquals(b.Factory.constructionSystem, b.System)
        && ReferenceEquals(GameMain.localPlanet?.factory, b.Factory)
        && ReferenceEquals(GameMain.data.localLoadedPlanetFactory, b.Factory)
        && b.Player.planetId == action.PlanetId && b.Factory.planetId == action.PlanetId && b.Module.id == 0;

    private bool ConstructionHoldOwned(ActionRecord action)
    {
        if (!ConstructionReferencesMatch(action)) return false;
        var player = action.ConstructionBinding!.Player;
        var order = player.currentOrder;
        return NativeWalkModeIsBounded(player, stationary: true, action.Plan.VoyagerLocalWalkScene) && player.speed <= .1f
            && Vector3.Distance(player.position, ToVector(action.BlueprintBuild!.ConstructionControl!.Anchor))
                <= NativeConstructionDroneReservePolicy.MaximumAnchorDisplacementMeters
            && player.orders.orderCount == 0 && (ReferenceEquals(order, action.PlayerOrder)
                || order is null && action.PlayerOrder?.targetReached == true)
            && player.controller.input0.sqrMagnitude == 0f && player.controller.input1.sqrMagnitude == 0f
            && !VFInput.rtsStop.onDown && !player.warpCommand && player.warpState == 0f
            && !player.navigation.navigating && BuildUiIsIdle(player) && VoyagerMechaWorkIdle(player);
    }

    private static NativeConstructionDroneReserve CaptureConstructionReserve(BlueprintBuildState build, Player player, out string costHash)
    {
        var module = player.mecha.constructionModule;
        var speed = GameMain.history.constructionDroneSpeed;
        var rate = Configs.freeMode.droneEnergyPerMeter;
        var eject = Configs.freeMode.droneEjectEnergy;
        var repair = Configs.freeMode.droneRepairHpPerTick * Configs.freeMode.droneRepairEnergyCostPerHP;
        var background = (Math.Max(0d, player.mecha.walkPower) * Math.Max(2d, player.mecha.walkSpeed)
            + Math.Max(0d, player.mecha.miningPower) + Math.Max(0d, player.mecha.replicatePower)
            + Math.Max(0d, player.mecha.researchPower)) * Math.Max(1d, player.mecha.reactorPowerConsRatio);
        var independent = CaptureNativeMechaBackgroundCosts(player);
        var anchor = build.ConstructionControl is not null ? ToVector(build.ConstructionControl.Anchor) : player.position;
        var ejectAnchor = anchor.normalized * (anchor.magnitude + 2.8f);
        var reach = 0d;
        foreach (var spec in build.Site.Objects)
        {
            var desc = LDB.items.Select(spec.ItemId)?.prefabDesc;
            if (desc is null || !FiniteConstruction(desc.selectSize.y)) { reach = double.NaN; break; }
            var position = ToVector(spec.Position);
            var target = position + position.normalized * (desc.selectSize.y + 1f);
            reach = Math.Max(reach, Vector3.Distance(ejectAnchor, target));
            // Both sorter poses must remain in the same finite local envelope.
            if (Vector3.Distance(anchor, ToVector(spec.Position2)) > 16d) { reach = double.NaN; break; }
        }
        costHash = CanonicalStateHash.Combine("native-structured-drone-cost-v1", module.droneCount,
            speed, rate, eject, repair, background, player.mecha.coreEnergyCap,
            independent.EnergyShieldCapacity, independent.EnergyShieldRechargeEnabled, independent.LaserActive,
            independent.AmmoItemId, independent.AmmoSelectSlot, independent.AmmoRateOfFire, independent.AmmoEnergyCost,
            independent.AmmoStorageEmptyAndUnfiltered,
            independent.GroundFleetCount, independent.SpaceFleetCount);
        return NativeConstructionDroneReservePolicy.Evaluate(new NativeConstructionDroneFacts
        {
            MaximumMechaDrones = module.droneCount, NativeSpeedMetersPerSecond = speed,
            NativeEnergyPerMeter = rate, NativeEjectEnergyJoules = eject, NativeRepairEnergyPerTick = repair,
            OtherMechaPowerUpperWatts = background, AnchorRadiusMeters = anchor.magnitude,
            MaximumTargetDistanceMeters = reach,
            NativePendingJumpTicks = player.controller.actionWalk.jumpDelayTick,
            IndependentMechaCosts = independent,
        });
    }

    private static bool TryObserveOwnedConstructionDrones(PlanetFactory factory, ConstructionModuleComponent module,
        out bool idle, out List<int> activeIds)
    {
        idle = false; activeIds = new List<int>();
        var pool = factory.constructionSystem?.drones;
        if (module.id != 0 || module.entityId != 0 || module.droneCount < 1 || module.droneCount > 32
            || module.droneIdleCount < 0 || module.droneIdleCount > module.droneCount
            || module.droneAliveCount != module.droneCount || pool?.buffer is null
            || pool.cursor < 1 || pool.cursor > 4096 || pool.cursor > pool.buffer.Length)
            return false;
        for (var index = 1; index < pool.cursor; index++)
        {
            var drone = pool.buffer[index];
            if (drone.id != index || drone.owner != 0 || drone.stage == 0) continue;
            if (drone.stage < 1 || drone.stage > 4 || drone.craftId <= 0
                || factory.craftPool is null || drone.craftId >= factory.craftCursor
                || drone.craftId >= factory.craftPool.Length || factory.craftPool[drone.craftId].id != drone.craftId)
                return false;
            activeIds.Add(index);
            if (activeIds.Count > module.droneCount) return false;
        }
        if (activeIds.Count != module.droneCount - module.droneIdleCount) return false;
        idle = module.allDronesIdle && activeIds.Count == 0;
        return true;
    }

    private static bool ConstructionFactoryHasOnlyOwnedPending(PlanetFactory factory, BlueprintBuildState build)
    {
        var system = factory.constructionSystem;
        if (system is null || system.constructionModules is null || system.constructStats is null
            || system.constructionModules.count != 0 || system.constructStats.count != 0
            || factory.prebuildCursor < 1 || factory.prebuildCursor > 131072
            || factory.prebuildPool is null || factory.prebuildCursor > factory.prebuildPool.Length)
            return false;
        var allowed = new HashSet<int>(build.Objects.Where(o => o.State == BlueprintObjectStates.PendingConstruction)
            .Select(o => o.PrebuildId.GetValueOrDefault()));
        var count = 0;
        for (var id = 1; id < factory.prebuildCursor; id++)
        {
            ref var p = ref factory.prebuildPool[id];
            if (p.id != id) continue;
            count++;
            if (!allowed.Contains(id) || p.isDestroyed) return false;
        }
        return count == factory.prebuildCount;
    }

    private static bool ConstructionOwnPrelaunchCacheClear(PlanetFactory factory, BlueprintBuildState build)
    {
        if (factory.prebuildPool is null) return false;
        foreach (var step in build.Objects.Where(o => o.State == BlueprintObjectStates.PendingConstruction))
        {
            var id = step.PrebuildId.GetValueOrDefault();
            if (id <= 0 || id >= factory.prebuildCursor || id >= factory.prebuildPool.Length
                || factory.prebuildPool[id].id != id || factory.prebuildPool[id].builderModuleId == -1) return false;
        }
        return true;
    }

    private bool ConstructionDronesStayInEnvelope(ActionRecord action)
    {
        var binding = action.ConstructionBinding!;
        var build = action.BlueprintBuild!;
        if (!TryObserveOwnedConstructionDrones(binding.Factory, binding.Module, out _, out var ids)) return false;
        var fixedAnchor = ToVector(build.ConstructionControl!.Anchor);
        var fixedEject = fixedAnchor.normalized * (fixedAnchor.magnitude + 2.8f);
        var liveEject = binding.Player.position.normalized * (binding.Player.position.magnitude + 2.8f);
        var allowed = new HashSet<int>(build.Objects.Select(o => -o.PrebuildId.GetValueOrDefault()).Where(id => id < 0));
        var speed = GameMain.history.constructionDroneSpeed;
        foreach (var id in ids)
        {
            var drone = binding.Factory.constructionSystem.drones.buffer[id];
            if (new[] { drone.targetObjectId, drone.nextTarget1ObjectId, drone.nextTarget2ObjectId, drone.nextTarget3ObjectId }
                .Any(target => target != 0 && !allowed.Contains(target))) return false;
            var nativePosition = binding.Factory.craftPool[drone.craftId].pos;
            var position = (Vector3)nativePosition;
            var target = drone.stage == 4 ? liveEject : drone.targetPos;
            var motion = new NativeConstructionDroneMotionFacts
            {
                Position = new UniversalPositionSnapshot { X = nativePosition.x, Y = nativePosition.y, Z = nativePosition.z }, Target = ConstructionVector(target),
                Initial = ConstructionVector(drone.initialVector), Eject = ConstructionVector(liveEject),
                Stage = drone.stage, Progress = drone.progress, SpeedMetersPerSecond = speed,
            };
            var next = NativeConstructionDroneReservePolicy.PredictCopiedOrdinaryStep(motion);
            if (next is null) return false;
            if (!NativeConstructionDroneReservePolicy.CopiedDroneStepPreservesEnvelope(drone.stage, speed,
                    Vector3.Distance(binding.Player.position, fixedAnchor), Vector3.Distance(position, fixedEject),
                    drone.stage == 4 ? 0d : Vector3.Distance(target, fixedEject),
                    Math.Sqrt(Math.Pow(next.X - fixedEject.x, 2d) + Math.Pow(next.Y - fixedEject.y, 2d) + Math.Pow(next.Z - fixedEject.z, 2d)),
                    NativeConstructionDroneReservePolicy.CopiedStepWouldNativeRecycle(motion)))
                return false;
        }
        return true;
    }

    private static List<int> CaptureConstructionExistingEntities(PlanetFactory factory, BlueprintBuildState build)
    {
        if (factory.entityCursor > 131072 || factory.entityPool is null || factory.entityCursor > factory.entityPool.Length)
            throw new InvalidOperationException("structured_entity_pool_bound");
        var itemId = build.Site.Objects[0].ItemId;
        var result = new List<int>();
        for (var id = 1; id < factory.entityCursor; id++)
        {
            var e = factory.entityPool[id];
            if (e.id == id && e.protoId == itemId && build.Site.Objects.Any(s => Vector3.Distance(e.pos, ToVector(s.Position)) <= .5f))
                result.Add(id);
            if (result.Count > 1024) throw new InvalidOperationException("structured_entity_attribution_bound");
        }
        return result;
    }

    private List<string> ReconcileStructuredConstructionState(BlueprintBuildState build)
    {
        var errors = new List<string>();
        var factory = GameMain.data?.localLoadedPlanetFactory;
        if (factory is null || factory.planetId != build.Site.PlanetId || GameMain.gameTick < build.LastEvidenceGameTick
            || factory.entityCursor > 131072 || factory.prebuildCursor > 131072 || factory.entityPool is null
            || factory.prebuildPool is null || factory.entityCursor > factory.entityPool.Length || factory.prebuildCursor > factory.prebuildPool.Length)
        { errors.Add("structured_world_precedes_evidence_or_factory_unavailable"); return errors; }
        foreach (var step in build.Objects)
        {
            var spec = build.Site.Objects[step.Index];
            if (step.State == BlueprintObjectStates.Submitting || step.State == BlueprintObjectStates.OutcomeUnknown)
            { errors.Add("structured_native_submission_unknown:" + step.Index); continue; }
            if (step.State == BlueprintObjectStates.Completed)
            {
                if (!StructuredEntityMatches(factory, step.EntityId.GetValueOrDefault(), build, spec)) errors.Add("structured_completed_identity_changed:" + step.Index);
            }
            else if (step.State == BlueprintObjectStates.PendingConstruction)
            {
                var id = step.PrebuildId.GetValueOrDefault();
                if (id > 0 && id < factory.prebuildCursor && factory.prebuildPool[id].id == id)
                {
                    if (!BlueprintPrebuildMatches(factory, id, spec)) errors.Add("structured_pending_identity_changed:" + step.Index);
                    continue;
                }
                var found = 0;
                for (var entityId = 1; entityId < factory.entityCursor; entityId++)
                {
                    if (build.ConstructionControl?.PreexistingEntityIds.Contains(entityId) == true
                        || build.Objects.Any(o => o.EntityId == entityId)) continue;
                    if (!StructuredEntityMatches(factory, entityId, build, spec)) continue;
                    if (found != 0) { found = -1; break; }
                    found = entityId;
                }
                if (found <= 0) errors.Add("structured_pending_entity_missing_or_ambiguous:" + step.Index);
                else build.ConfirmCompletion(step.Index, found, GameMain.gameTick);
            }
        }
        if (errors.Count == 0 && !StructuredConnectionsMatch(factory, build)) errors.Add("structured_exact_reciprocal_topology_unproved");
        return errors;
    }

    private static bool StructuredEntityMatches(PlanetFactory factory, int id, BlueprintBuildState build, BlueprintSiteObject spec)
    {
        if (id <= 0 || id >= factory.entityCursor || id >= factory.entityPool.Length) return false;
        ref var e = ref factory.entityPool[id];
        if (e.id != id || e.protoId != spec.ItemId || Vector3.Distance(e.pos, ToVector(spec.Position)) > .003f) return false;
        if (spec.ItemId == 2001) return BlueprintEntityMatches(factory, id, spec);
        if (Quaternion.Angle(e.rot, BlueprintRotation(spec.Rotation)) > .1f) return false;
        if (spec.ItemId == 2011)
        {
            var system = factory.factorySystem;
            if (system?.inserterPool is null || system.inserterPosePool is null || e.inserterId <= 0
                || e.inserterId >= system.inserterCursor || e.inserterId >= system.inserterPool.Length
                || e.inserterId >= system.inserterPosePool.Length || spec.Parameters.Length > 1) return false;
            ref var sorter = ref system.inserterPool[e.inserterId];
            ref var pose = ref system.inserterPosePool[e.inserterId];
            var stack = spec.Parameters.Length == 1 ? spec.Parameters[0] : 1;
            var desc = LDB.items.Select(2011)?.prefabDesc;
            return desc is not null && stack >= 1 && stack <= 4 && sorter.id == e.inserterId && sorter.entityId == id
                && sorter.stt == Math.Max(10000, desc.inserterSTT * stack) && sorter.filter == spec.FilterItemId
                && sorter.pickOffset == spec.InputOffset && sorter.insertOffset == spec.OutputOffset
                && IsSorterFilterApplied(id, spec.FilterItemId)
                && Vector3.Distance(pose.pos2, ToVector(spec.Position2)) <= .003f
                && Quaternion.Angle(pose.rot2, BlueprintRotation(spec.Rotation2)) <= .1f;
        }
        if (spec.ItemId == 2301)
        {
            if (e.minerId <= 0 || factory.factorySystem?.minerPool is null || e.minerId >= factory.factorySystem.minerCursor
                || e.minerId >= factory.factorySystem.minerPool.Length) return false;
            ref var miner = ref factory.factorySystem.minerPool[e.minerId];
            var expected = spec.Parameters.Where(n => n > 0).OrderBy(n => n).ToArray();
            return miner.id == e.minerId && miner.entityId == id && miner.veins is not null
                && miner.veinCount >= 1 && miner.veinCount <= miner.veins.Length
                && expected.Contains(build.StructuredConstruction!.ResourceNodeId)
                && miner.veins.Take(miner.veinCount).Where(n => n > 0).OrderBy(n => n).SequenceEqual(expected);
        }
        if (spec.ItemId == 2104)
        {
            if (e.stationId <= 0 || factory.transport?.stationPool is null || e.stationId >= factory.transport.stationCursor
                || e.stationId >= factory.transport.stationPool.Length) return false;
            var station = factory.transport.stationPool[e.stationId];
            return station is not null && station.id == e.stationId && station.entityId == id && station.isStellar
                && station.planetId == factory.planetId && station.gid > 0;
        }
        if (spec.ItemId == 2201 || spec.ItemId == 2203 || spec.ItemId == 2205)
        {
            if (e.powerNodeId <= 0 || factory.powerSystem?.nodePool is null || e.powerNodeId >= factory.powerSystem.nodeCursor
                || e.powerNodeId >= factory.powerSystem.nodePool.Length) return false;
            ref var node = ref factory.powerSystem.nodePool[e.powerNodeId];
            return node.id == e.powerNodeId && node.entityId == id;
        }
        return false;
    }

    private static bool StructuredConnectionsMatch(PlanetFactory factory, BlueprintBuildState build)
    {
        var binding = build.StructuredConstruction!;
        var ids = build.Objects.Select(o => o.State == BlueprintObjectStates.Completed ? o.EntityId.GetValueOrDefault()
            : o.State == BlueprintObjectStates.PendingConstruction ? -o.PrebuildId.GetValueOrDefault() : 0).ToArray();
        if (ids.Any(id => id == 0)) return build.Objects.All(o => o.State == BlueprintObjectStates.NotSubmitted);
        var edges = new List<StructuredObservedEdge>();
        for (var index = 0; index < ids.Length; index++)
        {
            var connection = binding.Steps[index];
            var input = connection.InputStepIndex >= 0 ? ids[connection.InputStepIndex] : connection.InputObjectId;
            var output = connection.OutputStepIndex >= 0 ? ids[connection.OutputStepIndex] : connection.OutputObjectId;
            if (input != 0) Add(input, connection.InputFromSlot, ids[index], connection.InputToSlot);
            if (output != 0) Add(ids[index], connection.OutputFromSlot, output, connection.OutputToSlot);
        }
        foreach (var endpoint in binding.Endpoints)
        {
            if (endpoint.ObjectId >= factory.entityCursor) return false;
            ref var entity = ref factory.entityPool[endpoint.ObjectId];
            if (entity.id != endpoint.ObjectId || entity.protoId != endpoint.ItemId
                || Vector3.Distance(entity.pos, ToVector(endpoint.Position)) > .003f
                || Quaternion.Angle(entity.rot, BlueprintRotation(endpoint.Rotation)) > .1f) return false;
        }
        foreach (var id in ids.Concat(binding.Endpoints.Select(e => e.ObjectId)))
        {
            var old = binding.Endpoints.SingleOrDefault(e => e.ObjectId == id)?.ExistingConnections;
            var expected = edges.Where(e => e.From == id || e.To == id).ToArray();
            var matched = new HashSet<StructuredObservedEdge>();
            var oldMatched = new HashSet<int>();
            for (var slot = 0; slot < 16; slot++)
            {
                factory.ReadObjectConn(id, slot, out var output, out var other, out var otherSlot);
                if (other == 0) continue;
                var retained = old?.SingleOrDefault(e => e.Slot == slot);
                if (retained is not null)
                {
                    if (retained.IsOutput != output || retained.OtherObjectId != other || retained.OtherSlot != otherSlot) return false;
                    oldMatched.Add(slot); continue;
                }
                var candidates = expected.Where(e => output
                    ? e.From == id && e.To == other && ConstructionSlotMatches(e.FromSlot, slot) && ConstructionSlotMatches(e.ToSlot, otherSlot)
                    : e.To == id && e.From == other && ConstructionSlotMatches(e.ToSlot, slot) && ConstructionSlotMatches(e.FromSlot, otherSlot)).ToArray();
                if (candidates.Length != 1 || !matched.Add(candidates[0])) return false;
                factory.ReadObjectConn(other, otherSlot, out var reverse, out var back, out var backSlot);
                if (reverse == output || back != id || backSlot != slot) return false;
            }
            if (matched.Count != expected.Length || oldMatched.Count != (old?.Count ?? 0)) return false;
        }
        foreach (var step in build.Objects.Where(o => o.State == BlueprintObjectStates.Completed && build.Site.Objects[o.Index].ItemId == 2011))
        {
            var c = binding.Steps[step.Index];
            ref var entity = ref factory.entityPool[step.EntityId!.Value];
            ref var sorter = ref factory.factorySystem.inserterPool[entity.inserterId];
            if (sorter.pickTarget != c.InputObjectId || sorter.insertTarget != c.OutputObjectId) return false;
        }
        return true;
        void Add(int from, int fromSlot, int to, int toSlot)
        {
            if (!edges.Any(e => e.From == from && e.To == to && e.FromSlot == fromSlot && e.ToSlot == toSlot))
                edges.Add(new StructuredObservedEdge { From = from, To = to, FromSlot = fromSlot, ToSlot = toSlot });
        }
    }

    private static bool ConstructionSlotMatches(int expected, int actual) => expected < 0 ? actual >= 0 && actual < 16 : expected == actual;
    private sealed class StructuredObservedEdge
    {
        public int From;
        public int FromSlot;
        public int To;
        public int ToSlot;
    }

    private ConstructionControlReadback? CaptureConstructionControl(BlueprintBuildState? build, ActionRecord? action = null)
    {
        if (build?.ConstructionControl is not { } control) return null;
        var exact = action is not null && ConstructionReferencesMatch(action);
        return new ConstructionControlReadback
        {
            Phase = control.Phase, DispatchDisabled = control.DispatchDisabled,
            PhysicalIdleVerified = control.PhysicalIdleVerified, ExactBindingVerified = exact,
            AnchorHeld = exact && ConstructionHoldOwned(action!), ReserveProofStillValid = action?.ConstructionReserveProofValid == true,
            ObservationLeaseExpiresAtUtc = control.ObservationLeaseExpiresAtUtc,
            TailGameTicks = control.TailGameTicks, TailAndNextTickEnergyJoules = control.TailAndNextTickEnergyJoules,
            StopReason = control.StopReason,
        };
    }
}
