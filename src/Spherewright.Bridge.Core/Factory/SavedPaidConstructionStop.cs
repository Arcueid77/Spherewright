using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Factory;

// A later independently observed Native STOP, not completion or authority to
// repeat Create. The original unknown control remains byte-identical.
public sealed class SavedPaidConstructionStop
{
    public string OriginalActionId { get; set; } = string.Empty;
    public string StoppedActionId { get; set; } = string.Empty;
    public string OriginalControlHash { get; set; } = string.Empty;
    public string BuildPlanHash { get; set; } = string.Empty;
    public string PaidSubmissionHash { get; set; } = string.Empty;
    public string NativeSessionId { get; set; } = string.Empty;
    public string SourceFingerprint { get; set; } = string.Empty;
    public long SourceGameTick { get; set; }
    public long NativeFactoryPassGameTick { get; set; }
    public long PhysicalIdleGameTick { get; set; }
    public List<SavedPaidCompletedObject> CompletedAtStop { get; set; } = new();

    public static string SubmissionHash(BlueprintBuildState b) => CanonicalStateHash.Combine("saved-paid-submission-v1",
        b.BuildId, b.PlanHash, CanonicalStateHash.Combine("original-paid-objects", b.Objects.Select(o => (object)CanonicalStateHash.Combine(
            "paid-object", o.Index, o.PrebuildId, o.SubmittedAtGameTick, o.InventoryBefore, o.InventoryAfter,
            o.BeforeInventoryHash, o.AfterInventoryHash)).ToArray()));

    public bool Matches(BlueprintBuildState b, long tick)
    {
        var controls = (b.PreviousConstructionWindows ?? new()).Concat(b.ConstructionControl is null
            ? Array.Empty<StructuredConstructionControlState>() : new[] { b.ConstructionControl });
        var matching = controls.Where(c => c is not null && c.EvidenceHash() == OriginalControlHash).Take(2).ToArray();
        if (matching.Length != 1) return false;
        var original = matching[0];
        if (!original.DispatchDisabled || original.StopReason is null
            || original.NativeCreateActionId != OriginalActionId || !Guid.TryParse(OriginalActionId, out _) || !Guid.TryParse(StoppedActionId, out _)
            || !Guid.TryParse(NativeSessionId, out _) || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(SourceFingerprint)
            || BuildPlanHash != b.PlanHash || PaidSubmissionHash != SubmissionHash(b)
            || SourceGameTick < original.StartedAtGameTick || SourceGameTick < original.DrainStartedAtGameTick
            || !original.DrainStartedAtGameTick.HasValue || NativeFactoryPassGameTick <= SourceGameTick
            || PhysicalIdleGameTick < NativeFactoryPassGameTick || PhysicalIdleGameTick > tick
            || b.Objects.Count == 0 || b.Objects.Any(o => o.State != BlueprintObjectStates.PendingConstruction && o.State != BlueprintObjectStates.Completed)
            || CompletedAtStop is null || CompletedAtStop.Select(o => o.Index).Distinct().Count() != CompletedAtStop.Count)
            return false;
        var first = b.Objects[0]; var count = b.Objects.Count;
        if (!first.InventoryBefore.HasValue || first.InventoryBefore < count || !first.InventoryAfter.HasValue || first.InventoryAfter != first.InventoryBefore - count
            || string.IsNullOrEmpty(first.BeforeInventoryHash) || string.IsNullOrEmpty(first.AfterInventoryHash)
            || b.Objects.Any(o => !o.SubmittedAtGameTick.HasValue || o.SubmittedAtGameTick > SourceGameTick || o.PrebuildId <= 0
                || o.InventoryBefore != first.InventoryBefore || o.InventoryAfter != first.InventoryAfter
                || o.BeforeInventoryHash != first.BeforeInventoryHash || o.AfterInventoryHash != first.AfterInventoryHash)) return false;
        return CompletedAtStop.All(w => w.Index >= 0 && w.Index < count && w.EntityId > 0
            && b.Objects[w.Index].State == BlueprintObjectStates.Completed && b.Objects[w.Index].EntityId == w.EntityId
            && !original.PreexistingEntityIds.Contains(w.EntityId));
    }

    public bool PermitsWindow(BlueprintBuildState b, StructuredConstructionControlState original, StructuredConstructionControlState next) =>
        Matches(b, next.StartedAtGameTick) && original.EvidenceHash() == OriginalControlHash
        && next.StartedAtGameTick >= PhysicalIdleGameTick && next.StartedAtUtc >= original.StartedAtUtc
        && (next.StartedAtGameTick >= original.WorkDeadlineGameTick || next.StartedAtUtc >= original.WorkDeadlineUtc)
        && next.NativeCreateActionId == original.NativeCreateActionId && next.OriginalDroneEnabled == original.OriginalDroneEnabled
        && next.PreexistingEntityIds.SequenceEqual(original.PreexistingEntityIds);

    public static SavedPaidConstructionStop? Current(BlueprintBuildState b, long tick) => b.ConstructionControl is null ? null
        : b.SavedPaidStops.LastOrDefault(s => s.OriginalControlHash == b.ConstructionControl.EvidenceHash() && s.Matches(b, tick));

    public string EvidenceHash() => CanonicalStateHash.Combine("saved-paid-native-stop-v1", OriginalActionId, StoppedActionId, OriginalControlHash,
        BuildPlanHash, PaidSubmissionHash, NativeSessionId, SourceFingerprint, SourceGameTick, NativeFactoryPassGameTick, PhysicalIdleGameTick,
        CanonicalStateHash.Combine("completed-at-stop", CompletedAtStop.Select(o => (object)CanonicalStateHash.Combine("object", o.Index, o.EntityId)).ToArray()));
}

public sealed class SavedPaidCompletedObject
{
    public int Index { get; set; }
    public int EntityId { get; set; }
}
