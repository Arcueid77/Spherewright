using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private sealed class GeneratorColdStartBinding
    {
        internal PlanetFactory Factory = null!;
        internal StorageComponent Package = null!;
        internal EntityData[] Entities = null!;
        internal PowerGeneratorComponent[] Generators = null!;
        internal int GeneratorId;
    }
    private sealed partial class NormalActionPlanPayload
    {
        public GeneratorColdStartBinding? GeneratorColdStart { get; set; }
    }

    private static string? CaptureGeneratorColdStart(Player p, PlanetFactory f, int entityId, int itemId, int count,
        out GeneratorColdStartBinding? binding, out int buildingItemId)
    {
        binding = null; buildingItemId = 0;
        if (p.package?.grids is null || !ReferenceEquals(p.factory, f) || ReferenceEquals(p.package, p.mecha.reactorStorage)
            || f.entityPool is null || entityId <= 0 || entityId >= f.entityCursor
            || entityId >= f.entityPool.Length || f.powerSystem?.genPool is null) return "generator_cold_start_component_unavailable";
        ref var e = ref f.entityPool[entityId]; var id = e.powerGenId; var system = f.powerSystem;
        if (e.id != entityId || id <= 0 || id >= system.genCursor || id >= system.genPool.Length)
            return "generator_cold_start_component_unavailable";
        ref var g = ref system.genPool[id]; var fuel = LDB.items.Select(itemId); var desc = LDB.items.Select(e.protoId)?.prefabDesc;
        var admitted = g.fuelMask > 0 && g.fuelMask < ItemProto.fuelNeeds.Length
            && ItemProto.fuelNeeds[g.fuelMask]?.Contains(itemId) == true && fuel is not null && fuel.HeatValue > 0;
        var facts = new GeneratorColdStartFuelFacts
        {
            BuildingItemId = buildingItemId = e.protoId, RequestedCount = count, RequestedFuelItemId = itemId,
            PackageCount = p.package.GetItemCount(itemId), NativeFuelAdmitted = admitted,
            ExactNativeComponent = g.id == id && g.entityId == entityId && g.useFuelPerTick > 0 && g.genEnergyPerTick > 0
                && desc is { isPowerGen: true } && desc.fuelMask == g.fuelMask
                && !g.photovoltaic && !g.wind && !g.gamma && !g.geothermal,
            ZeroIncPackageFuel = p.package.grids.Where(x => x.itemId == itemId).All(x => x.inc == 0),
            EmptyPlayerHand = p.inhandItemId == 0 && p.inhandItemCount == 0 && p.inhandItemInc == 0,
            WithinBuildArea = Vector3.Distance(p.position, e.pos) <= p.mecha.buildArea,
            BufferedFuelId = g.fuelId, BufferedFuelCount = g.fuelCount, BufferedFuelInc = g.fuelInc, BufferedFuelHeat = g.fuelHeat,
            CurrentFuelId = g.curFuelId, LoadedFuelEnergy = g.fuelEnergy,
            CapacityEnergyPerTick = g.capacityCurrentTick, GeneratedEnergyPerTick = g.generateCurrentTick,
        };
        if (GeneratorColdStartFuelPolicy.Blocker(facts) is { } blocked) return blocked;
        binding = new() { Factory = f, Package = p.package, Entities = f.entityPool, Generators = system.genPool, GeneratorId = id };
        return null;
    }

    private GameCallResult<PreparedNormalAction> PrepareGeneratorColdStartFuelOnMainThread(string? sessionId, PrepareTransferRequest r)
    {
        var common = ValidatePrepareCommon(sessionId, r.PlanetId, r.StateHashVersion);
        if (common.Error is not null) return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        if (r.ItemId <= 0 || r.Count < 1 || r.Count > 10 || r.Direction != TransferDirections.PlayerToGeneratorFuel)
            return InvalidPlan("Cold-start fuel requires player-to-generator-fuel and an exact count1..10.");
        var player = _reader.GetPlayerStateOnMainThread(sessionId, new LocalPlanetRequest { PlanetId = r.PlanetId });
        var entity = _reader.InspectFactoryEntityOnMainThread(sessionId, new InspectFactoryEntityRequest { PlanetId = r.PlanetId, ObjectId = r.StorageEntityId });
        if (!player.Success || player.Value is null) return GameCallResult<PreparedNormalAction>.Failed(player.Error!);
        if (!entity.Success || entity.Value is null) return GameCallResult<PreparedNormalAction>.Failed(entity.Error!);
        if (player.Value.StateHash != r.ExpectedPlayerStateHash || entity.Value.StateHash != r.ExpectedStorageStateHash)
            return StalePlan("Player or exact cold generator changed after inspection.");
        var f = GameMain.localPlanet?.factory; var p = GameMain.mainPlayer;
        if (f is null || p is null) return NotReadyPlan("The current owned factory/player is unavailable.");
        var blocker = CaptureGeneratorColdStart(p, f, r.StorageEntityId, r.ItemId, r.Count, out var binding, out var item);
        if (blocker is not null) return StalePlan(blocker);
        var playerHash = CanonicalStateHash.PlayerAction(player.Value);
        var hash = CanonicalStateHash.Combine("generator-cold-start-fuel-v1", _sessions.SessionId, r.PlanetId,
            playerHash, entity.Value.StateHash, r.StorageEntityId, r.Direction, r.ItemId, r.Count);
        var payload = NormalActionPlanPayload.Transfer(_sessions.SessionId!, r.PlanetId, hash, playerHash,
            entity.Value.StateHash, r.StorageEntityId, r.Direction, r.ItemId, r.Count);
        payload.GeneratorColdStart = binding;
        var prepared = AddPreparedPlan(payload, common.Session!, 1,
            "Normal package debit and Native generator insertion prove equal-and-opposite fuel counts at the same tick; no heat or generation is injected.");
        if (prepared.Success && prepared.Value is { } plan)
        {
            plan.DestinationObjectId = r.StorageEntityId;
            plan.GeneratorColdStartFuel = new() { EvidenceVersion = GeneratorColdStartFuelPolicy.EvidenceVersion,
                EntityId = r.StorageEntityId, BuildingItemId = item, ItemId = r.ItemId, Count = r.Count,
                EmptyUnlitGenerator = true, NativeOperation = GeneratorColdStartFuelPolicy.NativeOperation };
            plan.ItemBudget.Add(new() { ItemId = r.ItemId, Count = r.Count, Direction = r.Direction });
        }
        return prepared;
    }

    private BridgeError? RevalidateGeneratorColdStartFuelOnMainThread(NormalActionPlanPayload plan)
    {
        var b = plan.GeneratorColdStart; var f = GameMain.localPlanet?.factory; var p = GameMain.mainPlayer;
        if (b is null || f is null || p is null || !ReferenceEquals(f, b.Factory) || !ReferenceEquals(p.package, b.Package)
            || !ReferenceEquals(f.entityPool, b.Entities) || !ReferenceEquals(f.powerSystem?.genPool, b.Generators))
            return Stale("generator_cold_start_original_native_binding_lost");
        var player = _reader.GetPlayerStateOnMainThread(plan.SessionId, new LocalPlanetRequest { PlanetId = plan.PlanetId });
        var entity = _reader.InspectFactoryEntityOnMainThread(plan.SessionId, new InspectFactoryEntityRequest { PlanetId = plan.PlanetId, ObjectId = plan.TransferStorageEntityId });
        if (!player.Success || player.Value is null) return player.Error;
        if (!entity.Success || entity.Value is null) return entity.Error;
        if (CanonicalStateHash.PlayerAction(player.Value) != plan.PlayerStateHash || entity.Value.StateHash != plan.TransferStorageStateHash)
            return Stale("generator_cold_start_inventory_or_generator_changed");
        var blocker = CaptureGeneratorColdStart(p, f, plan.TransferStorageEntityId, plan.TransferItemId, plan.Count, out var fresh, out _);
        return blocker is null && fresh!.GeneratorId == b.GeneratorId ? null : Stale(blocker ?? "generator_cold_start_component_changed");
    }

    private static void ExecuteGeneratorColdStartFuelOnMainThread(ActionRecord action)
    {
        var plan = action.Plan; var b = plan.GeneratorColdStart!; var p = GameMain.mainPlayer;
        var beforeGenerator = b.Generators[b.GeneratorId];
        var before = b.Package.GetItemCount(plan.TransferItemId);
        var removed = b.Package.TakeItem(plan.TransferItemId, plan.Count, out var inc);
        if (removed != plan.Count || inc != 0) throw new InvalidOperationException("generator_cold_start_exact_package_debit_unproved");
        // Same Native business entry used by normal inserter delivery. No direct
        // field writes, fuel/heat creation, generation call or simulation tick.
        var inserted = b.Factory.InsertInto(plan.TransferStorageEntityId, 0, plan.TransferItemId, (byte)removed, 0, out var remainderInc);
        var g = b.Generators[b.GeneratorId]; var after = b.Package.GetItemCount(plan.TransferItemId);
        // Normalize only the four fields changed by Native SetNewFuel on a
        // DETACHED value copy; every unrelated component field must match.
        var unaffected = g;
        unaffected.fuelId = beforeGenerator.fuelId; unaffected.fuelCount = beforeGenerator.fuelCount;
        unaffected.fuelInc = beforeGenerator.fuelInc; unaffected.fuelHeat = beforeGenerator.fuelHeat;
        if (inserted != removed || remainderInc != 0 || after != before - removed
            || g.fuelId != plan.TransferItemId || g.fuelCount != removed || g.fuelInc != 0
            || g.curFuelId != beforeGenerator.curFuelId || g.fuelEnergy != beforeGenerator.fuelEnergy
            || g.fuelHeat != LDB.items.Select(plan.TransferItemId)!.HeatValue
            || !unaffected.Equals(beforeGenerator))
            throw new InvalidOperationException("generator_cold_start_bilateral_native_conservation_unproved");
        action.TargetObjectId = plan.TransferStorageEntityId; action.TargetItemId = plan.TransferItemId;
        action.BeforeTargetAmount = 0; action.AfterTargetAmount = g.fuelCount;
        action.AfterInventory = CaptureInventory(p); action.CompletedAtGameTick = GameMain.gameTick;
        action.AfterStateHash = CanonicalStateHash.Combine("generator-cold-start-completed-v1", after, g.fuelId, g.fuelCount, g.fuelInc, g.fuelEnergy);
        action.Message = "Normal package debit and Native cold-generator insertion conserved the exact fuel count; subsequent burn/generation remains Native simulation.";
        action.State = NormalActionStates.Completed; action.Terminal = true; action.Succeeded = true;
    }
}
