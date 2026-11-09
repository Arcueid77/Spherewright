using Spherewright.Bridge.Core.Safety;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Transport;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private sealed class NativeBeltPickupBinding
    {
        public PlanetFactory Factory = null!;
        public Player Player = null!;
        public StorageComponent Package = null!;
        public CargoPath Path = null!;
        public string Fingerprint = string.Empty;
        public NativeBeltPickupPlan Plan = null!;
        public string MemberSignature = string.Empty;
    }

    private sealed partial class NormalActionPlanPayload
    {
        public NativeBeltPickupBinding? BeltPickup { get; set; }
    }

    private NativeBeltPickupBinding CaptureNativeBeltPickup(int objectId)
    {
        var factory = GameMain.localPlanet?.factory;
        var player = GameMain.mainPlayer;
        if (factory is null || player?.package?.grids is null || !ReferenceEquals(player.factory, factory)
            || player.package.size < 1 || player.package.size > 512 || player.package.size > player.package.grids.Length
            || player.inhandItemId != 0 || player.inhandItemCount != 0 || player.inhandItemInc != 0
            || !NativePlayerStationaryForSetting(player) || player.mecha.forge.tasks.Count != 0
            || _flightCheckpoints.VoyagerJourney is { } journey && !IsVoyagerJourneyClosed(journey))
            throw new InvalidOperationException("belt_pickup_requires_closed_journey_and_stationary_empty_hand");
        var inspected = _reader.InspectFactoryEntityOnMainThread(_sessions.SessionId,
            new InspectFactoryEntityRequest { PlanetId = factory.planetId, ObjectId = objectId,
                MaterialInventoryObjectIds = new() { objectId } });
        var cut = inspected.Value?.MaterialInventoryCut;
        if (!inspected.Success || inspected.Value?.ComponentKind != "belt" || cut?.State != "observed"
            || cut.CargoPaths.Count != 1)
            throw new InvalidOperationException("belt_pickup_complete_path_unavailable");
        var observed = cut.CargoPaths[0];
        if (observed.PathClosed || observed.OutputPathId != 0 || observed.InputPathIds.Any(id => id != 0)
            || observed.PathLengthCells < 1 || observed.PathLengthCells > 4096
            || observed.BeltObjectIds.Count < 1 || observed.BeltObjectIds.Count > 64
            || observed.ItemCount < 1 || observed.ItemCount > 1000 || observed.Items.Count < 1
            || observed.Items.Any(item => item.ItemId <= 0 || item.Count <= 0 || item.Inc != 0))
            throw new InvalidOperationException("belt_pickup_requires_bounded_independent_zero_inc_path");
        var path = factory.cargoTraffic.GetCargoPath(observed.PathId);
        if (path?.buffer is null || path.pathLength != observed.PathLengthCells || path.buffer.Length < path.pathLength)
            throw new InvalidOperationException("belt_pickup_native_path_binding_unavailable");
        var fields = new List<object?> { factory.planetId, objectId, observed.PathId, observed.PathLengthCells,
            Convert.ToBase64String(path.buffer, 0, path.pathLength) };
        foreach (var id in observed.BeltObjectIds)
        {
            if (id <= 0 || id >= factory.entityCursor || id >= factory.entityPool.Length || factory.entityPool[id].id != id)
                throw new InvalidOperationException("belt_pickup_member_unavailable");
            var entity = factory.entityPool[id];
            if (entity.beltId <= 0 || entity.beltId >= factory.cargoTraffic.beltCursor
                || entity.beltId >= factory.cargoTraffic.beltPool.Length
                || factory.cargoTraffic.beltPool[entity.beltId].id != entity.beltId
                || factory.cargoTraffic.beltPool[entity.beltId].entityId != id
                || factory.cargoTraffic.beltPool[entity.beltId].segPathId != observed.PathId
                || Vector3.Distance(player.position, entity.pos) > player.mecha.buildArea)
                throw new InvalidOperationException("belt_pickup_member_identity_or_range_unavailable");
            fields.Add(id); fields.Add(entity.beltId); fields.Add(entity.modelIndex);
            fields.Add(factory.cargoTraffic.beltPool[entity.beltId].segIndex);
            fields.Add(factory.cargoTraffic.beltPool[entity.beltId].segLength);
        }
        var returns = observed.Items.Select(item => new BeltCargoItemSnapshot
            { ItemId = item.ItemId, Name = item.Name, Count = item.Count, Inc = item.Inc,
                CargoStackCount = item.CargoStackCount }).ToList();
        var inserters = new List<int>();
        if (factory.entityCursor > 131072) throw new InvalidOperationException("belt_pickup_factory_scan_bound");
        var members = new HashSet<int>(observed.BeltObjectIds);
        for (var id = 1; id < factory.entityCursor; id++)
        {
            var entity = factory.entityPool[id];
            if (entity.id != id || entity.inserterId <= 0 || entity.inserterId >= factory.factorySystem.inserterCursor
                || entity.inserterId >= factory.factorySystem.inserterPool.Length) continue;
            var native = factory.factorySystem.inserterPool[entity.inserterId];
            if (!members.Contains(native.pickTarget) && !members.Contains(native.insertTarget)) continue;
            if (native.id != entity.inserterId || native.entityId != id
                || !SorterDismantlePolicy.NativeCargoRecoverable(entity.protoId, native.grade, native.canStack,
                    native.bidirectional, native.stackInput, native.stackOutput,
                    native.itemId, native.itemCount, native.itemInc, native.stackCount)
                || native.itemInc != 0 || Vector3.Distance(player.position, entity.pos) > player.mecha.buildArea)
                throw new InvalidOperationException("belt_pickup_related_sorter_cargo_unproved");
            if (native.itemCount == 0) continue;
            if (inserters.Count == 64) throw new InvalidOperationException("belt_pickup_related_sorter_bound");
            inserters.Add(id);
            fields.Add(id); fields.Add(native.id); fields.Add(native.itemId); fields.Add(native.itemCount);
            fields.Add(native.pickTarget); fields.Add(native.insertTarget); fields.Add(native.filter);
            var item = returns.FirstOrDefault(row => row.ItemId == native.itemId);
            if (item is null) returns.Add(new() { ItemId = native.itemId, Count = native.itemCount,
                CargoStackCount = 1, Name = LDB.items.Select(native.itemId)?.name ?? string.Empty });
            else { item.Count += native.itemCount; item.CargoStackCount++; }
        }
        using var copy = new StorageCopy(player.package);
        foreach (var item in returns)
            if (copy.Value.AddItemStacked(item.ItemId, item.Count, 0, out var inc) != item.Count || inc != 0)
                throw new InvalidOperationException("belt_pickup_whole_package_capacity_unproved");
        fields.Add(PluginJson.Serialize(returns));
        var signature = NativeBeltPickupMemberSignature(factory, observed.BeltObjectIds.Concat(inserters));
        fields.Add(signature);
        return new() { Factory = factory, Player = player, Package = player.package, Path = path,
            MemberSignature = signature,
            Fingerprint = CanonicalStateHash.Combine("native-belt-pickup-v1", fields.ToArray()),
            Plan = new() { SelectedObjectId = objectId, PathId = observed.PathId,
                BeltObjectIds = observed.BeltObjectIds, InserterObjectIds = inserters, Items = returns } };
    }

    private static string NativeBeltPickupMemberSignature(PlanetFactory factory, IEnumerable<int> ids)
    {
        var fields = new List<object?>();
        foreach (var id in ids)
        {
            var entity = factory.entityPool[id];
            fields.Add(id); fields.Add(entity.protoId); fields.Add(entity.modelIndex);
            fields.Add(entity.pos.x); fields.Add(entity.pos.y); fields.Add(entity.pos.z);
            fields.Add(entity.rot.x); fields.Add(entity.rot.y); fields.Add(entity.rot.z); fields.Add(entity.rot.w);
            for (var slot = 0; slot < 16; slot++)
            {
                factory.ReadObjectConn(id, slot, out var output, out var other, out var otherSlot);
                fields.Add(output); fields.Add(other); fields.Add(otherSlot);
            }
            if (entity.inserterId <= 0) continue;
            var native = factory.factorySystem.inserterPool[entity.inserterId];
            fields.Add(native.id); fields.Add(native.filter); fields.Add(native.pickTarget); fields.Add(native.insertTarget);
        }
        return CanonicalStateHash.Combine("belt-pickup-member-state-v1", fields.ToArray());
    }

    private GameCallResult<PreparedNormalAction> PrepareNativeBeltPickup(string? sessionId, PrepareTransferRequest request)
    {
        var common = ValidatePrepareCommon(sessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null) return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        if (request.ItemId != 0 || request.Count != 0 || request.StorageEntityId <= 0)
            return InvalidPlan("Belt cargo pickup requires a positive belt object and itemId=0/count=0.");
        var player = _reader.GetPlayerStateOnMainThread(sessionId, new LocalPlanetRequest { PlanetId = request.PlanetId });
        var entity = _reader.InspectFactoryEntityOnMainThread(sessionId,
            new InspectFactoryEntityRequest { PlanetId = request.PlanetId, ObjectId = request.StorageEntityId });
        if (!player.Success || player.Value is null || !entity.Success || entity.Value is null)
            return NotReadyPlan("The exact local belt and player are unavailable.");
        if (player.Value.StateHash != request.ExpectedPlayerStateHash || entity.Value.StateHash != request.ExpectedStorageStateHash)
            return StalePlan("The exact pickup source or package changed.");
        NativeBeltPickupBinding binding;
        try { binding = CaptureNativeBeltPickup(request.StorageEntityId); }
        catch (InvalidOperationException error) { return InvalidPlan(error.Message); }
        var hash = CanonicalStateHash.PlayerAction(player.Value);
        var payload = NormalActionPlanPayload.Transfer(sessionId!, request.PlanetId,
            CanonicalStateHash.Combine("belt-pickup-plan-v1", hash, binding.Fingerprint), hash,
            entity.Value.StateHash, request.StorageEntityId, request.Direction, 0, 0);
        payload.BeltPickup = binding;
        var result = AddPreparedPlan(payload, common.Session!, 1,
            "Normal native belt pickup returns the disclosed complete independent path cargo to the package.");
        if (result.Success && result.Value is { } plan)
        {
            plan.BeltCargoPickup = binding.Plan;
            foreach (var item in binding.Plan.Items)
                plan.ItemBudget.Add(new() { ItemId = item.ItemId, Count = item.Count, Direction = request.Direction });
        }
        return result;
    }

    private BridgeError? RevalidateNativeBeltPickup(NormalActionPlanPayload plan)
    {
        var binding = plan.BeltPickup;
        var player = _reader.GetPlayerStateOnMainThread(plan.SessionId, new LocalPlanetRequest { PlanetId = plan.PlanetId });
        if (binding is null || !player.Success || player.Value is null
            || CanonicalStateHash.PlayerAction(player.Value) != plan.PlayerStateHash)
            return Stale("belt_pickup_original_package_binding_changed");
        try
        {
            var current = CaptureNativeBeltPickup(plan.TransferStorageEntityId);
            return ReferenceEquals(current.Factory, binding.Factory) && ReferenceEquals(current.Player, binding.Player)
                && ReferenceEquals(current.Package, binding.Package) && ReferenceEquals(current.Path, binding.Path)
                && current.Fingerprint == binding.Fingerprint ? null : Stale("belt_pickup_original_path_changed");
        }
        catch (InvalidOperationException error) { return Stale(error.Message); }
    }

    private void ExecuteNativeBeltPickup(ActionRecord action)
    {
        if (RevalidateNativeBeltPickup(action.Plan) is { } error) throw new InvalidOperationException(error.Message);
        var binding = action.Plan.BeltPickup!;
        var before = ProductionResetPackage(binding.Package);
        var expected = before.ToDictionary(row => row.Key, row => row.Value);
        foreach (var item in binding.Plan.Items)
        {
            expected.TryGetValue(item.ItemId, out var old);
            expected[item.ItemId] = (old.Count + item.Count, old.Inc);
        }
        foreach (var id in binding.Plan.BeltObjectIds)
            binding.Factory.cargoTraffic.PickupBeltItems(binding.Player, binding.Factory.entityPool[id].beltId, all: true);
        foreach (var id in binding.Plan.InserterObjectIds)
            binding.Factory.factorySystem.TakeBackItems_Inserter(binding.Player, binding.Factory.entityPool[id].inserterId);
        var after = ProductionResetPackage(binding.Package);
        var observed = _reader.InspectFactoryEntityOnMainThread(action.SessionId,
            new InspectFactoryEntityRequest { PlanetId = action.PlanetId, ObjectId = action.Plan.TransferStorageEntityId,
                MaterialInventoryObjectIds = new() { action.Plan.TransferStorageEntityId } });
        if (expected.Count != after.Count || expected.Any(row => !after.TryGetValue(row.Key, out var value) || value != row.Value)
            || observed.Value?.MaterialInventoryCut is not { State: "observed" } cut
            || cut.CargoPaths.Count != 1 || cut.CargoPaths[0].ItemCount != 0
            || binding.Plan.InserterObjectIds.Any(id => binding.Factory.factorySystem.inserterPool[
                binding.Factory.entityPool[id].inserterId].itemCount != 0)
            || NativeBeltPickupMemberSignature(binding.Factory,
                binding.Plan.BeltObjectIds.Concat(binding.Plan.InserterObjectIds)) != binding.MemberSignature
            || !cut.CargoPaths[0].BeltObjectIds.SequenceEqual(binding.Plan.BeltObjectIds))
            throw new InvalidOperationException("belt_pickup_same_tick_complete_conservation_unproved");
        action.TargetObjectId = action.Plan.TransferStorageEntityId;
        action.BeforeTargetAmount = binding.Plan.Items.Sum(item => item.Count); action.AfterTargetAmount = 0;
        action.AfterInventory = CaptureInventory(binding.Player);
        Complete(action, "Native belt pickup returned all disclosed path cargo with exact package count/inc conservation.");
    }
}
