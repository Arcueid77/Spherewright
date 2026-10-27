using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
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
