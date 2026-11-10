using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Factory;

public static class NativeBeltInsertionPolicy
{
    public static bool IsEmptyIndependentPath(int selectedId, MaterialCargoPathSnapshot? path) =>
        path is { PathId: > 0, PathLengthCells: > 0 and <= 4096, PathClosed: false,
            OutputPathId: 0, ItemCount: 0, CargoStackCount: 0 }
        && path.InputPathIds is not null && path.InputPathIds.All(id => id == 0)
        && path.Items is { Count: 0 } && ValidMembers(selectedId, path.BeltObjectIds);

    public static bool HasMatchingEcho(int entityId, int itemId, int count, PreparedNormalAction? response)
    {
        var plan = response?.BeltCargoInsertion;
        return itemId > 0 && count == 1
            && response is { Prepared: true, ActionKind: NormalActionKinds.Transfer }
            && !string.IsNullOrEmpty(response.PlanToken)
            && plan is { EvidenceVersion: 1, PathId: > 0, PathLengthCells: > 0 and <= 4096,
                Count: 1, Inc: 0, EmptyIndependentPath: true, NativeOperation: "CargoTraffic.PutItemOnBelt" }
            && plan.SelectedObjectId == entityId && plan.ItemId == itemId
            && ValidMembers(entityId, plan.BeltObjectIds)
            && response.ItemBudget is { Count: 1 } && response.ItemBudget[0] is { } budget
            && budget.ItemId == itemId && budget.Count == 1 && budget.Direction == TransferDirections.PlayerToBeltCargo;
    }

    private static bool ValidMembers(int selectedId, List<int>? ids) => selectedId > 0
        && ids is { Count: > 0 and <= 64 } && ids.Contains(selectedId)
        && ids.All(id => id > 0) && ids.Distinct().Count() == ids.Count;
}
