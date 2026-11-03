using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private static bool HasSavedPaidWorkReceipt(BlueprintBuildState b,string journeyId,long tick)
    {
        if(b.StructuredConstruction is not {} binding || binding.JourneyId!=journeyId || b.Phase!="outcome_unknown"
            || b.Objects.Count<1 || b.Objects.Count!=b.Site.Objects.Count || b.LastEvidenceGameTick>tick
            || b.ConstructionControl is not {DispatchDisabled:true,OriginalDroneEnabled:true,StopReason:not null} c
            || c.NativeCreateActionId!=b.ActionId || !Guid.TryParse(c.NativeCreateActionId,out _)
            || !c.DrainStartedAtGameTick.HasValue || c.DrainStartedAtGameTick>tick || !c.DrainStartedAtUtc.HasValue
            || b.Objects.Any(o=>o.State!=BlueprintObjectStates.Completed && o.State!=BlueprintObjectStates.PendingConstruction)
            || b.Site.Objects.Any(o=>!StructuredConstructionBinding.SupportsItem(o.ItemId)))return false;
        var first=b.Objects[0];var count=b.Objects.Count;
        return first.InventoryBefore>=count && first.InventoryAfter==first.InventoryBefore-count
            && !string.IsNullOrEmpty(first.BeforeInventoryHash) && !string.IsNullOrEmpty(first.AfterInventoryHash)
            && b.Objects.All(o=>o.SubmittedAtGameTick.HasValue && o.SubmittedAtGameTick<=tick && o.PrebuildId>0
                && (o.State==BlueprintObjectStates.PendingConstruction && !o.EntityId.HasValue
                    || o.State==BlueprintObjectStates.Completed && o.EntityId>0 && o.CompletedAtGameTick<=tick
                        && !c.PreexistingEntityIds.Contains(o.EntityId.GetValueOrDefault()))
                && o.InventoryBefore==first.InventoryBefore && o.InventoryAfter==first.InventoryAfter
                && o.BeforeInventoryHash==first.BeforeInventoryHash && o.AfterInventoryHash==first.AfterInventoryHash);
    }
    private static bool HasSavedStoppedPaidBatchReceipt(BlueprintBuildState b,string journeyId,long tick)
    {
        if(b.StructuredConstruction is not { } binding || binding.JourneyId!=journeyId || b.Phase!="outcome_unknown"
            || b.Objects.Count<1 || b.Objects.Count!=b.Site.Objects.Count || b.LastEvidenceGameTick>tick
            || b.ConstructionControl is not {DispatchDisabled:true,OriginalDroneEnabled:true,BindingLost:false,
                StopReason:"construction_complete_draining"} c || c.NativeCreateActionId!=b.ActionId
            || !Guid.TryParse(c.NativeCreateActionId,out _) || !c.DrainStartedAtGameTick.HasValue
            || c.DrainStartedAtGameTick>tick || !c.DrainStartedAtUtc.HasValue
            || b.Objects.Any(o=>o.State!=BlueprintObjectStates.Completed)
            || b.Site.Objects.Any(o=>!StructuredConstructionBinding.SupportsItem(o.ItemId)))return false;
        var first=b.Objects[0];var count=b.Objects.Count;
        return first.InventoryBefore>=count && first.InventoryAfter==first.InventoryBefore-count
            && !string.IsNullOrEmpty(first.BeforeInventoryHash) && !string.IsNullOrEmpty(first.AfterInventoryHash)
            && b.Objects.All(o=>o.SubmittedAtGameTick.HasValue && o.CompletedAtGameTick.HasValue
                && o.SubmittedAtGameTick<=o.CompletedAtGameTick && o.CompletedAtGameTick<=tick
                && o.PrebuildId>0 && o.EntityId>0 && !c.PreexistingEntityIds.Contains(o.EntityId.Value)
                && o.InventoryBefore==first.InventoryBefore && o.InventoryAfter==first.InventoryAfter
                && o.BeforeInventoryHash==first.BeforeInventoryHash && o.AfterInventoryHash==first.AfterInventoryHash);
    }
    private static bool HasSavedPassiveTowerReceipt(BlueprintBuildState b, string journeyId, long tick)
    {
        if (b.Site.Objects.Count != 1 || b.Site.Objects[0].ItemId != 2104 || b.Objects.Count != 1
            || b.StructuredConstruction is not { Kind: "core" } p || p.JourneyId != journeyId
            || b.LastEvidenceGameTick > tick || (b.Phase != "outcome_unknown" && b.Phase != "completed")
            || b.ConstructionControl is not { DispatchDisabled: true, OriginalDroneEnabled: true } c
            || string.IsNullOrEmpty(c.NativeCreateActionId) || c.NativeCreateActionId != b.ActionId
            || string.IsNullOrEmpty(c.StopReason) || !c.DrainStartedAtGameTick.HasValue
            || !c.DrainStartedAtUtc.HasValue || c.DrainStartedAtGameTick > tick) return false;
        var s = b.Objects[0];
        return (s.State == BlueprintObjectStates.PendingConstruction || s.State == BlueprintObjectStates.Completed)
            && s.PrebuildId.GetValueOrDefault() > 0 && s.SubmittedAtGameTick.HasValue && s.SubmittedAtGameTick <= tick
            && s.InventoryBefore.GetValueOrDefault() >= 1 && s.InventoryAfter == s.InventoryBefore - 1
            && !string.IsNullOrEmpty(s.BeforeInventoryHash) && !string.IsNullOrEmpty(s.AfterInventoryHash)
            && (!s.CompletedAtGameTick.HasValue || s.CompletedAtGameTick <= tick);
    }
}
