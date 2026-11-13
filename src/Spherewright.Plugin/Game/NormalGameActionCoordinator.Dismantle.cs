using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameCallResult<PreparedNormalAction> PrepareDismantlePlanOnMainThread(
        string? requestedSessionId,
        PrepareDismantleRequest request)
    {
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        }

        if (request.ObjectId <= 0
            || string.IsNullOrWhiteSpace(request.ExpectedEndpointStateHash)
            || string.IsNullOrWhiteSpace(request.ExpectedPlayerStateHash))
        {
            return InvalidPlan("Dismantle requires one positive inspected entity ID plus exact endpoint and player state hashes.");
        }

        var playerResult = _reader.GetPlayerStateOnMainThread(
            requestedSessionId,
            new LocalPlanetRequest { PlanetId = request.PlanetId });
        if (!playerResult.Success || playerResult.Value is null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(playerResult.Error!);
        }

        if (!string.Equals(request.ExpectedPlayerStateHash, playerResult.Value.StateHash, StringComparison.Ordinal))
        {
            return StalePlan("Player position, package, hand, queue, or construction state changed after inspection.");
        }

        var targetResult = _reader.InspectFactoryEntityOnMainThread(
            requestedSessionId,
            new InspectFactoryEntityRequest { PlanetId = request.PlanetId, ObjectId = request.ObjectId });
        if (!targetResult.Success || targetResult.Value is null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(targetResult.Error!);
        }

        var target = targetResult.Value;
        if (request.PreviewOnly)
        {
            if (target.ItemId != WindTurbineDismantlePolicy.ItemId)
                return InvalidPlan("The read-only dismantle preview selects one wind turbine.");
            if (target.EndpointStateHash != request.ExpectedEndpointStateHash)
                return StalePlan("The selected wind turbine changed after inspection.");
            return PrepareWindDismantlePreview(target, playerResult.Value, request.ExpectedEndpointStateHash);
        }
        var targetError = ValidateDismantleTarget(playerResult.Value, target, request.ExpectedEndpointStateHash);
        if (targetError is not null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(targetError);
        }

        var playerActionHash = CanonicalStateHash.PlayerAction(playerResult.Value);
        var emptyStorageHash = target.ComponentKind == "storage"
            ? CaptureEmptyStorageDismantleBoundary(target, false).NativeHash : string.Empty;
        var emptyBeltHash = target.ComponentKind == "belt"
            ? CaptureEmptyBeltDismantleBoundary(target, false).NativeHash : string.Empty;
        var windBoundary = target.ItemId == WindTurbineDismantlePolicy.ItemId
            ? CaptureWindDismantleBoundary(target, false) : null;
        var expectedHash = CanonicalStateHash.Combine(
            NormalActionKinds.Dismantle,
            _sessions.SessionId,
            request.PlanetId,
            playerActionHash,
            target.EndpointStateHash,
            target.ObjectId,
            target.ItemId);
        if (emptyStorageHash.Length != 0)
            expectedHash = CanonicalStateHash.Combine(expectedHash, emptyStorageHash);
        if (emptyBeltHash.Length != 0)
            expectedHash = CanonicalStateHash.Combine(expectedHash, emptyBeltHash);
        if (windBoundary is not null)
            expectedHash = CanonicalStateHash.Combine(expectedHash, windBoundary.NativeHash);
        var payload = NormalActionPlanPayload.Dismantle(
            _sessions.SessionId!,
            request.PlanetId,
            expectedHash,
            playerActionHash,
            target.EndpointStateHash,
            target.ObjectId,
            target.ItemId, emptyStorageHash, emptyBeltHash);
        payload.WindPowerStateHash = windBoundary?.NativeHash ?? string.Empty;
        var prepared = AddPreparedPlan(
            payload,
            common.Session!,
            1,
            "DSP's normal PlayerAction_Build.DoDismantleObject removes the exact supported miner, basic sorter, isolated empty default2101 storage, isolated empty2001 chain head (at most16 belts/512 cells), or one2203 wind turbine with bound power-topology verification. Readback proves disappearance and recovery; empty storage/belt additionally preserve surviving entities/prebuilds/connections, with touched belt rotations proven from exact native geometry. No automatic rebuild or slot write.");
        if (prepared.Success && prepared.Value is not null)
        {
            prepared.Value.TargetObjectId = target.ObjectId;
            if (windBoundary is not null)
                prepared.Value.WindTurbineDismantle = CreateWindDismantlePreview(target, playerResult.Value, windBoundary, false);
            prepared.Value.ItemBudget.Add(new ActionItemBudget
            {
                ItemId = target.ItemId,
                Name = target.Name,
                Count = 1,
                Direction = "dismantle-recovery",
            });
        }

        return prepared;
    }

    private BridgeError? RevalidateDismantlePlanOnMainThread(NormalActionPlanPayload plan)
    {
        var playerResult = _reader.GetPlayerStateOnMainThread(
            plan.SessionId,
            new LocalPlanetRequest { PlanetId = plan.PlanetId });
        if (!playerResult.Success || playerResult.Value is null)
        {
            return playerResult.Error;
        }

        if (!string.Equals(CanonicalStateHash.PlayerAction(playerResult.Value), plan.PlayerStateHash, StringComparison.Ordinal))
        {
            return Stale("Player position, package, hand, queue, or construction state changed after dismantle preparation.");
        }

        var targetResult = _reader.InspectFactoryEntityOnMainThread(
            plan.SessionId,
            new InspectFactoryEntityRequest { PlanetId = plan.PlanetId, ObjectId = plan.EntityId });
        if (!targetResult.Success || targetResult.Value is null)
        {
            return targetResult.Error;
        }

        if (targetResult.Value.ItemId != plan.BuildingItemId)
        {
            return Stale("The dismantle target item identity changed after preparation.");
        }

        var error = ValidateDismantleTarget(playerResult.Value, targetResult.Value, plan.FactoryStateHash);
        if (error is not null) return error;
        if (targetResult.Value.ComponentKind == "storage"
            && CaptureEmptyStorageDismantleBoundary(targetResult.Value, false).NativeHash != plan.StorageNativeStateHash)
            return Stale("The empty storage native identity or contents changed after dismantle preparation.");
        if (targetResult.Value.ComponentKind == "belt"
            && CaptureEmptyBeltDismantleBoundary(targetResult.Value, false).NativeHash != plan.EmptyBeltNativeStateHash)
            return Stale("The empty belt chain native identity, geometry, topology or cargo changed after dismantle preparation.");
        if (targetResult.Value.ItemId == WindTurbineDismantlePolicy.ItemId
            && CaptureWindDismantleBoundary(targetResult.Value, false).NativeHash != plan.WindPowerStateHash)
            return Stale("The wind generator or complete power topology changed after preparation.");
        return null;
    }

    private BridgeError? ValidateDismantleTarget(
        PlayerStateSnapshot player,
        FactoryEntitySnapshot target,
        string expectedEndpointStateHash)
    {
        if (target.ObjectKind != FactoryObjectKinds.Entity
            || target.ObjectId <= 0
            || target.ItemId <= 0
            || !(target.ComponentKind == "miner" || target.ComponentKind == "inserter" && SorterDismantlePolicy.Supports(target.ItemId)
                || EmptyStorageDismantlePolicy.SupportsSnapshot(target)
                || EmptyBeltDismantlePolicy.SupportsSnapshot(target)
                || WindTurbineDismantlePolicy.SupportsSnapshot(target)))
        {
            return BridgeError.Create(
                BridgeErrorCodes.InvalidRequest,
                "Dismantle accepts a resource miner, ordinary2011/2012 sorter, isolated empty default2101 storage, isolated empty2001 chain head, or one2203 wind turbine.",
                false,
                "Inspect one supported positive entity and prepare again; other types are not implicitly authorized.");
        }

        var item = LDB.items.Select(target.ItemId);
        if (item?.prefabDesc is null
            || (target.ComponentKind == "miner" && !item.prefabDesc.veinMiner
                && !item.prefabDesc.oilMiner
                && item.prefabDesc.minerType != EMinerType.Vein
                && item.prefabDesc.minerType != EMinerType.Oil)
            || (target.ComponentKind == "inserter" && !item.prefabDesc.isInserter)
            || (target.ComponentKind == "storage" && !item.prefabDesc.isStorage)
            || (target.ComponentKind == "belt" && !item.prefabDesc.isBelt)
            || (target.ItemId == WindTurbineDismantlePolicy.ItemId && !item.prefabDesc.windForcedPower))
        {
            return BridgeError.Create(
                BridgeErrorCodes.InvalidRequest,
                "The inspected entity is not a supported current-version resource miner, basic sorter, empty storage or empty basic belt head.",
                false,
                "Use one explicitly supported entity; no general building removal.");
        }

        if (GameMain.sandboxToolsEnabled && GameMain.preferences.instantDismantle)
            return BridgeError.Create(BridgeErrorCodes.InvalidRequest,
                "Native instant-dismantle would discard cargo; this path requires ordinary recovery.", false,
                "Disable the native instant-dismantle option manually before preparing; Spherewright never toggles or invokes it.");

        if (!string.Equals(target.EndpointStateHash, expectedEndpointStateHash, StringComparison.Ordinal))
        {
            return Stale("Dismantle target identity, pose, or connections changed after inspection.");
        }

        if (!player.IsAlive || !player.IsOnPlanet || player.MovementState != "Walk" || player.Speed > 0.1f
            || !BuildUiIsIdle(GameMain.mainPlayer))
        {
            return BridgeError.Create(
                BridgeErrorCodes.PlayerBusy,
                "Dismantle requires a living, settled player on the local planet.",
                true,
                "Wait for Walk state at no more than 0.1 m/s, inspect again, and prepare a new plan.");
        }

        var distance = Vector3.Distance(ToVector(player.Position), ToVector(target.Position));
        if (distance > player.BuildArea + 0.1f)
        {
            return BridgeError.Create(
                BridgeErrorCodes.TargetOutOfRange,
                $"The dismantle target is {distance:F2} metres away, outside the player's {player.BuildArea:F2}-metre build area.",
                true,
                "Move into normal construction range and prepare again.");
        }

        if (target.ComponentKind == "inserter")
        {
            try { CaptureSorterDismantleNeighbors(target); CaptureDismantleInventoryInc(GameMain.mainPlayer); }
            catch (InvalidOperationException error)
            {
                return BridgeError.Create(BridgeErrorCodes.BuildConnectionInvalid, error.Message, false,
                    "Fresh inspect the exact sorter and endpoints; no malformed present connection or unrecoverable cargo may be removed.");
            }
        }

        if (target.ComponentKind == "storage")
        {
            try { CaptureEmptyStorageDismantleBoundary(target, false); CaptureDismantleInventoryInc(GameMain.mainPlayer); }
            catch (InvalidOperationException error)
            {
                return BridgeError.Create(BridgeErrorCodes.BuildConnectionInvalid, error.Message, false,
                    "Only an empty default2101 with no layers, add-on, entity/prebuild links or cached references is recoverable. Never force removal or automatically rebuild.");
            }
        }

        if (target.ComponentKind == "belt")
        {
            try { CaptureEmptyBeltDismantleBoundary(target, false); CaptureDismantleInventoryInc(GameMain.mainPlayer); }
            catch (InvalidOperationException error)
            {
                return BridgeError.Create(BridgeErrorCodes.BuildConnectionInvalid, error.Message, false,
                    "Only a complete empty isolated2001 chain head (at most16 belts/512 cells) is recoverable. Disconnect external endpoints normally first; never force removal.");
            }
        }

        if (target.ItemId == WindTurbineDismantlePolicy.ItemId)
        {
            try { CaptureWindDismantleBoundary(target, false); CaptureDismantleInventoryInc(GameMain.mainPlayer); }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException)
            {
                return BridgeError.Create(BridgeErrorCodes.BuildConnectionInvalid, error.Message, false,
                    "Inspect the exact wind turbine and complete power topology, then prepare a fresh removal.");
            }
        }

        var requiredSlots = CountConservativeRecoverySlots(target);
        var freeSlots = player.InventorySlotCount - player.InventoryOccupiedSlotCount;
        if (freeSlots < requiredSlots)
        {
            return BridgeError.Create(
                BridgeErrorCodes.InventoryFull,
                $"Normal dismantle recovery may require {requiredSlots} empty package slots, but only {freeSlots} are free.",
                true,
                "Free package slots, inspect the player and target again, then prepare a new dismantle plan.");
        }

        return null;
    }

    private static int CountConservativeRecoverySlots(FactoryEntitySnapshot target)
    {
        var recovery = CaptureExpectedDismantleRecovery(target);
        var slots = 0;
        foreach (var pair in recovery)
        {
            var stackSize = Math.Max(1, LDB.items.Select(pair.Key)?.StackSize ?? 1);
            slots += (pair.Value + stackSize - 1) / stackSize;
        }

        return slots;
    }

    private static Dictionary<int, int> CaptureExpectedDismantleRecovery(FactoryEntitySnapshot target)
    {
        var recovery = new Dictionary<int, int> { [target.ItemId] = 1 };
        foreach (var buffer in target.Buffers.Where(buffer => buffer.ItemId > 0 && buffer.Count > 0))
        {
            recovery[buffer.ItemId] = GetCount(recovery, buffer.ItemId) + buffer.Count;
        }

        return recovery;
    }

    private void ExecuteDismantleOnMainThread(ActionRecord action)
    {
        var player = GameMain.mainPlayer
            ?? throw new InvalidOperationException("The player is unavailable during dismantle.");
        var targetResult = _reader.InspectFactoryEntityOnMainThread(
            action.SessionId,
            new InspectFactoryEntityRequest { PlanetId = action.PlanetId, ObjectId = action.Plan.EntityId });
        if (!targetResult.Success || targetResult.Value is null)
        {
            throw new InvalidOperationException("The exact dismantle target disappeared before execution.");
        }

        var expectedRecovery = CaptureExpectedDismantleRecovery(targetResult.Value);
        var sorter = targetResult.Value.ComponentKind == "inserter";
        var emptyStorage = targetResult.Value.ComponentKind == "storage";
        var emptyBelt = targetResult.Value.ComponentKind == "belt";
        var wind = targetResult.Value.ItemId == WindTurbineDismantlePolicy.ItemId;
        EmptyStorageDismantleBoundary? emptyBoundary = null;
        EmptyBeltDismantleBoundary? beltBoundary = null;
        WindDismantleBoundary? windBoundary = null;
        if (emptyStorage)
        {
            if (RevalidateDismantlePlanOnMainThread(action.Plan) is not null)
                throw new InvalidOperationException("Empty storage lost its prepared recovery proof before execution.");
            emptyBoundary = CaptureEmptyStorageDismantleBoundary(targetResult.Value, true);
        }
        if (emptyBelt)
        {
            if (RevalidateDismantlePlanOnMainThread(action.Plan) is not null)
                throw new InvalidOperationException("Empty belt lost its prepared recovery proof before execution.");
            beltBoundary = CaptureEmptyBeltDismantleBoundary(targetResult.Value, true);
        }
        if (wind)
        {
            if (RevalidateDismantlePlanOnMainThread(action.Plan) is not null)
                throw new InvalidOperationException("Wind removal lost its prepared power topology proof.");
            windBoundary = CaptureWindDismantleBoundary(targetResult.Value, true);
        }
        var survivors = sorter ? CaptureSorterDismantleNeighbors(targetResult.Value) : new List<FactoryEntitySnapshot>();
        var beforeInc = sorter || emptyStorage || emptyBelt || wind ? CaptureDismantleInventoryInc(player) : null;
        var cargo = targetResult.Value.Buffers.SingleOrDefault(b => b.Role == "inserter-held");
        foreach (var survivor in survivors)
            survivor.Connections.RemoveAll(e => e.OtherObjectId == action.Plan.EntityId);
        if (!player.controller.actionBuild.DoDismantleObject(action.Plan.EntityId))
        {
            throw new InvalidOperationException("DSP's normal dismantle path rejected the exact entity.");
        }

        if (sorter)
        {
            foreach (var survivor in survivors)
            {
                var fresh = _reader.InspectFactoryEntityOnMainThread(action.SessionId,
                    new InspectFactoryEntityRequest { PlanetId = action.PlanetId, ObjectId = survivor.ObjectId });
                if (!fresh.Success || fresh.Value is null || !SorterDismantlePolicy.SurvivorMatches(survivor, fresh.Value))
                    throw new InvalidOperationException("Sorter removal changed an unrelated surviving pose/configuration/connection.");
            }
            if (!SorterDismantlePolicy.ProvesIncRecovery(beforeInc!, CaptureDismantleInventoryInc(player),
                cargo?.ItemId ?? 0, cargo?.Inc ?? 0))
                throw new InvalidOperationException("Native sorter removal did not preserve the exact cargo proliferation points.");
        }

        if (emptyBoundary is not null)
        {
            VerifyEmptyStorageDismantleBoundary(action.Plan.EntityId, emptyBoundary);
            if (!SorterDismantlePolicy.ProvesIncRecovery(beforeInc!, CaptureDismantleInventoryInc(player), 0, 0))
                throw new InvalidOperationException("Empty storage recovery changed inventory proliferation points.");
        }

        if (beltBoundary is not null)
        {
            VerifyEmptyBeltDismantleBoundary(action.Plan.EntityId, beltBoundary);
            if (!SorterDismantlePolicy.ProvesIncRecovery(beforeInc!, CaptureDismantleInventoryInc(player), 0, 0))
                throw new InvalidOperationException("Empty belt recovery changed inventory proliferation points.");
        }

        if (windBoundary is not null)
        {
            action.WindTurbineDismantleReadback = VerifyWindDismantleBoundary(action.Plan.EntityId, windBoundary);
            if (!SorterDismantlePolicy.ProvesIncRecovery(beforeInc!, CaptureDismantleInventoryInc(player), 0, 0))
                throw new InvalidOperationException("Wind recovery changed inventory proliferation points.");
            action.WindTurbineDismantleReadback.InventoryIncPreserved = true;
        }

        var afterTarget = _reader.InspectFactoryEntityOnMainThread(
            action.SessionId,
            new InspectFactoryEntityRequest { PlanetId = action.PlanetId, ObjectId = action.Plan.EntityId });
        if (afterTarget.Success || afterTarget.Error?.Code != BridgeErrorCodes.InvalidEntity)
        {
            throw new InvalidOperationException("The target entity still exists or its disappearance could not be proven after dismantle.");
        }

        var afterInventory = CaptureInventory(player);
        var changedItemIds = action.BeforeInventory.Keys
            .Concat(afterInventory.Keys)
            .Concat(expectedRecovery.Keys)
            .Distinct()
            .ToArray();
        foreach (var itemId in changedItemIds)
        {
            var actualDelta = GetCount(afterInventory, itemId) - GetCount(action.BeforeInventory, itemId);
            var expectedDelta = GetCount(expectedRecovery, itemId);
            if (actualDelta != expectedDelta)
            {
                throw new InvalidOperationException(
                    $"Normal dismantle inventory recovery for item {itemId} was {actualDelta}, not the expected {expectedDelta}.");
            }
        }

        action.TargetObjectId = action.Plan.EntityId;
        action.TargetItemId = action.Plan.BuildingItemId;
        if (action.WindTurbineDismantleReadback is not null)
            action.WindTurbineDismantleReadback.ExactBuildingRecovery = true;
        Complete(action,
            $"DSP's normal dismantle path removed {targetResult.Value.ComponentKind} {action.Plan.EntityId} and returned its building item plus live recoverable cargo with exact inventory conservation."
            + (sorter ? $" Cargo inc and {survivors.Count} surviving endpoint configurations/connections were verified; no slot write or automatic rebuild occurred." : "")
            + (emptyStorage ? " Empty default2101 refund, unchanged inventory inc and all surviving entities/prebuilds/connections were verified; no automatic rebuild occurred." : "")
            + (emptyBelt ? " Empty2001 head refund, unchanged inventory inc, complete remaining empty chain and all surviving entities/prebuilds/connections were verified; only exact native renderer rotations may change. No automatic rebuild occurred." : "")
            + (wind ? " Wind generator/node removal, projected power connectivity, exact refund and surviving world preservation were verified." : ""));
    }

    private List<FactoryEntitySnapshot> CaptureSorterDismantleNeighbors(FactoryEntitySnapshot target)
    {
        var factory = GameMain.data.localLoadedPlanetFactory;
        if (factory?.factorySystem is null || factory.entityCursor > 8192 || factory.prebuildCursor > 8192
            || factory.entityCursor > factory.entityPool.Length || factory.prebuildCursor > factory.prebuildPool.Length
            || target.ObjectId >= factory.entityCursor)
            throw new InvalidOperationException("Sorter dismantle requires a bounded current local factory (at most8192 pool entries).");
        var id = factory.entityPool[target.ObjectId].inserterId;
        if (id <= 0 || id >= factory.factorySystem.inserterCursor || id >= factory.factorySystem.inserterPool.Length)
            throw new InvalidOperationException("Sorter component identity is unavailable.");
        var native = factory.factorySystem.inserterPool[id];
        if (native.id != id || native.entityId != target.ObjectId
            || native.itemCount > 0 && LDB.items.Select(native.itemId) is null
            || !SorterDismantlePolicy.NativeCargoRecoverable(target.ItemId, native.grade, native.canStack,
                native.bidirectional, native.stackInput, native.stackOutput, native.itemId, native.itemCount, native.itemInc, native.stackCount))
            throw new InvalidOperationException("Sorter grade/stack mode/cargo does not prove ordinary native recovery.");
        var ids = new HashSet<int>(target.Connections.Select(e => e.OtherObjectId));
        if (native.pickTarget > 0) ids.Add(native.pickTarget);
        if (native.insertTarget > 0) ids.Add(native.insertTarget);
        if (native.pickTarget < 0 || native.insertTarget < 0 || ids.Any(n => n <= 0 || n == target.ObjectId))
            throw new InvalidOperationException("Sorter dismantle cannot target prebuild or self-referential endpoints.");
        // Native ClearObjectConn clears the referenced neighbor slot unconditionally.
        // Prove every present/inbound edge first; missing ends are safe, mismatches are not.
        for (var objectId = 1; objectId < factory.entityCursor; objectId++)
        {
            if (factory.entityPool[objectId].id != objectId || objectId == target.ObjectId) continue;
            for (var slot = 0; slot < 16; slot++)
            {
                factory.ReadObjectConn(objectId, slot, out _, out var other, out _);
                if (other == target.ObjectId) ids.Add(objectId);
            }
        }
        for (var prebuild = 1; prebuild < factory.prebuildCursor; prebuild++)
        {
            if (prebuild >= factory.prebuildPool.Length || factory.prebuildPool[prebuild].id != prebuild) continue;
            for (var slot = 0; slot < 16; slot++)
            {
                factory.ReadObjectConn(-prebuild, slot, out _, out var other, out _);
                if (other == target.ObjectId) throw new InvalidOperationException("A pending prebuild references the sorter; wait for ordinary completion before dismantling.");
            }
        }
        if (ids.Count > 4) throw new InvalidOperationException("Sorter has more neighbors than the bounded recovery subset.");
        var neighbors = new List<FactoryEntitySnapshot>();
        foreach (var objectId in ids.OrderBy(n => n))
        {
            var read = _reader.InspectFactoryEntityOnMainThread(target.SessionId,
                new InspectFactoryEntityRequest { PlanetId = target.PlanetId, ObjectId = objectId });
            if (!read.Success || read.Value is null) throw new InvalidOperationException("A present or cached sorter endpoint is not a proven completed entity.");
            neighbors.Add(read.Value);
        }
        if (!SorterDismantlePolicy.PresentConnectionsAreReciprocal(target, neighbors))
            throw new InvalidOperationException("A present sorter connection is nonreciprocal; native deletion could clear an unrelated slot.");
        return neighbors;
    }

    private static Dictionary<int, int> CaptureDismantleInventoryInc(Player player)
    {
        var result = new Dictionary<int, int>();
        if (player.package?.grids is null || player.package.size < 0 || player.package.size > 1024
            || player.package.size > player.package.grids.Length)
            throw new InvalidOperationException("Bounded exact package inc readback is unavailable.");
        for (var i = 0; i < player.package.size; i++)
        {
            var grid = player.package.grids[i];
            if (grid.itemId > 0 && grid.count > 0) result[grid.itemId] = checked(GetCount(result, grid.itemId) + grid.inc);
        }
        if (player.inhandItemId > 0 && player.inhandItemCount > 0)
            result[player.inhandItemId] = checked(GetCount(result, player.inhandItemId) + player.inhandItemInc);
        return result;
    }

    private sealed partial class NormalActionPlanPayload
    {
        public string EmptyBeltNativeStateHash { get; set; } = string.Empty;
        public string WindPowerStateHash { get; set; } = string.Empty;

        public static NormalActionPlanPayload Dismantle(
            string sessionId,
            int planetId,
            string expectedStateHash,
            string playerStateHash,
            string endpointStateHash,
            int entityId,
            int buildingItemId, string emptyStorageNativeHash = "", string emptyBeltNativeHash = "") => new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.Dismantle,
                SessionId = sessionId,
                PlanetId = planetId,
                ExpectedStateHash = expectedStateHash,
                PlayerStateHash = playerStateHash,
                FactoryStateHash = endpointStateHash,
                EntityId = entityId,
                BuildingItemId = buildingItemId,
                StorageNativeStateHash = emptyStorageNativeHash,
                EmptyBeltNativeStateHash = emptyBeltNativeHash,
                Count = 1,
                EstimatedTicks = 1,
            };
    }
}
