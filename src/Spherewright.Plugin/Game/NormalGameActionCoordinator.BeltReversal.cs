using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Transport;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private sealed class BeltReversalBinding
    {
        internal PlanetFactory Factory = null!;
        internal Player Player = null!;
        internal CargoPath Path = null!;
        internal UIBeltWindow Window = null!;
        internal int BeltId;
        internal int PilerId;
        internal PilerComponent Piler;
        internal Dictionary<int, BeltComponent> Belts = new();
        internal Dictionary<int, InserterComponent> Sorters = new();
        internal Dictionary<int, EntityData> Entities = new();
        internal string PeerHash = string.Empty;
        internal string Fingerprint = string.Empty;
        internal BeltPathReversalPlan Plan = null!;
    }

    private BeltReversalBinding CaptureBeltReversal(int selectedId)
    {
        var factory = GameMain.localPlanet?.factory;
        var player = GameMain.mainPlayer;
        var window = UIRoot.instance?.uiGame?.beltWindow;
        if (factory is null || player?.package is null || !ReferenceEquals(player.factory, factory)
            || window is null || window.active || window.factory is not null || window.traffic is not null
            || window.player is not null || window.beltId != 0 || !BuildUiIsIdle(player)
            || player.inhandItemId != 0 || player.inhandItemCount != 0 || player.inhandItemInc != 0
            || player.mecha.forge.tasks.Count != 0
            || _flightCheckpoints.VoyagerJourney is { } journey && !IsVoyagerJourneyClosed(journey))
            throw new InvalidOperationException("belt_reversal_requires_idle_native_context_and_closed_journey");
        if (NativePlayerStationaryForSettingBlocker(player) is { } physical)
            throw new InvalidOperationException(physical);
        var inspected = _reader.InspectFactoryEntityOnMainThread(_sessions.SessionId,
            new InspectFactoryEntityRequest { PlanetId = factory.planetId, ObjectId = selectedId,
                MaterialInventoryObjectIds = new() { selectedId } });
        var cut = inspected.Value?.MaterialInventoryCut;
        if (!inspected.Success || inspected.Value?.ItemId != 2001 || cut is not { State: "observed" }
            || cut.CargoPaths.Count != 1 || !NativeBeltInsertionPolicy.IsEmptyIndependentPath(selectedId, cut.CargoPaths[0])
            || !EmptyStorageDismantlePolicy.BoundedPool(factory.entityCursor, factory.entityPool.Length)
            || !EmptyStorageDismantlePolicy.BoundedPool(factory.prebuildCursor, factory.prebuildPool.Length)
            || !EmptyStorageDismantlePolicy.BoundedPool(factory.factorySystem.inserterCursor, factory.factorySystem.inserterPool.Length)
            || !EmptyStorageDismantlePolicy.BoundedPool(factory.cargoTraffic.beltCursor, factory.cargoTraffic.beltPool.Length)
            || !EmptyStorageDismantlePolicy.BoundedPool(factory.cargoTraffic.pilerCursor, factory.cargoTraffic.pilerPool.Length))
            throw new InvalidOperationException("belt_reversal_requires_complete_empty_bounded_path");
        var observed = cut.CargoPaths[0];
        var traffic = factory.cargoTraffic;
        var path = traffic.GetCargoPath(observed.PathId);
        if (path?.belts is null || path.belts.Count != observed.BeltObjectIds.Count
            || path.pathLength != observed.PathLengthCells || !ReferenceEquals(path.cargoContainer, factory.cargoContainer))
            throw new InvalidOperationException("belt_reversal_native_path_binding_unavailable");
        var binding = new BeltReversalBinding { Factory = factory, Player = player, Path = path, Window = window };
        var members = new HashSet<int>(observed.BeltObjectIds);
        foreach (var id in members)
        {
            if (id >= factory.entityCursor || factory.entityPool[id].id != id)
                throw new InvalidOperationException("belt_reversal_member_identity_unavailable");
            var entity = factory.entityPool[id];
            if (entity.protoId != 2001 || entity.tilt != 0 || entity.beltId <= 0
                || entity.beltId >= traffic.beltCursor || entity.beltId >= traffic.beltPool.Length
                || !path.belts.Contains(entity.beltId) || Vector3.Distance(player.position, entity.pos) > player.mecha.buildArea)
                throw new InvalidOperationException("belt_reversal_member_type_or_range_unavailable");
            var belt = traffic.beltPool[entity.beltId];
            if (belt.id != entity.beltId || belt.entityId != id || belt.segPathId != path.id
                || belt.segIndex < 0 || belt.segLength <= 0 || (long)belt.segIndex + belt.segLength > path.pathLength)
                throw new InvalidOperationException("belt_reversal_component_binding_unavailable");
            binding.Belts.Add(id, belt); binding.Entities.Add(id, entity);
        }
        var ordered = binding.Belts.OrderBy(pair => pair.Value.segIndex).Select(pair => pair.Key).ToList();
        var pilers = new HashSet<int>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var id = ordered[i]; var belt = binding.Belts[id];
            var previous = i > 0 ? binding.Belts[ordered[i - 1]].id : 0;
            var next = i + 1 < ordered.Count ? binding.Belts[ordered[i + 1]].id : 0;
            if (!BeltPathReversalPolicy.MatchesLinearMember(belt.mainInputId, belt.outputId,
                    new[] { belt.backInputId, belt.leftInputId, belt.rightInputId }, previous, next))
                throw new InvalidOperationException("belt_reversal_requires_simple_directed_path");
            for (var slot = 0; slot < 16; slot++)
            {
                factory.ReadObjectConn(id, slot, out var output, out var other, out var otherSlot);
                if (other == 0) continue;
                if (other < 0 || other >= factory.entityCursor || factory.entityPool[other].id != other)
                    throw new InvalidOperationException("belt_reversal_pending_or_invalid_connection");
                factory.ReadObjectConn(other, otherSlot, out var reverseOutput, out var reverse, out var reverseSlot);
                if (reverse != id || reverseSlot != slot || reverseOutput == output)
                    throw new InvalidOperationException("belt_reversal_connection_not_reciprocal");
                if (members.Contains(other))
                {
                    var expected = slot == 0 && i + 1 < ordered.Count ? ordered[i + 1]
                        : slot == 1 && i > 0 ? ordered[i - 1] : 0;
                    if (other != expected || output != (slot == 0) || otherSlot != (slot == 0 ? 1 : 0))
                        throw new InvalidOperationException("belt_reversal_internal_connection_mismatch");
                    continue;
                }
                var external = factory.entityPool[other];
                if (slot is 0 or 1 && external.pilerId > 0 && external.protoId == 2040
                    && otherSlot is 0 or 1 && (slot == 0 ? i == ordered.Count - 1 : i == 0))
                    pilers.Add(other);
                else if (slot is >= 4 and <= 11 && external.inserterId > 0 && external.protoId is 2011 or 2012)
                {
                    if (external.inserterId >= factory.factorySystem.inserterCursor)
                        throw new InvalidOperationException("belt_reversal_sorter_binding_unavailable");
                    var sorter = factory.factorySystem.inserterPool[external.inserterId];
                    if (sorter.id != external.inserterId || sorter.entityId != other || sorter.itemCount != 0 || sorter.itemInc != 0
                        || sorter.bidirectional || sorter.canStack || Vector3.Distance(player.position, external.pos) > player.mecha.buildArea)
                        throw new InvalidOperationException("belt_reversal_related_sorter_not_idle");
                    binding.Sorters[other] = sorter;
                    binding.Entities[other] = external;
                }
                else throw new InvalidOperationException("belt_reversal_external_device_unsupported");
            }
        }
        if (pilers.Count != 1) throw new InvalidOperationException("belt_reversal_requires_one_exact_piler_endpoint");
        var pilerEntity = pilers.Single(); binding.PilerId = factory.entityPool[pilerEntity].pilerId;
        if (binding.PilerId >= traffic.pilerCursor || binding.PilerId >= traffic.pilerPool.Length)
            throw new InvalidOperationException("belt_reversal_piler_binding_unavailable");
        binding.Piler = traffic.pilerPool[binding.PilerId];
        if (binding.Piler.id != binding.PilerId || binding.Piler.entityId != pilerEntity
            || binding.Piler.cacheCargoStack1 != 0 || binding.Piler.cacheCargoStack2 != 0
            || binding.Piler.cacheCargoInc1 != 0 || binding.Piler.cacheCargoInc2 != 0
            || Vector3.Distance(player.position, factory.entityPool[pilerEntity].pos) > player.mecha.buildArea)
            throw new InvalidOperationException("belt_reversal_piler_cargo_or_range_unproved");
        var allowed = new HashSet<int>(binding.Sorters.Keys) { pilerEntity };
        for (var id = 1; id < factory.entityCursor; id++)
        {
            var entity = factory.entityPool[id];
            if (entity.id == 0 || members.Contains(id)) continue;
            if (entity.id != id) throw new InvalidOperationException("belt_reversal_external_identity_invalid");
            for (var slot = 0; slot < 16; slot++)
            {
                factory.ReadObjectConn(id, slot, out _, out var other, out _);
                if (members.Contains(other) && !allowed.Contains(id))
                    throw new InvalidOperationException("belt_reversal_foreign_reference");
            }
            if (entity.inserterId > 0)
            {
                if (entity.inserterId >= factory.factorySystem.inserterCursor)
                    throw new InvalidOperationException("belt_reversal_sorter_pool_identity_invalid");
                var sorter = factory.factorySystem.inserterPool[entity.inserterId];
                if ((members.Contains(sorter.pickTarget) || members.Contains(sorter.insertTarget)) && !binding.Sorters.ContainsKey(id))
                    throw new InvalidOperationException("belt_reversal_unbound_sorter_reference");
            }
        }
        for (var id = 1; id < factory.prebuildCursor; id++)
        {
            if (factory.prebuildPool[id].id == 0) continue;
            for (var slot = 0; slot < 16; slot++)
            {
                factory.ReadObjectConn(-id, slot, out _, out var other, out _);
                if (members.Contains(other)) throw new InvalidOperationException("belt_reversal_prebuild_reference");
            }
        }
        var stack = _reader.CaptureNativeStack(factory, pilerEntity);
        if (!stack.Complete) throw new InvalidOperationException("belt_reversal_complete_piler_stack_unavailable");
        binding.BeltId = factory.entityPool[selectedId].beltId;
        binding.PeerHash = CapturePilerReversalPeers(factory, pilerEntity);
        binding.Plan = new() { SelectedObjectId = selectedId, PathId = path.id, PathLengthCells = path.pathLength,
            BeltObjectIds = ordered, SorterObjectIds = binding.Sorters.Keys.OrderBy(id => id).ToList(),
            PilerObjectId = pilerEntity, PilerLayerObjectIds = stack.Layers.Select(layer => layer.ObjectId).ToList(), EmptyIndependentPath = true };
        var componentFields = new List<object?>();
        foreach (var id in ordered)
        {
            var belt = binding.Belts[id];
            componentFields.Add(belt.id); componentFields.Add(belt.segIndex); componentFields.Add(belt.segLength);
            componentFields.Add(belt.segPivotOffset); componentFields.Add(belt.speed);
            componentFields.Add(belt.mainInputId); componentFields.Add(belt.outputId);
        }
        foreach (var id in binding.Plan.SorterObjectIds)
        {
            var sorter = binding.Sorters[id];
            componentFields.Add(id); componentFields.Add(sorter.pickOffset); componentFields.Add(sorter.insertOffset);
        }
        binding.Fingerprint = CanonicalStateHash.Combine("native-piler-path-reversal-v1", PluginJson.Serialize(binding.Plan),
            NativeBeltPickupMemberSignature(factory, ordered.Concat(binding.Plan.SorterObjectIds)), binding.PeerHash,
            binding.Piler.inputBeltId, binding.Piler.outputBeltId, binding.Piler.pilerState,
            CanonicalStateHash.Combine("reversal-components", componentFields.ToArray()));
        return binding;
    }

    private string CapturePilerReversalPeers(PlanetFactory factory, int pilerId)
    {
        var stack = _reader.CaptureNativeStack(factory, pilerId);
        if (!stack.Complete) throw new InvalidOperationException("belt_reversal_piler_stack_unavailable");
        stack.CapturedAtGameTick = 0; stack.StateHash = string.Empty;
        foreach (var layer in stack.Layers)
        {
            layer.EndpointStateHash = string.Empty;
            if (layer.ObjectId != pilerId) continue;
            layer.Connections.RemoveAll(connection => connection.Slot is 0 or 1);
            layer.DeviceState!.PilerMode = null;
            layer.DeviceState.NativeInputBeltIds = null; layer.DeviceState.NativeOutputBeltIds = null;
        }
        return CanonicalStateHash.Combine("piler-reversal-peer-state-v1", PluginJson.Serialize(stack));
    }

    private GameCallResult<PreparedNormalAction> PrepareBeltReversal(string? sessionId, PrepareConfigureBuildingRequest request, SessionState session)
    {
        if (request.RecipeId != 0 || request.TechId != 0 || request.FilterItemId != 0
            || !string.IsNullOrEmpty(request.StorageOperation) || request.StorageBannedGridCount != -1)
            return InvalidPlan("Belt reversal requires default recipe, filter and storage-operation fields.");
        var selected = _reader.InspectFactoryEntityOnMainThread(sessionId,
            new InspectFactoryEntityRequest { PlanetId = request.PlanetId, ObjectId = request.EntityId });
        if (!selected.Success || selected.Value is null) return NotReadyPlan("The exact belt is unavailable.");
        if (selected.Value.StateHash != request.ExpectedFactoryStateHash) return StalePlan("The exact belt changed after inspection.");
        BeltReversalBinding binding;
        try { binding = CaptureBeltReversal(request.EntityId); }
        catch (InvalidOperationException error) { return InvalidPlan(error.Message); }
        var plan = NormalActionPlanPayload.CreateBeltReversal(sessionId!, request.PlanetId,
            request.EntityId, selected.Value.StateHash, binding);
        var result = AddPreparedPlan(plan, session, 1,
            "One Native reversal of the disclosed empty piler-connected path preserves all entities, package stock and other piler layers.");
        if (result.Value is { } value) { value.TargetObjectId = request.EntityId; value.BeltPathReversal = binding.Plan; }
        return result;
    }

    private BridgeError? RevalidateBeltReversal(NormalActionPlanPayload plan)
    {
        try
        {
            var before = plan.BeltReversal;
            var fresh = CaptureBeltReversal(plan.EntityId);
            return before is not null && ReferenceEquals(before.Factory, fresh.Factory)
                && ReferenceEquals(before.Player, fresh.Player) && ReferenceEquals(before.Path, fresh.Path)
                && ReferenceEquals(before.Window, fresh.Window) && before.Fingerprint == fresh.Fingerprint
                ? null : Stale("belt_reversal_original_binding_changed");
        }
        catch (InvalidOperationException error) { return Stale(error.Message); }
    }

    private void ExecuteBeltReversal(ActionRecord action)
    {
        if (RevalidateBeltReversal(action.Plan) is { } error) throw new InvalidOperationException(error.Message);
        var before = action.Plan.BeltReversal!; var window = before.Window;
        var package = CapturePlayerPackageState(before.Player);
        try
        {
            window.factory = before.Factory; window.traffic = before.Factory.cargoTraffic; window.player = before.Player;
            window.beltId = before.BeltId;
            window.OnReverseButtonClick(0);
        }
        finally
        {
            // OnBeltIdChange returns immediately for this closed native view.
            window.beltId = 0; window.factory = null; window.traffic = null; window.player = null;
        }
        var after = CaptureBeltReversal(action.Plan.EntityId);
        if (!after.Plan.BeltObjectIds.SequenceEqual(before.Plan.BeltObjectIds.AsEnumerable().Reverse())
            || after.Plan.PathLengthCells != before.Plan.PathLengthCells || after.PeerHash != before.PeerHash
            || CapturePlayerPackageState(before.Player) != package
            || !after.Plan.SorterObjectIds.SequenceEqual(before.Plan.SorterObjectIds)
            || !after.Plan.PilerLayerObjectIds.SequenceEqual(before.Plan.PilerLayerObjectIds))
            throw new InvalidOperationException("belt_reversal_complete_path_or_package_preservation_unproved");
        foreach (var pair in before.Belts)
        {
            var belt = after.Belts[pair.Key]; var old = pair.Value;
            var entity = before.Factory.entityPool[pair.Key]; var priorEntity = before.Entities[pair.Key];
            if (belt.id != old.id || belt.mainInputId != old.outputId || belt.outputId != old.mainInputId
                || belt.speed != old.speed || entity.protoId != priorEntity.protoId || entity.modelIndex != priorEntity.modelIndex
                || entity.pos != priorEntity.pos || entity.tilt != 0)
                throw new InvalidOperationException("belt_reversal_member_direction_or_identity_unproved");
        }
        foreach (var pair in before.Sorters)
        {
            var expected = pair.Value;
            if (before.Belts.ContainsKey(expected.pickTarget)) expected.pickOffset = (short)-expected.pickOffset;
            if (before.Belts.ContainsKey(expected.insertTarget)) expected.insertOffset = (short)-expected.insertOffset;
            if (!expected.Equals(after.Sorters[pair.Key])
                || !before.Entities[pair.Key].Equals(after.Factory.entityPool[pair.Key]))
                throw new InvalidOperationException("belt_reversal_sorter_configuration_or_cargo_changed");
        }
        var protectedPiler = after.Piler;
        after.Factory.ReadObjectConn(after.Plan.PilerObjectId, 0, out var firstOutput, out var firstEntity, out _);
        after.Factory.ReadObjectConn(after.Plan.PilerObjectId, 1, out var secondOutput, out var secondEntity, out _);
        var firstBelt = firstEntity > 0 ? after.Factory.entityPool[firstEntity].beltId : 0;
        var secondBelt = secondEntity > 0 ? after.Factory.entityPool[secondEntity].beltId : 0;
        var expectedMode = BeltPathReversalPolicy.PilerMode(firstBelt, secondBelt, firstOutput, secondOutput);
        if ((int)after.Piler.pilerState != expectedMode
            || expectedMode != 0 && (after.Piler.inputBeltId != (firstOutput ? secondBelt : firstBelt)
                || after.Piler.outputBeltId != (firstOutput ? firstBelt : secondBelt))
            || expectedMode == 0 && (after.Piler.inputBeltId != 0 && after.Piler.inputBeltId != firstBelt && after.Piler.inputBeltId != secondBelt
                || after.Piler.outputBeltId != 0 && after.Piler.outputBeltId != firstBelt && after.Piler.outputBeltId != secondBelt))
            throw new InvalidOperationException("belt_reversal_native_piler_direction_unproved");
        protectedPiler.inputBeltId = before.Piler.inputBeltId; protectedPiler.outputBeltId = before.Piler.outputBeltId;
        protectedPiler.pilerState = before.Piler.pilerState;
        if (!protectedPiler.Equals(before.Piler)) throw new InvalidOperationException("belt_reversal_piler_contents_changed");
        action.TargetObjectId = action.Plan.EntityId;
        action.BeltPathReversalReadback = new() { BeforeBeltObjectIds = before.Plan.BeltObjectIds,
            AfterBeltObjectIds = after.Plan.BeltObjectIds, PilerObjectId = before.Plan.PilerObjectId,
            BeforePilerMode = (int)before.Piler.pilerState, AfterPilerMode = (int)after.Piler.pilerState,
            CompletePackageCountIncPreserved = true, OtherPilerLayersPreserved = true };
        Complete(action, "Native path reversal preserved complete package stock, belt identity and other piler layers; inspect subsequent cargo flow separately.");
    }

    private sealed partial class NormalActionPlanPayload
    {
        internal BeltReversalBinding? BeltReversal { get; set; }

        internal static NormalActionPlanPayload CreateBeltReversal(string sessionId, int planetId, int entityId,
            string stateHash, BeltReversalBinding binding) => new()
        {
            ActionKind = NormalActionKinds.ConfigureBuilding, ConfigureMode = BuildingConfigurationModes.ReverseBeltPath,
            SessionId = sessionId, PlanetId = planetId, EntityId = entityId, FactoryStateHash = stateHash,
            ExpectedStateHash = binding.Fingerprint, BeltReversal = binding,
        };
    }
}
