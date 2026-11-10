using Spherewright.Contracts.Actions;

namespace Spherewright.Bridge.Core.Factory;

public static class BeltPathReversalPolicy
{
    public static bool MatchesLinearMember(int mainInput, int output, IEnumerable<int> inputs, int previous, int next) =>
        mainInput == previous && output == next
        && inputs.Where(id => id != 0).SequenceEqual(previous == 0 ? Array.Empty<int>() : new[] { previous });

    public static int PilerMode(int firstBelt, int secondBelt, bool firstOutput, bool secondOutput) =>
        firstBelt > 0 && secondBelt > 0 && firstOutput != secondOutput ? firstOutput ? 1 : 2 : 0;

    public static bool ConfirmsEcho(PrepareConfigureBuildingRequest request, PreparedNormalAction? response)
    {
        var plan = response?.BeltPathReversal;
        return response is { Prepared: true, ActionKind: NormalActionKinds.ConfigureBuilding }
            && !string.IsNullOrEmpty(response.PlanToken) && request.Mode == BuildingConfigurationModes.ReverseBeltPath
            && plan is { EvidenceVersion: 1, PathId: > 0, PathLengthCells: > 0 and <= 4096,
                PilerObjectId: > 0, EmptyIndependentPath: true, NativeOperation: "UIBeltWindow.OnReverseButtonClick" }
            && plan.SelectedObjectId == request.EntityId && response.TargetObjectId == request.EntityId
            && ValidIds(plan.BeltObjectIds) && plan.BeltObjectIds.Contains(request.EntityId)
            && ValidIds(plan.PilerLayerObjectIds) && plan.PilerLayerObjectIds.Contains(plan.PilerObjectId)
            && plan.SorterObjectIds is { Count: <= 64 } && plan.SorterObjectIds.All(id => id > 0)
            && plan.SorterObjectIds.Distinct().Count() == plan.SorterObjectIds.Count
            && !plan.BeltObjectIds.Intersect(plan.PilerLayerObjectIds.Concat(plan.SorterObjectIds)).Any();
    }

    private static bool ValidIds(List<int> ids) => ids is { Count: > 0 and <= 64 }
        && ids.All(id => id > 0) && ids.Distinct().Count() == ids.Count;
}
