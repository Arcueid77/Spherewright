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
    private sealed class AssemblerInputBinding
    {
        internal PlanetFactory Factory = null!;
        internal StorageComponent Package = null!;
        internal EntityData[] Entities = null!;
        internal AssemblerComponent[] Assemblers = null!;
        internal object RecipeData = null!;
        internal int AssemblerId;
        internal int InputIndex;
    }

    private sealed partial class NormalActionPlanPayload
    {
        public AssemblerInputBinding? AssemblerInput { get; set; }
    }

    private static string? CaptureAssemblerInput(Player player, PlanetFactory factory, int entityId, int itemId, int count,
        out AssemblerInputBinding? binding, out int buildingItemId, out int recipeId, out int beforeInputCount)
    {
        binding = null; buildingItemId = recipeId = beforeInputCount = 0;
        var package = player.package; var system = factory.factorySystem;
        if (player.mecha is null || package?.grids is null || package.size < 1 || package.size > package.grids.Length
            || !ReferenceEquals(player.factory, factory) || ReferenceEquals(package, player.mecha.reactorStorage)
            || factory.entityPool is null || factory.entityNeeds is null || factory.entityMutexs is null
            || entityId <= 0 || entityId >= factory.entityCursor || entityId >= factory.entityPool.Length
            || entityId >= factory.entityNeeds.Length || entityId >= factory.entityMutexs.Length
            || factory.entityNeeds[entityId] is null || factory.entityMutexs[entityId] is null || system?.assemblerPool is null)
            return "assembler_input_native_pools_unavailable";
        ref var entity = ref factory.entityPool[entityId]; var assemblerId = entity.assemblerId;
        if (entity.id != entityId || assemblerId <= 0 || assemblerId >= system.assemblerCursor
            || assemblerId >= system.assemblerPool.Length)
            return "assembler_input_component_unavailable";
        ref var assembler = ref system.assemblerPool[assemblerId]; var recipe = assembler.recipeExecuteData;
        var requires = recipe?.requires;
        if (assembler.id != assemblerId || assembler.entityId != entityId || assembler.recipeId <= 0
            || requires is null || requires.Length < 1 || requires.Length > 6
            || assembler.served is null || assembler.incServed is null || assembler.produced is null
            || assembler.served.Length < requires.Length || assembler.incServed.Length < requires.Length)
            return "assembler_input_recipe_or_buffers_unavailable";
        var indexes = Enumerable.Range(0, requires.Length).Where(index => requires[index] == itemId).ToArray();
        if (indexes.Length != 1) return "assembler_input_not_required_by_current_recipe";
        var inputIndex = indexes[0];
        var validInc = package.grids.Take(package.size).Where(grid => grid.itemId == itemId && grid.count > 0)
            .All(grid => grid.inc >= 0 && (long)grid.inc <= (long)grid.count * 10L);
        var emptyHand = player.inhandItemId == 0 && player.inhandItemCount == 0 && player.inhandItemInc == 0;
        if (!AssemblerInputPolicy.CanInsert(itemId, count, package.GetItemCount(itemId), assembler.served[inputIndex],
                assembler.incServed[inputIndex], true, LDB.items.Select(itemId) is not null,
                Vector3.Distance(player.position, entity.pos) <= player.mecha.buildArea, emptyHand, validInc))
            return "assembler_input_inventory_range_or_count_unavailable";
        buildingItemId = entity.protoId; recipeId = assembler.recipeId; beforeInputCount = assembler.served[inputIndex];
        binding = new() { Factory = factory, Package = package, Entities = factory.entityPool,
            Assemblers = system.assemblerPool, RecipeData = recipe!, AssemblerId = assemblerId, InputIndex = inputIndex };
        return null;
    }

    private GameCallResult<PreparedNormalAction> PrepareAssemblerInputOnMainThread(string? sessionId, PrepareTransferRequest request)
    {
        var common = ValidatePrepareCommon(sessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null) return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        if (request.Direction != TransferDirections.PlayerToAssemblerInput || request.ItemId <= 0 || request.Count < 1 || request.Count > 100)
            return InvalidPlan("Assembler input requires player-to-assembler-input and an exact count from 1 through 100.");
        var player = _reader.GetPlayerStateOnMainThread(sessionId, new LocalPlanetRequest { PlanetId = request.PlanetId });
        var entity = _reader.InspectFactoryEntityOnMainThread(sessionId, new InspectFactoryEntityRequest { PlanetId = request.PlanetId, ObjectId = request.StorageEntityId });
        if (!player.Success || player.Value is null) return GameCallResult<PreparedNormalAction>.Failed(player.Error!);
        if (!entity.Success || entity.Value is null) return GameCallResult<PreparedNormalAction>.Failed(entity.Error!);
        if (player.Value.StateHash != request.ExpectedPlayerStateHash || entity.Value.StateHash != request.ExpectedStorageStateHash)
            return StalePlan("Player or exact assembler changed after inspection.");
        var factory = GameMain.localPlanet?.factory; var livePlayer = GameMain.mainPlayer;
        if (factory is null || livePlayer is null) return NotReadyPlan("The current owned factory/player is unavailable.");
        var blocker = CaptureAssemblerInput(livePlayer, factory, request.StorageEntityId, request.ItemId, request.Count,
            out var binding, out var building, out var recipe, out var before);
        if (blocker is not null) return StalePlan(blocker);
        var playerHash = CanonicalStateHash.PlayerAction(player.Value);
        var hash = CanonicalStateHash.Combine("assembler-input-v1", _sessions.SessionId, request.PlanetId, playerHash,
            entity.Value.StateHash, request.StorageEntityId, request.Direction, request.ItemId, request.Count, recipe, before);
        var payload = NormalActionPlanPayload.Transfer(_sessions.SessionId!, request.PlanetId, hash, playerHash,
            entity.Value.StateHash, request.StorageEntityId, request.Direction, request.ItemId, request.Count);
        payload.AssemblerInput = binding;
        var prepared = AddPreparedPlan(payload, common.Session!, 1,
            "Normal package removal and Native assembler insertion preserve item count and proliferator points at the same tick; later processing is separate.");
        if (prepared.Success && prepared.Value is { } plan)
        {
            plan.DestinationObjectId = request.StorageEntityId;
            plan.AssemblerInput = new() { EvidenceVersion = AssemblerInputPolicy.EvidenceVersion, EntityId = request.StorageEntityId,
                BuildingItemId = building, RecipeId = recipe, ItemId = request.ItemId, Count = request.Count,
                BeforeInputCount = before, NativeOperation = AssemblerInputPolicy.NativeOperation };
            plan.ItemBudget.Add(new() { ItemId = request.ItemId, Count = request.Count, Direction = request.Direction });
        }
        return prepared;
    }

    private BridgeError? RevalidateAssemblerInputOnMainThread(NormalActionPlanPayload plan)
    {
        var binding = plan.AssemblerInput; var factory = GameMain.localPlanet?.factory; var player = GameMain.mainPlayer;
        if (binding is null || factory is null || player is null || !ReferenceEquals(factory, binding.Factory)
            || !ReferenceEquals(player.package, binding.Package) || !ReferenceEquals(factory.entityPool, binding.Entities)
            || !ReferenceEquals(factory.factorySystem?.assemblerPool, binding.Assemblers))
            return Stale("assembler_input_original_native_binding_lost");
        var currentPlayer = _reader.GetPlayerStateOnMainThread(plan.SessionId, new LocalPlanetRequest { PlanetId = plan.PlanetId });
        var entity = _reader.InspectFactoryEntityOnMainThread(plan.SessionId, new InspectFactoryEntityRequest { PlanetId = plan.PlanetId, ObjectId = plan.TransferStorageEntityId });
        if (!currentPlayer.Success || currentPlayer.Value is null) return currentPlayer.Error;
        if (!entity.Success || entity.Value is null) return entity.Error;
        if (CanonicalStateHash.PlayerAction(currentPlayer.Value) != plan.PlayerStateHash || entity.Value.StateHash != plan.TransferStorageStateHash)
            return Stale("assembler_input_inventory_or_device_changed");
        var blocker = CaptureAssemblerInput(player, factory, plan.TransferStorageEntityId, plan.TransferItemId, plan.Count,
            out var fresh, out _, out _, out _);
        return blocker is null && fresh!.AssemblerId == binding.AssemblerId && fresh.InputIndex == binding.InputIndex
            && ReferenceEquals(fresh.RecipeData, binding.RecipeData) ? null : Stale(blocker ?? "assembler_input_component_changed");
    }

    private static void ExecuteAssemblerInputOnMainThread(ActionRecord action)
    {
        var plan = action.Plan; var binding = plan.AssemblerInput!; var player = GameMain.mainPlayer;
        ref var assembler = ref binding.Assemblers[binding.AssemblerId];
        var beforeAssembler = assembler; var beforeServed = (int[])assembler.served.Clone();
        var beforeInc = (int[])assembler.incServed.Clone(); var beforeProduced = (int[])assembler.produced.Clone();
        var beforePackage = ProductionResetPackage(binding.Package);
        var removed = 0; var inc = 0;
        while (removed < plan.Count)
        {
            var part = Math.Min(25, plan.Count - removed);
            var taken = binding.Package.TakeItem(plan.TransferItemId, part, out var partInc);
            if (taken != part || partInc < 0 || partInc > byte.MaxValue)
                throw new InvalidOperationException("assembler_input_exact_package_debit_unproved");
            var inserted = binding.Factory.InsertInto(plan.TransferStorageEntityId, 0, plan.TransferItemId,
                (byte)taken, (byte)partInc, out var remainingInc);
            if (inserted != taken || remainingInc != 0)
                throw new InvalidOperationException("assembler_input_native_insertion_unproved");
            removed += taken; inc += partInc;
        }
        var afterPackage = ProductionResetPackage(binding.Package);
        var expectedPackage = beforePackage.ToDictionary(row => row.Key, row => row.Value);
        var old = expectedPackage[plan.TransferItemId];
        if (inc > old.Inc || old.Count == removed && old.Inc != inc)
            throw new InvalidOperationException("assembler_input_package_inc_conservation_unproved");
        if (old.Count == removed) expectedPackage.Remove(plan.TransferItemId);
        else expectedPackage[plan.TransferItemId] = (old.Count - removed, old.Inc - inc);
        var unchangedPackage = expectedPackage.Count == afterPackage.Count && expectedPackage.All(row =>
            afterPackage.TryGetValue(row.Key, out var value) && value == row.Value);
        var inputIndex = binding.InputIndex;
        var unchangedBuffers = AssemblerInputPolicy.BuffersConserved(beforeServed, assembler.served,
            beforeInc, assembler.incServed, beforeProduced, assembler.produced, inputIndex, removed, inc);
        if (!assembler.Equals(beforeAssembler) || !unchangedPackage || !unchangedBuffers)
            throw new InvalidOperationException("assembler_input_native_conservation_unproved");
        action.TargetObjectId = plan.TransferStorageEntityId; action.TargetItemId = plan.TransferItemId;
        action.BeforeTargetAmount = beforeServed[inputIndex]; action.AfterTargetAmount = assembler.served[inputIndex];
        afterPackage.TryGetValue(plan.TransferItemId, out var packageAfter);
        action.AssemblerInputReadback = new() { EntityId = plan.TransferStorageEntityId, RecipeId = assembler.recipeId,
            ItemId = plan.TransferItemId, RequestedCount = removed,
            InputCountBefore = beforeServed[inputIndex], InputCountAfter = assembler.served[inputIndex],
            InputIncBefore = beforeInc[inputIndex], InputIncAfter = assembler.incServed[inputIndex],
            PackageCountBefore = old.Count, PackageCountAfter = packageAfter.Count,
            PackageIncBefore = old.Inc, PackageIncAfter = packageAfter.Inc,
            ExactNativeBindingVerified = true, OtherBuffersPreserved = true, OtherPackageStockPreserved = true,
            ConservationVerified = true, CapturedAtGameTick = GameMain.gameTick };
        action.AfterInventory = CaptureInventory(player); action.CompletedAtGameTick = GameMain.gameTick;
        action.AfterStateHash = CanonicalStateHash.Combine("assembler-input-completed-v1", plan.TransferStorageEntityId,
            plan.TransferItemId, action.BeforeTargetAmount, action.AfterTargetAmount, inc);
        action.Message = "Normal package removal and Native assembler insertion conserved the exact item count and proliferator points; later processing remains Native simulation.";
        action.State = NormalActionStates.Completed; action.Terminal = true; action.Succeeded = true;
    }
}
