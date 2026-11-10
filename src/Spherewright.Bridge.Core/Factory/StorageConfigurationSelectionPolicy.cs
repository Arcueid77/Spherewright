using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Factory;

public static class StorageConfigurationSelectionPolicy
{
    public static int ResolveTarget(FactoryEntitySnapshot selected)
    {
        var stack = selected.Stack;
        if (stack is not { Complete: true, State: "observed", EvidenceVersion: 1 }
            || stack.Layers is null || stack.Attachments is null
            || stack.SelectedObjectId != selected.ObjectId || !ValidIds(stack.Layers.Select(x => x.ObjectId).ToList())
            || stack.RootObjectId != stack.Layers[0].ObjectId
            || stack.TopObjectId != stack.Layers[stack.Layers.Count - 1].ObjectId) return 0;
        if (selected.ItemId is 2101 or 2102)
            return stack.Layers.Count(x => x.ObjectId == selected.ObjectId && x.ItemId == selected.ItemId) == 1
                ? selected.ObjectId : 0;
        if (selected.ItemId != 2107) return 0;
        var attachments = stack.Attachments.Where(x => x.Entity.ObjectId == selected.ObjectId && x.Entity.ItemId == 2107).ToList();
        if (attachments.Count != 1) return 0;
        var parent = attachments[0].ParentObjectId;
        return stack.Layers.Count(x => x.ObjectId == parent && x.ItemId is 2101 or 2102) == 1 ? parent : 0;
    }

    public static bool ConfirmsEcho(PrepareConfigureBuildingRequest request, PreparedNormalAction? response)
    {
        var plan = response?.NativeStorageConfiguration;
        return response is { Prepared: true, ActionKind: NormalActionKinds.ConfigureBuilding }
            && !string.IsNullOrEmpty(response.PlanToken)
            && response.PlannedStorageOperation == request.StorageOperation && response.PlannedStorageConfiguration is not null
            && plan is { EvidenceVersion: 1, StorageItemId: 2101 or 2102, PreservesOtherLayers: true }
            && plan.SelectedObjectId == request.EntityId && plan.TargetObjectId == response.TargetObjectId
            && ValidIds(plan.LayerObjectIds) && plan.RootObjectId == plan.LayerObjectIds[0]
            && plan.LayerObjectIds.Contains(plan.TargetObjectId)
            && plan.AttachmentObjectIds is { Count: <= NativeStackingPolicy.MaximumObservedLayers }
            && plan.AttachmentObjectIds.All(id => id > 0 && !plan.LayerObjectIds.Contains(id))
            && plan.AttachmentObjectIds.Distinct().Count() == plan.AttachmentObjectIds.Count
            && (plan.SelectedObjectId == plan.TargetObjectId || plan.AttachmentObjectIds.Contains(plan.SelectedObjectId));
    }

    private static bool ValidIds(List<int>? ids) => ids is { Count: > 0 and <= NativeStackingPolicy.MaximumObservedLayers }
        && ids.All(id => id > 0) && ids.Distinct().Count() == ids.Count;
}
