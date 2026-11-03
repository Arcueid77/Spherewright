using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Factory;

// Appended by a later exact Native admission. The old unknown action/control
// remains unchanged; this certifies only a newly observed physical state.
public sealed class SavedFactoryTailCertificate
{
    public string OriginalActionId {get;set;}=string.Empty;
    public string OriginalControlHash {get;set;}=string.Empty;
    public string BuildPlanHash {get;set;}=string.Empty;
    public long SourceGameTick {get;set;}
    public long NativeFactoryPassGameTick {get;set;}
    public long PhysicalIdleGameTick {get;set;}
    public string SourceFingerprint {get;set;}=string.Empty;

    public bool Matches(BlueprintBuildState b,long currentTick) => b.ConstructionControl is { } c
        && Guid.TryParse(OriginalActionId,out _) && OriginalActionId==b.ActionId && OriginalActionId==c.NativeCreateActionId
        && OriginalControlHash==c.EvidenceHash() && BuildPlanHash==b.PlanHash && !string.IsNullOrEmpty(SourceFingerprint)
        && SourceGameTick>=0 && NativeFactoryPassGameTick>SourceGameTick && PhysicalIdleGameTick>=NativeFactoryPassGameTick
        && PhysicalIdleGameTick<=currentTick && b.Objects.All(o=>o.State==BlueprintObjectStates.Completed);
    public string EvidenceHash()=>CanonicalStateHash.Combine("saved-factory-tail-state-v1",OriginalActionId,OriginalControlHash,
        BuildPlanHash,SourceGameTick,NativeFactoryPassGameTick,PhysicalIdleGameTick,SourceFingerprint);
}
