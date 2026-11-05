using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Factory;

public static class StructuredConstructionWindowPolicy
{
    // A different anchor belongs only to a distinct, expired, safely stopped
    // paid window. Ordinary continuation keeps the exact original anchor.
    public static bool StartsNewPaidWindow(BlueprintBuildState b,bool explicitlyAllowed,bool currentNativeStop,
        long tick,DateTimeOffset now)
    {
        var c=b.ConstructionControl;
        if(!explicitlyAllowed || c is null || c.RemainingWorkGameTicks(tick,now).HasValue
            || b.PreviousConstructionWindows.Count>=16
            || !b.Objects.Any(o=>o.State==BlueprintObjectStates.PendingConstruction)
            || b.Objects.Any(o=>o.State!=BlueprintObjectStates.PendingConstruction && o.State!=BlueprintObjectStates.Completed))return false;
        return b.Phase=="paused" && c.CanReadmitPaidPending(tick,now)
            || b.Phase=="outcome_unknown" && currentNativeStop && SavedPaidConstructionStop.Current(b,tick) is not null;
    }

    public static Vector3Snapshot ReserveAnchor(BlueprintBuildState b,Vector3Snapshot current,bool startsNewPaidWindow)
    {
        var selected=startsNewPaidWindow || b.ConstructionControl is null ? current : b.ConstructionControl.Anchor;
        return new Vector3Snapshot {X=selected.X,Y=selected.Y,Z=selected.Z};
    }
}
