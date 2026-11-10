using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Spherewright.Bridge.Core.Safety;

namespace Spherewright.Bridge.Core.Factory;

public static class NativeStackingPolicy
{
    public const int MaximumObservedLayers = 256;

    public static bool IsStorageAttachment(NativeStackConstructionPlan plan) =>
        plan.BuildingItemId == 2107 && plan.InputFromSlot == 13 && plan.InputToSlot == 0;

    public static string? PlacementToolKind(int nativeBuildMode, string family)
    {
        // DSP29151 dispatches item BuildMode5 through the Native Addon tool.
        if (family == "spraycoater") return nativeBuildMode == 5 ? "addon" : null;
        return nativeBuildMode == 1 ? "click" : null;
    }

    public static string? PlanHash(NativeStackConstructionPlan? plan)
    {
        if (plan is null) return null;
        ValidatePlan(plan);
        var original = CanonicalStateHash.Combine("native-stack-construction-v1", plan.EvidenceVersion,
            plan.SelectedObjectId, plan.RootObjectId, plan.AnchorObjectId, plan.BeforeStackStateHash,
            plan.BuildingItemId, plan.ModelIndex, plan.ExistingLayerCount, plan.MaximumLayers,
            plan.InputFromSlot, plan.InputToSlot,
            CanonicalStateHash.Combine("existing-stack-members", plan.ExistingObjectIds.Cast<object>().ToArray()));
        var placement = plan.BuildToolKind == "click" && plan.HeightLimitKind == "technology" ? original
            : CanonicalStateHash.Combine("native-stack-placement-v2", original, plan.BuildToolKind, plan.HeightLimitKind);
        return plan.ExistingAttachmentObjectIds.Count == 0 ? placement
            : CanonicalStateHash.Combine("native-stack-attachments-v1", placement,
                CanonicalStateHash.Combine("original-attachments", plan.ExistingAttachmentObjectIds.Cast<object>().ToArray()));
    }

    public static void ValidatePlan(NativeStackConstructionPlan plan)
    {
        var attachment = IsStorageAttachment(plan);
        if (plan.EvidenceVersion != 1 || plan.SelectedObjectId <= 0 || plan.RootObjectId <= 0
            || plan.AnchorObjectId <= 0 || plan.BuildingItemId <= 0 || plan.ModelIndex <= 0
            || plan.ExistingLayerCount < 1 || plan.ExistingLayerCount >= MaximumObservedLayers
            || plan.MaximumLayers.HasValue && plan.MaximumLayers < 1
            || plan.HeightLimitKind is not ("technology" or "native_reach")
            || plan.BuildToolKind is not ("click" or "addon")
            || plan.MaximumLayers.HasValue != (plan.HeightLimitKind == "technology")
            || plan.ExistingObjectIds is null
            || plan.ExistingObjectIds.Count != plan.ExistingLayerCount
            || plan.ExistingObjectIds.Any(id => id <= 0)
            || plan.ExistingObjectIds.Distinct().Count() != plan.ExistingLayerCount
            || plan.ExistingAttachmentObjectIds is null || plan.ExistingAttachmentObjectIds.Count > MaximumObservedLayers
            || plan.ExistingAttachmentObjectIds.Any(id => id <= 0 || plan.ExistingObjectIds.Contains(id))
            || plan.ExistingAttachmentObjectIds.Distinct().Count() != plan.ExistingAttachmentObjectIds.Count
            || plan.ExistingObjectIds[0] != plan.RootObjectId
            || plan.ExistingObjectIds[plan.ExistingLayerCount - 1] != plan.AnchorObjectId
            || string.IsNullOrEmpty(plan.BeforeStackStateHash)
            || !plan.BeforeStackStateHash.StartsWith("sha256:", StringComparison.Ordinal)
            || (attachment
                ? plan.BuildToolKind != "addon" || plan.HeightLimitKind != "native_reach"
                    || plan.ExistingAttachmentObjectIds.Count != 0
                : plan.BuildingItemId == 2107 || plan.InputFromSlot != 15 || plan.InputToSlot != 14))
            throw new InvalidDataException("Invalid native stack construction identity.");
    }

    public static bool ConfirmsCompletedMembers(NativeStackConstructionPlan plan, NativeStackSnapshot snapshot,
        out int createdObjectId)
    {
        createdObjectId = 0;
        var attachment = IsStorageAttachment(plan);
        if (!snapshot.Complete || snapshot.RootObjectId != plan.RootObjectId
            || snapshot.Layers.Count != plan.ExistingLayerCount + (attachment ? 0 : 1)
            || !snapshot.Layers.Take(plan.ExistingLayerCount).Select(layer => layer.ObjectId).SequenceEqual(plan.ExistingObjectIds))
            return false;
        if (attachment)
        {
            if (snapshot.TopObjectId != plan.AnchorObjectId || snapshot.Attachments.Count != 1) return false;
            var added = snapshot.Attachments[0];
            if (added.ParentObjectId != plan.AnchorObjectId || added.Entity.ItemId != plan.BuildingItemId
                || added.Entity.ModelIndex != plan.ModelIndex || plan.ExistingObjectIds.Contains(added.Entity.ObjectId))
                return false;
            createdObjectId = added.Entity.ObjectId;
        }
        else
        {
            if (!snapshot.Attachments.Select(value => value.Entity.ObjectId).OrderBy(id => id)
                .SequenceEqual(plan.ExistingAttachmentObjectIds.OrderBy(id => id))) return false;
            createdObjectId = snapshot.TopObjectId;
        }
        return createdObjectId > 0;
    }

    public static string? ValidateRequest(PrepareBuildRequest request)
    {
        if (request.PreferredModelIndex.HasValue && request.PreferredModelIndex <= 0)
            return "A preferred native model index must be positive.";
        if (request.StackOnObjectId is null)
            return string.IsNullOrEmpty(request.ExpectedStackStateHash)
                ? null : "A stack hash requires an explicit built stack anchor.";
        if (request.StackOnObjectId <= 0 || string.IsNullOrEmpty(request.ExpectedStackStateHash)
            || !request.ExpectedStackStateHash!.StartsWith("sha256:", StringComparison.Ordinal))
            return "Stack construction requires a positive built anchor and its current stack state hash.";
        if (request.PreferredPosition is not null || request.ResourceNodeId is not null
            || request.SourceObjectId is not null || request.DestinationObjectId is not null
            || request.PathEnd is not null || request.InitialSorterFilterItemId != 0
            || request.BeltEndpointPreview is not null || request.BeltRoutePreview is not null
            || request.BeltPathMode != BeltPathModes.NativeGrid
            || request.BeltStartAltitudeLevel is not null || request.BeltEndAltitudeLevel is not null)
            return "A stack anchor cannot be combined with ground, resource, sorter or belt placement arguments.";
        return null;
    }

    public static bool ConfirmsPlan(PrepareBuildRequest request, PreparedNormalAction? response)
    {
        if (request.StackOnObjectId is null) return true;
        var plan = response?.PlannedStackConstruction;
        if (plan is null) return false;
        try { ValidatePlan(plan); }
        catch (InvalidDataException) { return false; }
        return plan is { EvidenceVersion: 1, AnchorObjectId: > 0, RootObjectId: > 0, ModelIndex: > 0 }
            && IsStorageAttachment(plan) == (request.BuildingItemId == 2107)
            && plan.SelectedObjectId == request.StackOnObjectId
            && plan.BuildingItemId == request.BuildingItemId
            && plan.BeforeStackStateHash == request.ExpectedStackStateHash
            && (request.PreferredModelIndex is null || plan.ModelIndex == request.PreferredModelIndex)
            && plan.ExistingLayerCount > 0 && plan.ExistingLayerCount < MaximumObservedLayers
            && (!plan.MaximumLayers.HasValue || plan.MaximumLayers > 0);
    }
}
