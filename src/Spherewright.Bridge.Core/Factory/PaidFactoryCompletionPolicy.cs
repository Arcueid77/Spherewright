using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Factory;

// Physical completion and the original action outcome are independent. Never
// rewrite an unknown action to admit a later, separately certified idle site.
public static class PaidFactoryCompletionPolicy
{
    public static bool HasCompletedIdleProof(BlueprintBuildState build, long currentTick)
    {
        if (build.StructuredConstruction is null || build.ConstructionControl is null
            || build.LastEvidenceGameTick > currentTick || build.Objects.Count == 0
            || build.Objects.Count != build.Site.Objects.Count
            || build.Objects.Any(o => o.State != BlueprintObjectStates.Completed)) return false;
        if (build.SavedTailCompletion?.Matches(build, currentTick) == true) return true;
        if (SavedPaidConstructionStop.Current(build, currentTick) is not null) return true;
        var control = build.ConstructionControl;
        return build.Phase == "completed" && control.Phase == "completed_idle"
            && control.PhysicalIdleVerified && control.PhysicalIdleVerifiedAtGameTick.HasValue
            && control.PhysicalIdleVerifiedAtGameTick <= currentTick
            && control.DisabledNativeFactoryPassGameTick.HasValue
            && control.DisabledNativeFactoryPassGameTick <= control.PhysicalIdleVerifiedAtGameTick;
    }
}
