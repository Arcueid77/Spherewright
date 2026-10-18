using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameCallResult<PreparedNormalAction> PrepareRefuelPlanOnMainThread(
        string? requestedSessionId,
        PrepareRefuelRequest request)
    {
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        }

        if (request.ItemId <= 0 || request.Count <= 0)
        {
            return InvalidPlan("Fuel item and count must be positive.");
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
            return StalePlan("Player inventory or mecha fuel state changed after inspection.");
        }

        var player = GameMain.mainPlayer;
        var item = LDB.items.Select(request.ItemId);
        if (player?.package is null || player.mecha?.reactorStorage is null)
        {
            return NotReadyPlan("The player package or mecha fuel chamber is unavailable.");
        }

        if (item is null
            || StorageComponent.itemIsFuel is null
            || request.ItemId >= StorageComponent.itemIsFuel.Length
            || !OrdinaryMechaFuelPolicy.IsAccepted(item.HeatValue, item.FuelType, StorageComponent.itemIsFuel[request.ItemId]))
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest,
                "The requested runtime item is not accepted as ordinary mecha fuel.",
                false,
                "Inspect the current recipe catalog for acceptedAsMechaFuel=true, acquire a real stack and prepare its exact native transfer count."));
        }

        if (!TryResolveRefuelTransfer(player, request.ItemId, out var grid, out var exactCount, out var rejection))
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                rejection.IndexOf("package", StringComparison.OrdinalIgnoreCase) >= 0
                    ? BridgeErrorCodes.InventoryInsufficient
                    : BridgeErrorCodes.InventoryFull,
                rejection,
                true,
                "Acquire ordinary fuel or wait for fuel-chamber capacity, then inspect and prepare again."));
        }

        if (request.Count != exactCount)
        {
            return GameCallResult<PreparedNormalAction>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest,
                $"DSP's native one-stack fuel transfer will move exactly {exactCount} item(s) into grid {grid}; the requested count was {request.Count}.",
                true,
                "Use the exact count reported by current package and fuel-grid capacity, then prepare again."));
        }

        var playerActionHash = CanonicalStateHash.PlayerAction(playerResult.Value);
        var expectedHash = CanonicalStateHash.Combine(
            NormalActionKinds.Refuel,
            _sessions.SessionId,
            request.PlanetId,
            playerActionHash,
            request.ItemId,
            request.Count,
            grid);
        var payload = NormalActionPlanPayload.Refuel(
            _sessions.SessionId!,
            request.PlanetId,
            expectedHash,
            playerActionHash,
            request.ItemId,
            request.Count,
            grid);
        var prepared = AddPreparedPlan(
            payload,
            common.Session!,
            1,
            "Mecha.AutoReplenishFuel moves the exact prepared stack from the player package into the bound fuel grid, and combined item count is conserved.");
        if (prepared.Success && prepared.Value is not null)
        {
            prepared.Value.ItemBudget.Add(new ActionItemBudget
            {
                ItemId = request.ItemId,
                Name = item.name ?? string.Empty,
                Count = request.Count,
                Direction = "player-to-mecha-fuel",
            });
        }

        return prepared;
    }

    private BridgeError? RevalidateRefuelOnMainThread(NormalActionPlanPayload plan)
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
            return Stale("Player inventory or mecha fuel state changed after refuel preparation.");
        }

        var player = GameMain.mainPlayer;
        if (!TryResolveRefuelTransfer(player, plan.FuelItemId, out var grid, out var exactCount, out _)
            || grid != plan.FuelGrid
            || exactCount != plan.Count)
        {
            return Stale("The exact native fuel transfer count or destination grid changed after prepare.");
        }

        return null;
    }

    private static void ExecuteRefuelOnMainThread(ActionRecord action)
    {
        var player = GameMain.mainPlayer
            ?? throw new InvalidOperationException("The player is unavailable.");
        var plan = action.Plan;
        var fuelStorage = player.mecha?.reactorStorage
            ?? throw new InvalidOperationException("The mecha fuel chamber is unavailable.");
        var playerBefore = player.package.GetItemCount(plan.FuelItemId, out var playerIncBefore);
        var fuelBefore = fuelStorage.GetItemCount(plan.FuelItemId, out var fuelIncBefore);

        if (!player.mecha.AutoReplenishFuel(plan.FuelItemId, plan.FuelGrid))
        {
            throw new InvalidOperationException("DSP's native mecha fuel transfer rejected the prepared stack.");
        }

        var playerAfter = player.package.GetItemCount(plan.FuelItemId, out var playerIncAfter);
        var fuelAfter = fuelStorage.GetItemCount(plan.FuelItemId, out var fuelIncAfter);
        if (playerBefore - playerAfter != plan.Count
            || fuelAfter - fuelBefore != plan.Count
            || playerBefore + fuelBefore != playerAfter + fuelAfter
            || playerIncBefore + fuelIncBefore != playerIncAfter + fuelIncAfter)
        {
            throw new InvalidOperationException("Mecha refuel readback did not prove exact bilateral item conservation.");
        }

        action.TargetItemId = plan.FuelItemId;
        action.BeforeTargetAmount = fuelBefore;
        action.AfterTargetAmount = fuelAfter;
        action.Message = $"DSP's native mecha fuel transfer conserved item {plan.FuelItemId}: player {playerBefore}->{playerAfter}, fuel chamber {fuelBefore}->{fuelAfter}.";
        action.State = NormalActionStates.Completed;
        action.Terminal = true;
        action.Succeeded = true;
        action.CompletedAtGameTick = GameMain.gameTick;
        action.AfterInventory = CaptureInventory(player);
        action.AfterStateHash = CanonicalStateHash.Combine(
            NormalActionKinds.Refuel,
            playerAfter,
            fuelAfter,
            playerIncAfter,
            fuelIncAfter,
            plan.FuelItemId,
            plan.Count,
            plan.FuelGrid);
    }

    private static bool TryResolveRefuelTransfer(
        Player? player,
        int itemId,
        out int gridIndex,
        out int exactCount,
        out string rejection)
    {
        gridIndex = -1;
        exactCount = 0;
        rejection = string.Empty;
        var package = player?.package;
        var storage = player?.mecha?.reactorStorage;
        if (package is null || storage?.grids is null || storage.type != EStorageType.Fuel)
        {
            rejection = "The player package or native fuel-typed chamber is unavailable.";
            return false;
        }

        var packageCount = package.GetItemCount(itemId);
        if (packageCount <= 0)
        {
            rejection = "The player package contains no requested fuel item.";
            return false;
        }

        if (itemId >= StorageComponent.itemStackCount.Length
            || itemId >= StorageComponent.itemIsFuel.Length
            || !StorageComponent.itemIsFuel[itemId])
        {
            rejection = "The runtime item is not accepted by the current fuel-storage type.";
            return false;
        }

        var stackLimit = StorageComponent.itemStackCount[itemId];
        var gridCount = Math.Min(storage.size, storage.grids.Length);
        for (var index = 0; index < gridCount; index++)
        {
            var grid = storage.grids[index];
            var gridLimit = grid.stackSize > 0 ? grid.stackSize : stackLimit;
            var capacity = Math.Min(stackLimit, gridLimit - grid.count);
            if (grid.itemId == itemId && capacity > 0)
            {
                gridIndex = index;
                exactCount = Math.Min(packageCount, capacity);
                return exactCount > 0;
            }
        }

        for (var index = 0; index < gridCount; index++)
        {
            var grid = storage.grids[index];
            if (grid.itemId <= 0 && (grid.filter <= 0 || grid.filter == itemId))
            {
                var gridLimit = grid.filter > 0 && grid.stackSize > 0 ? grid.stackSize : stackLimit;
                gridIndex = index;
                exactCount = Math.Min(packageCount, Math.Min(stackLimit, gridLimit));
                return exactCount > 0;
            }
        }

        rejection = "The mecha fuel chamber has no compatible free stack capacity.";
        return false;
    }

    private GameCallResult<PreparedNormalAction> PrepareSavePlanOnMainThread(
        string? requestedSessionId,
        PrepareSaveRequest request)
    {
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null)
        {
            return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        }

        if (request.ExpectedRevision != common.Session!.Revision)
        {
            return StalePlan("The owned session revision changed after inspection.");
        }

        if (string.IsNullOrWhiteSpace(common.Session.SaveName)
            || GameMain.data?.localLoadedPlanetFactory is null)
        {
            return NotReadyPlan("The exact Spherewright-owned save identity or local factory is unavailable.");
        }

        var expectedHash = CanonicalStateHash.Combine(
            NormalActionKinds.Save,
            _sessions.SessionId,
            request.PlanetId,
            request.ExpectedRevision,
            common.Session.SaveName);
        var payload = NormalActionPlanPayload.Save(
            _sessions.SessionId!,
            request.PlanetId,
            expectedHash,
            common.Session.SaveName!,
            request.ExpectedRevision);
        return AddPreparedPlan(
            payload,
            common.Session,
            1,
            "GameSave.SaveCurrentGame returns true for the exact high-entropy save name owned by this session, and the saved game tick is recorded.");
    }

    private BridgeError? RevalidateSaveOnMainThread(NormalActionPlanPayload plan)
    {
        var session = _sessions.CaptureOnMainThread();
        if (!session.OwnedBySpherewright
            || session.LocalPlanetId != plan.PlanetId
            || session.Revision != plan.SaveExpectedRevision
            || !string.Equals(session.SaveName, plan.SaveOwnedName, StringComparison.Ordinal)
            || GameMain.data?.localLoadedPlanetFactory is null)
        {
            return Stale("The owned save identity, planet, factory, or session revision changed after prepare.");
        }

        return null;
    }

    private void ExecuteSaveOnMainThread(ActionRecord action)
    {
        if (!_sessions.TrySaveOwnedWorldNowOnMainThread(out var error))
        {
            throw new InvalidOperationException(error ?? "DSP's normal save API did not confirm success.");
        }

        var session = _sessions.CaptureOnMainThread();
        if (!session.LastOwnedSaveGameTick.HasValue)
        {
            throw new InvalidOperationException("The owned save completed without a recorded game tick.");
        }

        CloseVoyagerAfterNormalSave(action, session);

        action.State = NormalActionStates.Completed;
        action.Terminal = true;
        action.Succeeded = true;
        action.CompletedAtGameTick = GameMain.gameTick;
        action.Message = $"DSP's normal save API confirmed the exact owned save at game tick {session.LastOwnedSaveGameTick.Value}.";
        action.AfterInventory = CaptureInventory(GameMain.mainPlayer);
        action.AfterStateHash = CanonicalStateHash.Combine(
            NormalActionKinds.Save,
            action.SessionId,
            action.PlanetId,
            session.LastOwnedSaveGameTick.Value,
            session.Revision);
    }

    private sealed partial class NormalActionPlanPayload
    {
        public int FuelItemId { get; private set; }

        public int FuelGrid { get; private set; }

        public long SaveExpectedRevision { get; private set; }

        public string SaveOwnedName { get; private set; } = string.Empty;

        public static NormalActionPlanPayload Refuel(
            string sessionId,
            int planetId,
            string expectedStateHash,
            string playerStateHash,
            int itemId,
            int count,
            int grid) => new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.Refuel,
                SessionId = sessionId,
                PlanetId = planetId,
                ExpectedStateHash = expectedStateHash,
                PlayerStateHash = playerStateHash,
                FuelItemId = itemId,
                Count = count,
                FuelGrid = grid,
                EstimatedTicks = 1,
            };

        public static NormalActionPlanPayload Save(
            string sessionId,
            int planetId,
            string expectedStateHash,
            string saveOwnedName,
            long expectedRevision) => new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.Save,
                SessionId = sessionId,
                PlanetId = planetId,
                ExpectedStateHash = expectedStateHash,
                SaveOwnedName = saveOwnedName,
                SaveExpectedRevision = expectedRevision,
                EstimatedTicks = 1,
            };
    }
}
