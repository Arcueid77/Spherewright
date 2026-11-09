using Spherewright.Contracts.Actions;

namespace Spherewright.Bridge.Core.Factory;

public static class NativeBeltPickupPolicy
{
    public static bool HasMatchingEcho(int entityId, int itemId, int count, PreparedNormalAction? response)
    {
        var plan = response?.BeltCargoPickup;
        return itemId == 0 && count == 0 && response is { Prepared: true, ActionKind: NormalActionKinds.Transfer }
            && !string.IsNullOrEmpty(response.PlanToken) && plan is { EvidenceVersion: 1, PathId: > 0 }
            && plan.SelectedObjectId == entityId && plan.NativeOperation == "CargoTraffic.PickupBeltItems"
            && plan.BeltObjectIds is { Count: > 0 and <= 64 } && plan.BeltObjectIds.Contains(entityId)
            && plan.BeltObjectIds.All(id => id > 0) && plan.BeltObjectIds.Distinct().Count() == plan.BeltObjectIds.Count
            && plan.InserterObjectIds is { Count: <= 64 } && plan.InserterObjectIds.All(id => id > 0)
            && plan.InserterObjectIds.Distinct().Count() == plan.InserterObjectIds.Count
            && plan.Items is { Count: > 0 and <= 64 } && plan.Items.All(item => item is not null && item.ItemId > 0 && item.Count > 0 && item.Inc == 0)
            && plan.Items.Select(item => item.ItemId).Distinct().Count() == plan.Items.Count
            && response.ItemBudget is not null && response.ItemBudget.Count == plan.Items.Count
            && plan.Items.All(item => response.ItemBudget.Count(budget => budget is not null && budget.ItemId == item.ItemId
                && budget.Count == item.Count && budget.Direction == TransferDirections.BeltCargoToPlayer) == 1);
    }
}
