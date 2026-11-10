using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private sealed class NativeBeltInsertionBinding
    {
        public PlanetFactory Factory = null!;
        public Player Player = null!;
        public CargoPath Path = null!;
        public StorageComponent Package = null!;
        public int BeltId;
        public string Signature = string.Empty;
        public NativeBeltInsertionPlan Plan = null!;
    }

    private sealed partial class NormalActionPlanPayload
    {
        public NativeBeltInsertionBinding? BeltInsertion { get; set; }
    }

    private NativeBeltInsertionBinding CaptureNativeBeltInsertion(int objectId, int itemId)
    {
        var factory = GameMain.localPlanet?.factory;
        var player = GameMain.mainPlayer;
        if (factory is null || player?.package?.grids is null || !ReferenceEquals(player.factory, factory)
            || player.package.size < 1 || player.package.size > 512 || player.package.size > player.package.grids.Length
            || player.inhandItemId != 0 || player.inhandItemCount != 0 || player.inhandItemInc != 0
            || player.mecha.forge.tasks.Count != 0 || LDB.items.Select(itemId) is null
            || _flightCheckpoints.VoyagerJourney is { } journey && !IsVoyagerJourneyClosed(journey))
            throw new InvalidOperationException("belt_insertion_requires_closed_journey_idle_forge_and_empty_hand");
        if (NativePlayerStationaryForSettingBlocker(player) is { } physicalBlocker)
            throw new InvalidOperationException(physicalBlocker);
        var stock = ProductionResetPackage(player.package);
        if (!stock.TryGetValue(itemId, out var item) || item.Count < 1 || item.Inc != 0)
            throw new InvalidOperationException("belt_insertion_requires_owned_zero_inc_item");
        var inspected = _reader.InspectFactoryEntityOnMainThread(_sessions.SessionId,
            new InspectFactoryEntityRequest { PlanetId = factory.planetId, ObjectId = objectId,
                MaterialInventoryObjectIds = new() { objectId } });
        var cut = inspected.Value?.MaterialInventoryCut;
        if (!inspected.Success || inspected.Value?.ComponentKind != "belt"
            || cut is not { State: "observed" } || cut.CargoPaths.Count != 1
            || !NativeBeltInsertionPolicy.IsEmptyIndependentPath(objectId, cut.CargoPaths[0]))
            throw new InvalidOperationException("belt_insertion_requires_complete_empty_independent_path");
        var observed = cut.CargoPaths[0];
        var path = factory.cargoTraffic.GetCargoPath(observed.PathId);
        if (path?.buffer is null || path.pathLength != observed.PathLengthCells || path.buffer.Length < path.pathLength)
            throw new InvalidOperationException("belt_insertion_native_path_binding_unavailable");
        foreach (var id in observed.BeltObjectIds)
        {
            if (id >= factory.entityCursor || id >= factory.entityPool.Length || factory.entityPool[id].id != id)
                throw new InvalidOperationException("belt_insertion_member_unavailable");
            var entity = factory.entityPool[id];
            if (entity.beltId <= 0 || entity.beltId >= factory.cargoTraffic.beltCursor
                || entity.beltId >= factory.cargoTraffic.beltPool.Length
                || factory.cargoTraffic.beltPool[entity.beltId].id != entity.beltId
                || factory.cargoTraffic.beltPool[entity.beltId].entityId != id
                || factory.cargoTraffic.beltPool[entity.beltId].segPathId != observed.PathId
                || Vector3.Distance(player.position, entity.pos) > player.mecha.buildArea)
                throw new InvalidOperationException("belt_insertion_member_identity_or_range_unavailable");
        }
        return new() { Factory = factory, Player = player, Package = player.package, Path = path,
            BeltId = factory.entityPool[objectId].beltId,
            Signature = NativeBeltPickupMemberSignature(factory, observed.BeltObjectIds),
            Plan = new() { SelectedObjectId = objectId, PathId = observed.PathId,
                PathLengthCells = observed.PathLengthCells, BeltObjectIds = observed.BeltObjectIds,
                ItemId = itemId, EmptyIndependentPath = true } };
    }

    private GameCallResult<PreparedNormalAction> PrepareNativeBeltInsertion(string? sessionId, PrepareTransferRequest request)
    {
        var common = ValidatePrepareCommon(sessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null) return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        if (request.Count != 1 || request.ItemId <= 0 || request.StorageEntityId <= 0)
            return InvalidPlan("Manual belt insertion requires one owned zero-inc item and one exact belt.");
        var player = _reader.GetPlayerStateOnMainThread(sessionId, new LocalPlanetRequest { PlanetId = request.PlanetId });
        var entity = _reader.InspectFactoryEntityOnMainThread(sessionId,
            new InspectFactoryEntityRequest { PlanetId = request.PlanetId, ObjectId = request.StorageEntityId });
        if (!player.Success || player.Value is null || !entity.Success || entity.Value is null)
            return NotReadyPlan("The exact local belt and player are unavailable.");
        if (player.Value.StateHash != request.ExpectedPlayerStateHash || entity.Value.StateHash != request.ExpectedStorageStateHash)
            return StalePlan("The exact belt or package changed after inspection.");
        NativeBeltInsertionBinding binding;
        try { binding = CaptureNativeBeltInsertion(request.StorageEntityId, request.ItemId); }
        catch (InvalidOperationException error) { return InvalidPlan(error.Message); }
        var playerHash = CanonicalStateHash.PlayerAction(player.Value);
        var hash = CanonicalStateHash.Combine("native-belt-insertion-v1", playerHash, binding.Signature,
            binding.Plan.PathId, binding.Plan.PathLengthCells, request.ItemId);
        var payload = NormalActionPlanPayload.Transfer(sessionId!, request.PlanetId, hash, playerHash,
            entity.Value.StateHash, request.StorageEntityId, request.Direction, request.ItemId, 1);
        payload.BeltInsertion = binding;
        var result = AddPreparedPlan(payload, common.Session!, 1,
            "Native PutItemOnBelt places one owned zero-inc item with same-tick package/path conservation.");
        if (result.Success && result.Value is { } plan)
        {
            plan.BeltCargoInsertion = binding.Plan;
            plan.DestinationObjectId = request.StorageEntityId;
            plan.ItemBudget.Add(new() { ItemId = request.ItemId, Count = 1, Direction = request.Direction });
        }
        return result;
    }

    private BridgeError? RevalidateNativeBeltInsertion(NormalActionPlanPayload plan)
    {
        var binding = plan.BeltInsertion;
        var player = _reader.GetPlayerStateOnMainThread(plan.SessionId, new LocalPlanetRequest { PlanetId = plan.PlanetId });
        if (binding is null || !player.Success || player.Value is null
            || CanonicalStateHash.PlayerAction(player.Value) != plan.PlayerStateHash)
            return Stale("belt_insertion_original_package_binding_changed");
        try
        {
            var current = CaptureNativeBeltInsertion(plan.TransferStorageEntityId, plan.TransferItemId);
            return ReferenceEquals(current.Factory, binding.Factory) && ReferenceEquals(current.Player, binding.Player)
                && ReferenceEquals(current.Package, binding.Package) && ReferenceEquals(current.Path, binding.Path)
                && current.BeltId == binding.BeltId && current.Signature == binding.Signature
                && current.Plan.PathId == binding.Plan.PathId && current.Plan.PathLengthCells == binding.Plan.PathLengthCells
                && current.Plan.BeltObjectIds.SequenceEqual(binding.Plan.BeltObjectIds)
                ? null : Stale("belt_insertion_original_path_changed");
        }
        catch (InvalidOperationException error) { return Stale(error.Message); }
    }

    private void ExecuteNativeBeltInsertion(ActionRecord action)
    {
        if (RevalidateNativeBeltInsertion(action.Plan) is { } error) throw new InvalidOperationException(error.Message);
        var binding = action.Plan.BeltInsertion!;
        var before = ProductionResetPackage(binding.Package);
        var itemId = action.Plan.TransferItemId;
        if (binding.Package.TakeItem(itemId, 1, out var inc) != 1 || inc != 0)
            throw new InvalidOperationException("belt_insertion_exact_package_debit_unproved");
        // The same single-item Native operation used by UIBeltWindow. No tick,
        // cargo-buffer write, item creation or implicit neighbouring belt.
        if (!binding.Factory.cargoTraffic.PutItemOnBelt(binding.BeltId, itemId, 0))
        {
            var refused = _reader.InspectFactoryEntityOnMainThread(action.SessionId,
                new InspectFactoryEntityRequest { PlanetId = action.PlanetId, ObjectId = action.Plan.TransferStorageEntityId,
                    MaterialInventoryObjectIds = new() { action.Plan.TransferStorageEntityId } });
            if (!refused.Success || refused.Value?.MaterialInventoryCut is not { State: "observed" } refusedCut
                || refusedCut.CargoPaths.Count != 1
                || !NativeBeltInsertionPolicy.IsEmptyIndependentPath(action.Plan.TransferStorageEntityId, refusedCut.CargoPaths[0])
                || refusedCut.CargoPaths[0].PathId != binding.Plan.PathId
                || refusedCut.CargoPaths[0].PathLengthCells != binding.Plan.PathLengthCells
                || !refusedCut.CargoPaths[0].BeltObjectIds.SequenceEqual(binding.Plan.BeltObjectIds)
                || NativeBeltPickupMemberSignature(binding.Factory, binding.Plan.BeltObjectIds) != binding.Signature)
                throw new InvalidOperationException("belt_insertion_refused_path_conservation_unproved");
            if (binding.Package.AddItemStacked(itemId, 1, 0, out var remainingInc) != 1 || remainingInc != 0
                || !SameBeltInsertionPackage(before, ProductionResetPackage(binding.Package)))
                throw new InvalidOperationException("belt_insertion_refused_return_conservation_unproved");
            Fail(action, "Native PutItemOnBelt refused insertion and the owned item was returned with exact package conservation.");
            return;
        }
        var expected = before.ToDictionary(row => row.Key, row => row.Value);
        var old = expected[itemId];
        if (old.Count == 1) expected.Remove(itemId);
        else expected[itemId] = (old.Count - 1, old.Inc);
        var observed = _reader.InspectFactoryEntityOnMainThread(action.SessionId,
            new InspectFactoryEntityRequest { PlanetId = action.PlanetId, ObjectId = action.Plan.TransferStorageEntityId,
                MaterialInventoryObjectIds = new() { action.Plan.TransferStorageEntityId } });
        var cut = observed.Value?.MaterialInventoryCut;
        var path = cut?.CargoPaths.SingleOrDefault();
        if (!observed.Success || cut is not { State: "observed" } || path is null
            || path.PathId != binding.Plan.PathId || path.PathLengthCells != binding.Plan.PathLengthCells
            || path.PathClosed || path.OutputPathId != 0 || path.InputPathIds.Any(id => id != 0)
            || path.ItemCount != 1 || path.CargoStackCount != 1 || path.Items.Count != 1
            || path.Items[0].ItemId != itemId || path.Items[0].Count != 1 || path.Items[0].Inc != 0
            || !path.BeltObjectIds.SequenceEqual(binding.Plan.BeltObjectIds)
            || NativeBeltPickupMemberSignature(binding.Factory, path.BeltObjectIds) != binding.Signature
            || !SameBeltInsertionPackage(expected, ProductionResetPackage(binding.Package)))
            throw new InvalidOperationException("belt_insertion_same_tick_package_path_conservation_unproved");
        action.TargetObjectId = action.Plan.TransferStorageEntityId;
        action.TargetItemId = itemId; action.BeforeTargetAmount = 0; action.AfterTargetAmount = 1;
        action.AfterInventory = CaptureInventory(binding.Player);
        Complete(action, "Native single-item placement conserved the exact owned package debit and complete-path count/inc.");
    }

    private static bool SameBeltInsertionPackage(Dictionary<int, (int Count, int Inc)> expected,
        Dictionary<int, (int Count, int Inc)> actual) => expected.Count == actual.Count
        && expected.All(row => actual.TryGetValue(row.Key, out var item) && item == row.Value);
}
