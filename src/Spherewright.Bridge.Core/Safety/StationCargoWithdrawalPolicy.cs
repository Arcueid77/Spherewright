using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Logistics;

namespace Spherewright.Bridge.Core.Safety;

public static class StationCargoWithdrawalPolicy
{
    public const string NativeOperation = "StationComponent.TakeItem/StorageComponent.AddItemStacked";

    public static string? Blocker(LogisticsStationSnapshot? station, int entityId, int itemId, int count,
        bool exactNativeBinding, bool emptyHand, bool withinBuildArea, bool packageCanAccept, int packageItemInc)
    {
        if (!exactNativeBinding || station is null || station.EntityId != entityId || entityId < 1
            || station.BuildingItemId is not (2103 or 2104) || station.IsCollector || station.IsVeinCollector
            || station.IsInterstellar!=(station.BuildingItemId==2104))
            return "station_cargo_exact_local_binding_unproved";
        if (itemId < 1 || count < 1 || count > 100) return "station_cargo_count_outside_1_to_100";
        if (!emptyHand || !withinBuildArea || !packageCanAccept || packageItemInc != 0)
            return "station_cargo_package_hand_or_range_unproved";
        if(station.StorageSlots is null || station.StorageSlots.Count<1 || station.StorageSlots.Count>6
            || station.StorageSlots.Any(s=>s.Index<0 || s.Index>=station.StorageSlots.Count)
            || station.StorageSlots.Select(s=>s.Index).Distinct().Count()!=station.StorageSlots.Count)
            return "station_cargo_complete_storage_indices_unproved";
        var matches=station.StorageSlots.Where(s=>s.ItemId==itemId).ToArray();
        if (matches.Length != 1 || matches[0].Index < 0
            || station.StorageSlots.Count(s=>s.Index==matches[0].Index)!=1)
            return "station_cargo_unique_storage_slot_unproved";
        var slot=matches[0];
        if (slot.Inc != 0 || slot.Count < count || slot.MaximumCount < 1)
            return "station_cargo_physical_zero_inc_stock_insufficient";
        if (slot.LocalOrder != 0 || slot.RemoteOrder != 0 || slot.TotalOrdered != 0)
            return "station_cargo_selected_slot_has_pending_orders";
        // Passive unpowered stations still have physical stock. Network/energy
        // are not admission requirements and are never silently filled in.
        return null;
    }

    public static bool HasMatchingEcho(int entityId,int itemId,int count,PreparedNormalAction? prepared)
        => prepared is {Prepared:true,ActionKind:NormalActionKinds.Transfer,StationCargoWithdrawal:{ } p}
            && prepared.SourceObjectId==entityId && p.EvidenceVersion==1 && p.EntityId==entityId && p.StorageIndex>=0
            && p.ItemId==itemId && p.Count==count && p.NativeOperation==NativeOperation
            && prepared.ItemBudget.Count==1 && prepared.ItemBudget[0].ItemId==itemId
            && prepared.ItemBudget[0].Count==count
            && prepared.ItemBudget[0].Direction==TransferDirections.StationStorageToPlayer;
}
