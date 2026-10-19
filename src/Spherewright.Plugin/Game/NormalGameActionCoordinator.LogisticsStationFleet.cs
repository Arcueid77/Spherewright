using Spherewright.Bridge.Core.Logistics;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Logistics;
using Spherewright.Contracts.Sessions;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameCallResult<PreparedNormalAction> PrepareStationFleetTransferOnMainThread(
        string? requestedSessionId,
        PrepareLogisticsStationFleetTransferRequest request)
    {
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        }

        if (request.Direction != LogisticsStationFleetTransferDirections.PlayerToStation
            && request.Direction != LogisticsStationFleetTransferDirections.StationToPlayer)
        {
            return InvalidPlan("Fleet transfer direction must be player-to-station or station-to-player.");
        }

        if (request.ItemId != LogisticsFleetItemIds.Drone
            && request.ItemId != LogisticsFleetItemIds.Vessel
            && request.ItemId != LogisticsFleetItemIds.Warper)
        {
            return InvalidPlan("Only logistics drones (5001), vessels (5002), and station warpers (1210) are supported.");
        }

        if (request.Count <= 0 || request.Count > 100)
        {
            return InvalidPlan("Fleet transfer count must be from 1 through 100.");
        }

        var playerResult = _reader.GetPlayerStateOnMainThread(
            requestedSessionId,
            new LocalPlanetRequest { PlanetId = request.PlanetId });
        var stationResult = _reader.InspectFactoryEntityOnMainThread(
            requestedSessionId,
            new InspectFactoryEntityRequest
            {
                PlanetId = request.PlanetId,
                ObjectId = request.StationEntityId,
            });
        if (!playerResult.Success || playerResult.Value is null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(playerResult.Error!);
        }

        if (!stationResult.Success || stationResult.Value?.LogisticsStation is null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(stationResult.Error ?? BridgeError.Create(
                BridgeErrorCodes.InvalidEntity,
                "The target is not an exact completed logistics station.",
                false,
                "Inspect a completed planetary or interstellar logistics station and use its positive entity ID."));
        }

        var stationSnapshot = stationResult.Value.LogisticsStation;
        if (!string.Equals(playerResult.Value.StateHash, request.ExpectedPlayerStateHash, StringComparison.Ordinal)
            || !string.Equals(stationSnapshot.FleetStateHash, request.ExpectedStationFleetStateHash, StringComparison.Ordinal))
        {
            return StalePlan("Player inventory or the exact station fleet changed after inspection.");
        }

        var factory = GameMain.localPlanet?.factory;
        var player = GameMain.mainPlayer;
        if (factory is null || player?.package is null
            || !TryGetFleetStation(factory, request.StationEntityId, out var station, out var droneCapacity, out var vesselCapacity))
        {
            return NotReadyPlan("The exact logistics-station fleet component is unavailable.");
        }

        var distance = Vector3.Distance(player.position, ToVector(stationSnapshot.Position));
        if (distance > player.mecha.buildArea)
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.TargetOutOfRange,
                $"The station is {distance:F2} metres away, outside the current normal interaction/build area.",
                true,
                "Move into range through spherewright_prepare_move, then inspect and prepare again."));
        }

        if (player.inhandItemId != 0 || player.inhandItemCount != 0 || player.inhandItemInc != 0)
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.PlayerBusy,
                "The player's hand must be empty before a bounded station fleet transfer.",
                true,
                "Finish the current hand-item interaction, then inspect and prepare again."));
        }

        var playerCount = player.package.GetItemCount(request.ItemId, out var playerInc);
        var canAcceptWithdrawal = CanPlayerPackageAcceptExactly(player.package, request.ItemId, request.Count);
        if (!LogisticsStationFleetTransferPolicy.TryValidate(
                station!.isStellar,
                station.isCollector,
                station.isVeinCollector,
                request.Direction,
                request.ItemId,
                request.Count,
                playerCount,
                playerInc,
                station.idleDroneCount,
                station.workDroneCount,
                droneCapacity,
                station.idleShipCount,
                station.workShipCount,
                vesselCapacity,
                canAcceptWithdrawal,
                GameMain.history.logisticShipWarpDrive,
                station.warperCount,
                station.warperMaxCount,
                out var rejection))
        {
            var insufficient = rejection.IndexOf("fewer", StringComparison.OrdinalIgnoreCase) >= 0;
            var full = rejection.IndexOf("capacity", StringComparison.OrdinalIgnoreCase) >= 0
                       || rejection.IndexOf("cannot accept", StringComparison.OrdinalIgnoreCase) >= 0;
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                insufficient ? BridgeErrorCodes.InventoryInsufficient
                    : full ? BridgeErrorCodes.InventoryFull
                    : BridgeErrorCodes.InvalidRequest,
                rejection,
                true,
                "Inspect the current player and station fleet, then adjust the direction or count."));
        }

        var playerActionHash = CanonicalStateHash.PlayerAction(playerResult.Value);
        var expectedHash = CanonicalStateHash.Combine(
            NormalActionKinds.LogisticsStationFleetTransfer,
            _sessions.SessionId,
            request.PlanetId,
            playerActionHash,
            stationSnapshot.FleetStateHash,
            request.StationEntityId,
            request.Direction,
            request.ItemId,
            request.Count);
        var payload = NormalActionPlanPayload.StationFleetTransfer(
            _sessions.SessionId!,
            request.PlanetId,
            expectedHash,
            playerActionHash,
            stationSnapshot.FleetStateHash,
            request.StationEntityId,
            request.Direction,
            request.ItemId,
            request.Count);
        var prepared = AddPreparedPlan(
            payload,
            common.Session!,
            1,
            "The player package and matching idle craft or native warper counter change by equal-and-opposite amounts; all other craft/counters, station storage, and energy remain unchanged. Warpers require native warp-drive technology/capacity and zero proliferator points.");
        if (prepared.Success && prepared.Value is not null)
        {
            prepared.Value.SourceObjectId = request.Direction == LogisticsStationFleetTransferDirections.StationToPlayer
                ? request.StationEntityId
                : (int?)null;
            prepared.Value.DestinationObjectId = request.Direction == LogisticsStationFleetTransferDirections.PlayerToStation
                ? request.StationEntityId
                : (int?)null;
            prepared.Value.EstimatedDistance = distance;
            prepared.Value.ItemBudget.Add(new ActionItemBudget
            {
                ItemId = request.ItemId,
                Name = LDB.items.Select(request.ItemId)?.name ?? string.Empty,
                Count = request.Count,
                Direction = request.Direction,
            });
        }

        return prepared;
    }

    private BridgeError? RevalidateStationFleetTransferOnMainThread(NormalActionPlanPayload plan)
    {
        var playerResult = _reader.GetPlayerStateOnMainThread(
            plan.SessionId,
            new LocalPlanetRequest { PlanetId = plan.PlanetId });
        var stationResult = _reader.InspectFactoryEntityOnMainThread(
            plan.SessionId,
            new InspectFactoryEntityRequest
            {
                PlanetId = plan.PlanetId,
                ObjectId = plan.FleetTransferStationEntityId,
            });
        if (!playerResult.Success || playerResult.Value is null)
        {
            return playerResult.Error;
        }

        if (!stationResult.Success || stationResult.Value?.LogisticsStation is null)
        {
            return stationResult.Error ?? Stale("The exact logistics station disappeared after preparation.");
        }

        var stationSnapshot = stationResult.Value.LogisticsStation;
        if (!string.Equals(CanonicalStateHash.PlayerAction(playerResult.Value), plan.PlayerStateHash, StringComparison.Ordinal)
            || !string.Equals(stationSnapshot.FleetStateHash, plan.StationFleetStateHash, StringComparison.Ordinal))
        {
            return Stale("Player inventory or station fleet changed after transfer preparation.");
        }

        var factory = GameMain.localPlanet?.factory;
        var player = GameMain.mainPlayer;
        if (factory is null || player?.package is null
            || player.inhandItemId != 0 || player.inhandItemCount != 0 || player.inhandItemInc != 0
            || !TryGetFleetStation(factory, plan.FleetTransferStationEntityId, out var station, out var droneCapacity, out var vesselCapacity))
        {
            return Stale("The player hand or exact station identity changed after preparation.");
        }

        var playerCount = player.package.GetItemCount(plan.FleetTransferItemId, out var playerInc);
        return LogisticsStationFleetTransferPolicy.TryValidate(
            station!.isStellar,
            station.isCollector,
            station.isVeinCollector,
            plan.FleetTransferDirection,
            plan.FleetTransferItemId,
            plan.Count,
            playerCount,
            playerInc,
            station.idleDroneCount,
            station.workDroneCount,
            droneCapacity,
            station.idleShipCount,
            station.workShipCount,
            vesselCapacity,
            CanPlayerPackageAcceptExactly(player.package, plan.FleetTransferItemId, plan.Count),
            GameMain.history.logisticShipWarpDrive,
            station.warperCount,
            station.warperMaxCount,
            out _)
            ? null
            : Stale("Fleet source count, destination capacity, idle availability, or current-version station capacity changed.");
    }

    private void ExecuteStationFleetTransferOnMainThread(ActionRecord action)
    {
        var factory = GameMain.localPlanet?.factory
            ?? throw new InvalidOperationException("The local factory is unavailable.");
        var player = GameMain.mainPlayer
            ?? throw new InvalidOperationException("The player is unavailable.");
        var plan = action.Plan;
        if (!TryGetFleetStation(factory, plan.FleetTransferStationEntityId, out var station, out _, out _))
        {
            throw new InvalidOperationException("The exact logistics station disappeared.");
        }

        var stationBeforeResult = _reader.InspectFactoryEntityOnMainThread(
            plan.SessionId,
            new InspectFactoryEntityRequest
            {
                PlanetId = plan.PlanetId,
                ObjectId = plan.FleetTransferStationEntityId,
            });
        var stationBefore = stationBeforeResult.Value?.LogisticsStation
            ?? throw new InvalidOperationException("The station fleet could not be captured before transfer.");
        var playerBefore = player.package.GetItemCount(plan.FleetTransferItemId, out var playerIncBefore);
        var isWarper = plan.FleetTransferItemId == LogisticsFleetItemIds.Warper;
        var idleBefore = isWarper ? station!.warperCount : plan.FleetTransferItemId == LogisticsFleetItemIds.Drone
            ? station!.idleDroneCount
            : station!.idleShipCount;
        var workingBefore = isWarper ? 0 : plan.FleetTransferItemId == LogisticsFleetItemIds.Drone
            ? station.workDroneCount
            : station.workShipCount;
        var packageOtherStateBefore = CapturePlayerPackageStateExcludingItem(player.package, plan.FleetTransferItemId);
        var stationStorageBefore = CaptureLogisticsStationStorageState(station);
        var energyBefore = station.energy;
        var warpersBefore = station.warperCount;

        if (plan.FleetTransferDirection == LogisticsStationFleetTransferDirections.PlayerToStation)
        {
            var removed = player.package.TakeItem(plan.FleetTransferItemId, plan.Count, out var removedInc);
            if (removed != plan.Count || removedInc != 0)
            {
                throw new InvalidOperationException("The player package did not remove the prepared unproliferated fleet count.");
            }

            if (plan.FleetTransferItemId == LogisticsFleetItemIds.Drone)
            {
                station.idleDroneCount += removed;
            }
            else if (isWarper)
            {
                station.warperCount += removed;
            }
            else
            {
                station.idleShipCount += removed;
            }
        }
        else
        {
            var added = player.package.AddItemStacked(plan.FleetTransferItemId, plan.Count, 0, out var remainingInc);
            if (added != plan.Count || remainingInc != 0)
            {
                throw new InvalidOperationException("The player package did not accept the prepared idle fleet count.");
            }

            if (plan.FleetTransferItemId == LogisticsFleetItemIds.Drone)
            {
                station.idleDroneCount -= added;
            }
            else if (isWarper)
            {
                station.warperCount -= added;
            }
            else
            {
                station.idleShipCount -= added;
            }

            player.NotifyPackageAddItem(plan.FleetTransferItemId, added, 0);
        }

        var stationAfterResult = _reader.InspectFactoryEntityOnMainThread(
            plan.SessionId,
            new InspectFactoryEntityRequest
            {
                PlanetId = plan.PlanetId,
                ObjectId = plan.FleetTransferStationEntityId,
            });
        var stationAfter = stationAfterResult.Value?.LogisticsStation
            ?? throw new InvalidOperationException("The station fleet could not be captured after transfer.");
        var playerAfter = player.package.GetItemCount(plan.FleetTransferItemId, out var playerIncAfter);
        var idleAfter = isWarper ? station.warperCount : plan.FleetTransferItemId == LogisticsFleetItemIds.Drone
            ? station.idleDroneCount
            : station.idleShipCount;
        var workingAfter = isWarper ? 0 : plan.FleetTransferItemId == LogisticsFleetItemIds.Drone
            ? station.workDroneCount
            : station.workShipCount;
        var expectedPlayerDelta = plan.FleetTransferDirection == LogisticsStationFleetTransferDirections.PlayerToStation
            ? -plan.Count
            : plan.Count;
        if (playerAfter - playerBefore != expectedPlayerDelta
            || idleAfter - idleBefore != -expectedPlayerDelta
            || workingAfter != workingBefore
            || playerBefore + idleBefore + workingBefore != playerAfter + idleAfter + workingAfter
            || playerIncAfter != playerIncBefore
            || (plan.FleetTransferItemId != LogisticsFleetItemIds.Drone
                && (station.idleDroneCount != stationBefore.IdleDroneCount || station.workDroneCount != stationBefore.WorkingDroneCount))
            || (plan.FleetTransferItemId != LogisticsFleetItemIds.Vessel
                && (station.idleShipCount != stationBefore.IdleVesselCount || station.workShipCount != stationBefore.WorkingVesselCount))
            || player.inhandItemId != 0 || player.inhandItemCount != 0 || player.inhandItemInc != 0
            || station.energy != energyBefore
            || (!isWarper && station.warperCount != warpersBefore)
            || !string.Equals(packageOtherStateBefore, CapturePlayerPackageStateExcludingItem(player.package, plan.FleetTransferItemId), StringComparison.Ordinal)
            || !string.Equals(stationStorageBefore, CaptureLogisticsStationStorageState(station), StringComparison.Ordinal)
            || !string.Equals(stationBefore.ConfigurationStateHash, stationAfter.ConfigurationStateHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Post-transfer readback did not prove exact fleet conservation and preservation of unrelated station/player state.");
        }

        action.TargetObjectId = plan.FleetTransferStationEntityId;
        action.TargetItemId = plan.FleetTransferItemId;
        action.BeforeTargetAmount = idleBefore;
        action.AfterTargetAmount = idleAfter;
        action.Message = $"Normal station fleet transfer conserved item {plan.FleetTransferItemId}: player {playerBefore}->{playerAfter}, idle fleet {idleBefore}->{idleAfter}, working fleet {workingBefore}.";
        action.State = NormalActionStates.Completed;
        action.Terminal = true;
        action.Succeeded = true;
        action.CompletedAtGameTick = GameMain.gameTick;
        action.AfterInventory = CaptureInventory(player);
        action.AfterStateHash = CanonicalStateHash.Combine(
            NormalActionKinds.LogisticsStationFleetTransfer,
            CanonicalStateHash.PlayerAction(_reader.GetPlayerStateOnMainThread(
                plan.SessionId,
                new LocalPlanetRequest { PlanetId = plan.PlanetId }).Value!),
            stationAfter.FleetStateHash,
            plan.FleetTransferItemId,
            plan.Count);
    }

    private static bool TryGetFleetStation(
        PlanetFactory factory,
        int entityId,
        out StationComponent? station,
        out int droneCapacity,
        out int vesselCapacity)
    {
        station = null;
        droneCapacity = 0;
        vesselCapacity = 0;
        if (entityId <= 0 || entityId >= factory.entityCursor || entityId >= factory.entityPool.Length)
        {
            return false;
        }

        ref var entity = ref factory.entityPool[entityId];
        var prefab = entity.id == entityId ? LDB.items.Select(entity.protoId)?.prefabDesc : null;
        if (prefab is null || entity.stationId <= 0)
        {
            return false;
        }

        station = factory.transport?.GetStationComponent(entity.stationId);
        if (station is null
            || station.id != entity.stationId
            || station.entityId != entityId
            || !LogisticsStationIdentityPolicy.MatchesLocalPlanet(
                station.isStellar,
                station.planetId,
                factory.planetId))
        {
            station = null;
            return false;
        }

        droneCapacity = prefab.stationMaxDroneCount;
        vesselCapacity = prefab.stationMaxShipCount;
        return true;
    }

    private static bool CanPlayerPackageAcceptExactly(StorageComponent package, int itemId, int count)
    {
        using var copy = new StorageCopy(package);
        var added = copy.Value.AddItemStacked(itemId, count, 0, out var remainingInc);
        return added == count && remainingInc == 0;
    }

    private static string CapturePlayerPackageStateExcludingItem(StorageComponent package, int excludedItemId)
    {
        var fields = new List<object?> { package.size, package.bans, package.isPlayerInventory };
        var grids = package.grids ?? Array.Empty<StorageComponent.GRID>();
        for (var index = 0; index < grids.Length; index++)
        {
            var grid = grids[index];
            if (grid.itemId == 0 || grid.itemId == excludedItemId)
            {
                continue;
            }

            fields.Add(index);
            fields.Add(grid.itemId);
            fields.Add(grid.count);
            fields.Add(grid.inc);
        }

        return CanonicalStateHash.Combine("player-package-excluding-item-v1", fields.ToArray());
    }

    private sealed partial class NormalActionPlanPayload
    {
        public static NormalActionPlanPayload StationFleetTransfer(
            string sessionId,
            int planetId,
            string expectedStateHash,
            string playerStateHash,
            string stationFleetStateHash,
            int stationEntityId,
            string direction,
            int itemId,
            int count) => new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.LogisticsStationFleetTransfer,
                SessionId = sessionId,
                PlanetId = planetId,
                ExpectedStateHash = expectedStateHash,
                PlayerStateHash = playerStateHash,
                StationFleetStateHash = stationFleetStateHash,
                FleetTransferStationEntityId = stationEntityId,
                FleetTransferDirection = direction,
                FleetTransferItemId = itemId,
                Count = count,
                EstimatedTicks = 1,
            };
    }
}
