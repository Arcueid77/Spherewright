using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Logistics;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Progression;
using Spherewright.Contracts.Resources;

namespace Spherewright.Bridge.Core.Safety;

public static class CanonicalStateHash
{
    public const int Version = 1;

    public static string Player(PlayerStateSnapshot snapshot)
    {
        var value = new StringBuilder();
        Append(value, "player-v1", snapshot.SessionId, snapshot.PlanetId, snapshot.IsAlive, snapshot.IsOnPlanet,
            snapshot.MovementState, F(snapshot.Position.X), F(snapshot.Position.Y), F(snapshot.Position.Z),
            F(snapshot.CoreEnergy), F(snapshot.CoreEnergyCapacity), snapshot.InventorySlotCount,
            snapshot.InventoryOccupiedSlotCount, F(snapshot.ReactorEnergy), snapshot.ReactorItemId,
            snapshot.ReactorItemInc, snapshot.AutoReplenishFuel, snapshot.FuelStorageSlotCount,
            snapshot.FuelStorageOccupiedSlotCount);
        foreach (var item in snapshot.Inventory.OrderBy(item => item.ItemId))
        {
            Append(value, "inventory", item.ItemId, item.Count, item.Inc, item.SlotCount);
        }

        foreach (var item in snapshot.FuelStorage.OrderBy(item => item.ItemId))
        {
            Append(value, "fuel", item.ItemId, item.Count, item.Inc, item.SlotCount);
        }

        if (snapshot.InHandItem is not null)
        {
            Append(value, "hand", snapshot.InHandItem.ItemId, snapshot.InHandItem.Count,
                snapshot.InHandItem.Inc, snapshot.InHandItem.SlotCount);
        }

        foreach (var task in snapshot.HandcraftQueue.OrderBy(task => task.QueueIndex))
        {
            Append(value, "forge", task.QueueIndex, task.RecipeId, task.RemainingCraftCount,
                task.Progress, task.ProgressRequired, task.ParentTaskIndex, task.IngredientsReserved);
            foreach (var item in task.Inputs.OrderBy(item => item.ItemId))
            {
                Append(value, "forge-in", item.ItemId, item.Count, item.BufferedCount);
            }

            foreach (var item in task.Outputs.OrderBy(item => item.ItemId))
            {
                Append(value, "forge-out", item.ItemId, item.Count, item.BufferedCount);
            }
        }

        Append(value, "drones", snapshot.ConstructionDrones.Enabled,
            snapshot.ConstructionDrones.ConstructionEnabled, snapshot.ConstructionDrones.Total,
            snapshot.ConstructionDrones.Alive, snapshot.ConstructionDrones.Idle,
            snapshot.ConstructionDrones.PendingBuildTargets, snapshot.ConstructionDrones.PendingRepairTargets);
        return Hash(value);
    }

    public static string PlayerAction(PlayerStateSnapshot snapshot) =>
        PlayerActionAtObservedPosition(snapshot, snapshot.Position);

    // This projection is not admission by itself. The passive-drift Move guard
    // supplies a server-recorded position and separately bounds age, speed and
    // displacement; every other PlayerAction field stays bound to the same hash.
    public static string PlayerActionAtObservedPosition(
        PlayerStateSnapshot snapshot,
        Vector3Snapshot observedPosition)
    {
        var value = new StringBuilder();
        Append(value, "player-action-v1", snapshot.SessionId, snapshot.PlanetId,
            snapshot.IsAlive, snapshot.IsOnPlanet, snapshot.MovementState,
            Q(observedPosition.X), Q(observedPosition.Y), Q(observedPosition.Z),
            snapshot.CoreEnergy > 0d, snapshot.CoreEnergyCapacity > 0d,
            snapshot.InventorySlotCount, snapshot.InventoryOccupiedSlotCount,
            snapshot.ReactorEnergy > 0d, snapshot.ReactorItemId, snapshot.ReactorItemInc,
            snapshot.AutoReplenishFuel, snapshot.FuelStorageSlotCount,
            snapshot.FuelStorageOccupiedSlotCount);
        foreach (var item in snapshot.Inventory.OrderBy(item => item.ItemId))
        {
            Append(value, "inventory", item.ItemId, item.Count, item.Inc, item.SlotCount);
        }

        foreach (var item in snapshot.FuelStorage.OrderBy(item => item.ItemId))
        {
            Append(value, "fuel", item.ItemId, item.Count, item.Inc, item.SlotCount);
        }

        if (snapshot.InHandItem is not null)
        {
            Append(value, "hand", snapshot.InHandItem.ItemId, snapshot.InHandItem.Count,
                snapshot.InHandItem.Inc, snapshot.InHandItem.SlotCount);
        }

        foreach (var task in snapshot.HandcraftQueue.OrderBy(task => task.QueueIndex))
        {
            Append(value, "forge", task.QueueIndex, task.RecipeId, task.RemainingCraftCount,
                task.Progress, task.ProgressRequired, task.ParentTaskIndex, task.IngredientsReserved);
        }

        Append(value, "drones", snapshot.ConstructionDrones.Enabled,
            snapshot.ConstructionDrones.ConstructionEnabled, snapshot.ConstructionDrones.Total,
            snapshot.ConstructionDrones.Alive, snapshot.ConstructionDrones.Idle,
            snapshot.ConstructionDrones.PendingBuildTargets, snapshot.ConstructionDrones.PendingRepairTargets);
        return Hash(value);
    }

    public static string Resource(ResourceNodeSnapshot snapshot)
    {
        var value = new StringBuilder();
        Append(value, "resource-v1", snapshot.SessionId, snapshot.PlanetId, snapshot.Kind,
            snapshot.NodeId, snapshot.ResourceType, snapshot.ProtoId, snapshot.RemainingAmount,
            snapshot.GroupIndex, snapshot.MinerCount, F(snapshot.Position.X), F(snapshot.Position.Y),
            F(snapshot.Position.Z));
        foreach (var yield in snapshot.Yields.OrderBy(item => item.ItemId))
        {
            Append(value, "yield", yield.ItemId, yield.Count, F(yield.Chance));
        }

        return Hash(value);
    }

    public static string Progression(ProgressionStateSnapshot snapshot)
    {
        var value = new StringBuilder();
        Append(value, "progression-v1", snapshot.SessionId, snapshot.PlanetId, snapshot.CurrentTechId);
        foreach (var queued in snapshot.TechQueue)
        {
            Append(value, "queue", queued);
        }

        foreach (var tech in snapshot.Technologies.OrderBy(tech => tech.TechId))
        {
            Append(value, "tech", tech.TechId, tech.Unlocked, tech.CurrentLevel, tech.MaximumLevel,
                tech.HashUploaded, tech.HashRequired, tech.IsLabTech, tech.IsQueued);
        }

        return Hash(value);
    }

    public static string Factory(FactoryEntitySnapshot snapshot)
    {
        var value = new StringBuilder();
        Append(value, "factory-v1", snapshot.SessionId, snapshot.PlanetId, snapshot.ObjectId,
            snapshot.ObjectKind, snapshot.ItemId, snapshot.ComponentKind, F(snapshot.Position.X),
            F(snapshot.Position.Y), F(snapshot.Position.Z), snapshot.RecipeId, snapshot.IsWorking,
            snapshot.Progress, snapshot.ProgressRequired, snapshot.PowerNetworkId,
            snapshot.PickTargetObjectId, snapshot.InsertTargetObjectId,
            snapshot.FilterItemId, snapshot.InserterStage, snapshot.InserterStackCount,
            snapshot.RequiredBuildItemCount, snapshot.ConstructionProgress.HasValue ? F(snapshot.ConstructionProgress.Value) : string.Empty);
        foreach (var connection in snapshot.Connections.OrderBy(connection => connection.Slot))
        {
            Append(value, "connection", connection.Slot, connection.IsOutput,
                connection.OtherObjectId, connection.OtherSlot);
        }

        foreach (var buffer in snapshot.Buffers.OrderBy(buffer => buffer.Role, StringComparer.Ordinal).ThenBy(buffer => buffer.ItemId))
        {
            Append(value, "buffer", buffer.Role, buffer.ItemId, buffer.Count, buffer.Inc);
        }

        foreach (var nodeId in snapshot.ResourceNodeIds.OrderBy(id => id))
        {
            Append(value, "node", nodeId);
        }

        AppendStorageConfiguration(value, snapshot.StorageConfiguration);
        AppendBlueprintSettings(value, snapshot);
        return Hash(value);
    }

    public static string FactoryEndpoint(FactoryEntitySnapshot snapshot)
    {
        var value = new StringBuilder();
        Append(value, "factory-endpoint-v1", snapshot.SessionId, snapshot.PlanetId,
            snapshot.ObjectId, snapshot.ObjectKind, snapshot.ItemId, snapshot.ComponentKind,
            F(snapshot.Position.X), F(snapshot.Position.Y), F(snapshot.Position.Z),
            F(snapshot.Rotation.X), F(snapshot.Rotation.Y), F(snapshot.Rotation.Z),
            F(snapshot.Rotation.W));
        foreach (var connection in snapshot.Connections.OrderBy(connection => connection.Slot))
        {
            Append(value, "connection", connection.Slot, connection.IsOutput,
                connection.OtherObjectId, connection.OtherSlot);
        }

        AppendStorageConfiguration(value, snapshot.StorageConfiguration);
        AppendBlueprintSettings(value, snapshot);
        return Hash(value);
    }

    public static string FactoryConfiguration(FactoryEntitySnapshot snapshot)
    {
        var value = new StringBuilder();
        Append(value, "factory-configuration-v1", snapshot.SessionId, snapshot.PlanetId,
            snapshot.ObjectId, snapshot.ObjectKind, snapshot.ItemId, snapshot.ComponentKind,
            F(snapshot.Position.X), F(snapshot.Position.Y), F(snapshot.Position.Z),
            F(snapshot.Rotation.X), F(snapshot.Rotation.Y), F(snapshot.Rotation.Z),
            F(snapshot.Rotation.W), snapshot.RecipeId, snapshot.PickTargetObjectId,
            snapshot.InsertTargetObjectId, snapshot.FilterItemId, snapshot.InserterStackCount);
        foreach (var connection in snapshot.Connections.OrderBy(connection => connection.Slot))
        {
            Append(value, "connection", connection.Slot, connection.IsOutput,
                connection.OtherObjectId, connection.OtherSlot);
        }

        foreach (var buffer in snapshot.Buffers
                     .OrderBy(buffer => buffer.Role, StringComparer.Ordinal)
                     .ThenBy(buffer => buffer.ItemId))
        {
            Append(value, "buffer", buffer.Role, buffer.ItemId, buffer.Count, buffer.Inc);
        }

        AppendStorageConfiguration(value, snapshot.StorageConfiguration);
        AppendBlueprintSettings(value, snapshot);
        return Hash(value);
    }

    private static void AppendBlueprintSettings(StringBuilder value, FactoryEntitySnapshot snapshot)
    {
        if (snapshot.ForceAccelerationMode.HasValue)
            Append(value, "assembler-acceleration-v1", snapshot.ForceAccelerationMode.Value);
        if (snapshot.ComponentKind == "inserter")
            Append(value, "inserter-filter-v1", snapshot.FilterItemId.GetValueOrDefault());
    }

    private static void AppendStorageConfiguration(StringBuilder value, StorageConfigurationSnapshot? storage)
    {
        if (storage is null) return; // Preserve the existing non-storage encoding.
        Append(value, "storage-configuration-v1", storage.GridCount, storage.BannedGridCount, storage.Mode);
        for (var i = 0; i < storage.GridFilterItemIds.Count; i++)
            Append(value, "storage-filter", i, storage.GridFilterItemIds[i]);
    }

    public static string ProgressionSelection(ProgressionStateSnapshot snapshot)
    {
        var value = new StringBuilder();
        Append(value, "progression-selection-v1", snapshot.SessionId, snapshot.PlanetId, snapshot.CurrentTechId);
        foreach (var queued in snapshot.TechQueue)
        {
            Append(value, "queue", queued);
        }

        foreach (var tech in snapshot.Technologies.OrderBy(tech => tech.TechId))
        {
            Append(value, "tech", tech.TechId, tech.Unlocked, tech.CurrentLevel, tech.MaximumLevel,
                tech.HashRequired, tech.IsLabTech, tech.IsQueued);
            foreach (var prerequisiteTechId in tech.PrerequisiteTechIds.OrderBy(id => id))
            {
                Append(value, "prerequisite", tech.TechId, prerequisiteTechId);
            }
        }

        return Hash(value);
    }

    public static string LogisticsStation(LogisticsStationSnapshot snapshot)
    {
        var value = new StringBuilder();
        AppendLogisticsStationConfiguration(value, snapshot, "logistics-station-v1");
        Append(value, "live", snapshot.PowerServeRatio, snapshot.Energy,
            snapshot.RequestedChargeEnergyPerTick, snapshot.RequestedChargePowerWatts,
            snapshot.WarperCount, snapshot.IdleDroneCount, snapshot.WorkingDroneCount,
            snapshot.IdleVesselCount, snapshot.WorkingVesselCount);
        foreach (var slot in snapshot.StorageSlots.OrderBy(slot => slot.Index))
        {
            Append(value, "storage-live", slot.Index, slot.Count, slot.Inc, slot.LocalOrder,
                slot.RemoteOrder, slot.TotalOrdered, slot.LocalSupplyCount, slot.LocalDemandCount,
                slot.RemoteSupplyCount, slot.RemoteDemandCount);
        }

        foreach (var itemId in snapshot.NeededItemIds.OrderBy(itemId => itemId))
        {
            Append(value, "need", itemId);
        }

        foreach (var slot in snapshot.BeltSlots.OrderBy(slot => slot.Index))
        {
            Append(value, "belt-live", slot.Index, slot.Counter);
        }

        return Hash(value);
    }

    public static string LogisticsStationConfiguration(LogisticsStationSnapshot snapshot)
    {
        var value = new StringBuilder();
        AppendLogisticsStationConfiguration(value, snapshot, "logistics-station-config-v1");
        return Hash(value);
    }

    public static string LogisticsStationFleet(LogisticsStationSnapshot snapshot)
    {
        var value = new StringBuilder();
        Append(value, "logistics-station-fleet-v1", snapshot.SessionId, snapshot.PlanetId,
            snapshot.EntityId, snapshot.StationId, snapshot.GalacticStationId,
            snapshot.BuildingItemId, F(snapshot.Position.X), F(snapshot.Position.Y),
            F(snapshot.Position.Z), snapshot.IsInterstellar, snapshot.IsCollector,
            snapshot.IsVeinCollector, snapshot.DroneCapacity, snapshot.VesselCapacity,
            snapshot.IdleDroneCount, snapshot.WorkingDroneCount,
            snapshot.IdleVesselCount, snapshot.WorkingVesselCount,
            snapshot.WarperCount, snapshot.WarperCapacity,
            snapshot.DroneAutoReplenish, snapshot.VesselAutoReplenish);
        return Hash(value);
    }

    private static void AppendLogisticsStationConfiguration(
        StringBuilder value,
        LogisticsStationSnapshot snapshot,
        string domain)
    {
        Append(value, domain, snapshot.SessionId, snapshot.PlanetId, snapshot.EntityId,
            snapshot.StationId, snapshot.GalacticStationId, snapshot.BuildingItemId,
            F(snapshot.Position.X), F(snapshot.Position.Y), F(snapshot.Position.Z),
            snapshot.IsInterstellar, snapshot.IsCollector, snapshot.IsVeinCollector,
            snapshot.PowerNetworkId, snapshot.EnergyCapacity,
            snapshot.MaximumChargeEnergyPerTick, snapshot.MaximumChargePowerWatts,
            snapshot.WarperCapacity, F(snapshot.DroneTripRangeRaw), F(snapshot.VesselTripRangeRaw),
            snapshot.IncludeOrbitCollectors, F(snapshot.WarpEnableDistanceRaw), snapshot.WarpersRequired,
            snapshot.DroneDeliverySetting, snapshot.VesselDeliverySetting, snapshot.PilerCount,
            snapshot.DroneAutoReplenish, snapshot.VesselAutoReplenish, snapshot.RemoteGroupMask,
            snapshot.RemoteRoutePriority);
        foreach (var slot in snapshot.StorageSlots.OrderBy(slot => slot.Index))
        {
            Append(value, "storage-config", slot.Index, slot.ItemId, slot.MaximumCount,
                slot.LocalLogic, slot.RemoteLogic, slot.KeepMode, F(slot.KeepIncRatio));
        }

        foreach (var slot in snapshot.BeltSlots.OrderBy(slot => slot.Index))
        {
            Append(value, "belt-slot", slot.Index, slot.Direction, slot.BeltComponentId,
                slot.BeltEntityId, slot.StorageIndex);
        }
    }

    public static string Combine(string actionKind, params object?[] fields)
    {
        var value = new StringBuilder();
        Append(value, "action-v1", actionKind);
        Append(value, fields);
        return Hash(value);
    }

    private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Q(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static void Append(StringBuilder builder, params object?[] fields)
    {
        foreach (var field in fields)
        {
            var text = field switch
            {
                null => string.Empty,
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => field.ToString() ?? string.Empty,
            };
            builder.Append(text.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(text);
            builder.Append('|');
        }
    }

    private static string Hash(StringBuilder canonical)
    {
        using var sha256 = SHA256.Create();
        var digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
        var hex = new StringBuilder(digest.Length * 2);
        foreach (var value in digest)
        {
            hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return "sha256:" + hex;
    }
}
