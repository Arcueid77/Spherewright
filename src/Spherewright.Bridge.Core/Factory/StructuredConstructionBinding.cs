using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Factory;

// Internal durable provenance for an already native-validated structured build.
// It is not blueprint syntax, a prepare token, or authority to submit again.
public sealed class StructuredConstructionBinding
{
    public string Kind { get; set; } = string.Empty;
    public string JourneyId { get; set; } = string.Empty;
    public string NativePlanHash { get; set; } = string.Empty;
    public int ResourceNodeId { get; set; }
    public string ResourceStateHash { get; set; } = string.Empty;
    public int SourceObjectId { get; set; }
    public int DestinationObjectId { get; set; }
    public List<StructuredConstructionConnection> Steps { get; set; } = new List<StructuredConstructionConnection>();
    public List<StructuredConstructionEndpoint> Endpoints { get; set; } = new List<StructuredConstructionEndpoint>();

    public static bool SupportsItem(int id) => id == 2301 || id == 2104 || id == 2201 || id == 2203
        || id == 2205 || id == 2001 || id == 2011;

    public string ImmutableHash()
    {
        if (Steps is null || Steps.Any(s => s is null) || Endpoints is null || Endpoints.Any(e => e is null))
            throw new InvalidDataException("Invalid structured provenance collections.");
        return CanonicalStateHash.Combine("structured-native-construction-v1", Kind, JourneyId,
            NativePlanHash, ResourceNodeId, ResourceStateHash, SourceObjectId, DestinationObjectId,
            CanonicalStateHash.Combine("connections", Steps.Select(s => (object)s.Hash()).ToArray()),
            CanonicalStateHash.Combine("endpoints", Endpoints.Select(e => (object)e.Hash()).ToArray()));
    }

    public void Validate(BlueprintSiteSnapshot site)
    {
        if (!Guid.TryParse(JourneyId, out _) || string.IsNullOrEmpty(NativePlanHash) || !NativePlanHash.StartsWith("sha256:", StringComparison.Ordinal)
            || site.Objects.Count < 1 || site.Objects.Count > 8 || Steps is null || Steps.Count != site.Objects.Count
            || site.Objects.Select(o => o.ItemId).Distinct().Count() != 1
            || !SupportsItem(site.Objects[0].ItemId)
            || (Kind != "core" && Kind != "resource" && Kind != "belt" && Kind != "inserter")
            || (Kind == "core" && (site.Objects[0].ItemId == 2301 || site.Objects[0].ItemId == 2001 || site.Objects[0].ItemId == 2011))
            || (Kind == "resource" && (site.Objects[0].ItemId != 2301 || ResourceNodeId <= 0
                || string.IsNullOrWhiteSpace(ResourceStateHash) || !site.Objects[0].Parameters.Contains(ResourceNodeId)))
            || (Kind != "resource" && ResourceNodeId != 0)
            || (Kind == "belt" && site.Objects[0].ItemId != 2001)
            || (Kind == "inserter" && (site.Objects[0].ItemId != 2011 || SourceObjectId <= 0 || DestinationObjectId <= 0))
            || SourceObjectId < 0 || DestinationObjectId < 0 || Endpoints is null || Endpoints.Count > 2 || Endpoints.Any(e => e is null)
            || Endpoints.Select(e => e.ObjectId).Distinct().Count() != Endpoints.Count
            || !Endpoints.Select(e => e.ObjectId).OrderBy(i => i).SequenceEqual(
                new[] { SourceObjectId, DestinationObjectId }.Where(i => i > 0).Distinct().OrderBy(i => i))
            || Steps.Any(s => s is null || s.InputStepIndex < -1 || s.InputStepIndex >= Steps.Count
                || s.OutputStepIndex < -1 || s.OutputStepIndex >= Steps.Count
                || s.InputObjectId < 0 || s.OutputObjectId < 0
                || s.InputFromSlot < -1 || s.InputFromSlot >= 16 || s.InputToSlot < 0 || s.InputToSlot >= 16
                || s.OutputFromSlot < 0 || s.OutputFromSlot >= 16 || s.OutputToSlot < -1 || s.OutputToSlot >= 16
                || !Finite(s.Yaw)))
            throw new InvalidDataException("Invalid native structured construction provenance.");
        foreach (var endpoint in Endpoints) endpoint.Validate();
    }

    private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
}

public sealed class StructuredConstructionEndpoint
{
    public int ObjectId { get; set; }
    public int ItemId { get; set; }
    public Vector3Snapshot Position { get; set; } = new Vector3Snapshot();
    public QuaternionSnapshot Rotation { get; set; } = new QuaternionSnapshot();
    public List<FactoryConnectionSnapshot> ExistingConnections { get; set; } = new List<FactoryConnectionSnapshot>();
    public string Hash()
    {
        Validate();
        return CanonicalStateHash.Combine("structured-endpoint-v1", ObjectId, ItemId,
            Position.X, Position.Y, Position.Z, Rotation.X, Rotation.Y, Rotation.Z, Rotation.W,
            CanonicalStateHash.Combine("old-connections", ExistingConnections.OrderBy(c => c.Slot).Select(c => (object)
                CanonicalStateHash.Combine("edge", c.Slot, c.IsOutput, c.OtherObjectId, c.OtherSlot)).ToArray()));
    }
    public void Validate()
    {
        if (ObjectId <= 0 || ItemId <= 0 || Position is null || Rotation is null || ExistingConnections is null
            || ExistingConnections.Count > 16 || ExistingConnections.Any(c => c is null || c.Slot < 0 || c.Slot >= 16
                || c.OtherObjectId == 0 || c.OtherSlot < 0 || c.OtherSlot >= 16)
            || ExistingConnections.Select(c => c.Slot).Distinct().Count() != ExistingConnections.Count
            || new[] { Position.X, Position.Y, Position.Z, Rotation.X, Rotation.Y, Rotation.Z, Rotation.W }
                .Any(v => float.IsNaN(v) || float.IsInfinity(v)))
            throw new InvalidDataException("Invalid native structured endpoint identity.");
    }
}

public sealed class StructuredConstructionConnection
{
    public int InputStepIndex { get; set; } = -1;
    public int OutputStepIndex { get; set; } = -1;
    public int InputObjectId { get; set; }
    public int OutputObjectId { get; set; }
    public int InputFromSlot { get; set; }
    public int InputToSlot { get; set; }
    public int OutputFromSlot { get; set; }
    public int OutputToSlot { get; set; }
    public float Yaw { get; set; }
    public bool IsConnectionNode { get; set; }
    public string Hash() => CanonicalStateHash.Combine("structured-connection-v1", InputStepIndex, OutputStepIndex,
        InputObjectId, OutputObjectId, InputFromSlot, InputToSlot, OutputFromSlot, OutputToSlot, Yaw, IsConnectionNode);
}

public sealed class StructuredConstructionControlState
{
    public string Phase { get; set; } = "reserved";
    public Vector3Snapshot Anchor { get; set; } = new Vector3Snapshot();
    public string NativeCostHash { get; set; } = string.Empty;
    public long StartedAtGameTick { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public long WorkDeadlineGameTick { get; set; }
    public DateTimeOffset WorkDeadlineUtc { get; set; }
    public DateTimeOffset ObservationLeaseExpiresAtUtc { get; set; }
    public long TailGameTicks { get; set; }
    public double TailAndNextTickEnergyJoules { get; set; }
    public double NativePowerUpperWatts { get; set; }
    public long? DrainStartedAtGameTick { get; set; }
    public DateTimeOffset? DrainStartedAtUtc { get; set; }
    public bool DispatchDisabled { get; set; }
    public bool PhysicalIdleVerified { get; set; }
    public long? PhysicalIdleVerifiedAtGameTick { get; set; }
    public long? DisabledNativeFactoryPassGameTick { get; set; }
    public bool BindingLost { get; set; }
    public bool OriginalDroneEnabled { get; set; }
    public string? NativeCreateActionId { get; set; }
    public string? StopReason { get; set; }
    public List<int> PreexistingEntityIds { get; set; } = new List<int>();

    public long? RemainingWorkGameTicks(long tick, DateTimeOffset nowUtc)
    {
        if (tick < StartedAtGameTick || tick >= WorkDeadlineGameTick || nowUtc == default
            || nowUtc < StartedAtUtc || nowUtc >= WorkDeadlineUtc) return null;
        var wallTicks = Math.Floor((WorkDeadlineUtc - nowUtc).TotalSeconds * 60d);
        if (wallTicks < 1d) return null;
        return (long)Math.Min(WorkDeadlineGameTick - tick, wallTicks);
    }

    public bool RetainsOriginalWindowAndSubmission(StructuredConstructionControlState original) =>
        StartedAtGameTick == original.StartedAtGameTick && StartedAtUtc == original.StartedAtUtc
        && WorkDeadlineGameTick == original.WorkDeadlineGameTick && WorkDeadlineUtc == original.WorkDeadlineUtc
        && OriginalDroneEnabled == original.OriginalDroneEnabled
        && (original.NativeCreateActionId is null || NativeCreateActionId == original.NativeCreateActionId)
        && PreexistingEntityIds.SequenceEqual(original.PreexistingEntityIds);

    public bool RenewObservationLease(DateTimeOffset nowUtc)
    {
        if (StopReason is not null || Phase != "working" || nowUtc == default || nowUtc < StartedAtUtc || nowUtc >= WorkDeadlineUtc) return false;
        ObservationLeaseExpiresAtUtc = nowUtc.AddSeconds(30) < WorkDeadlineUtc ? nowUtc.AddSeconds(30) : WorkDeadlineUtc;
        return true;
    }

    public bool RequestStop(string reason, long tick, DateTimeOffset nowUtc, bool bindingLost)
    {
        BindingLost |= bindingLost;
        if (StopReason is not null) return false;
        if (string.IsNullOrWhiteSpace(reason) || tick < StartedAtGameTick)
            throw new InvalidOperationException("A stop needs its original finite clock facts.");
        if (nowUtc == default || nowUtc < StartedAtUtc)
        { StopReason = reason; Phase = "interrupted"; BindingLost = true; return true; }
        StopReason = reason; Phase = "stop_draining";
        DrainStartedAtGameTick = tick; DrainStartedAtUtc = nowUtc;
        return true;
    }

    public bool StopOutcomeUnknown(long? currentGameTick, DateTimeOffset? nowUtc)
    {
        if (StopReason is null) return false;
        return !currentGameTick.HasValue || !nowUtc.HasValue || !DrainStartedAtGameTick.HasValue || !DrainStartedAtUtc.HasValue
            || TailGameTicks < 1 || TailGameTicks > 7200 || currentGameTick < DrainStartedAtGameTick
            || nowUtc < DrainStartedAtUtc
            || (decimal)currentGameTick.Value >= (decimal)DrainStartedAtGameTick.Value + TailGameTicks
            || nowUtc.Value >= DrainStartedAtUtc.Value.AddSeconds(TailGameTicks / 60d);
    }

    public bool MayRestoreOriginalDroneSetting(bool allObjectsComplete, bool originalOutcomeKnown,
        bool exactOwnedHoldStillPresent, bool userPaused) =>
        allObjectsComplete && originalOutcomeKnown && exactOwnedHoldStillPresent && !userPaused
        && OriginalDroneEnabled && !BindingLost && DispatchDisabled && PhysicalIdleVerified
        && PhysicalIdleVerifiedAtGameTick.HasValue
        && NativeDrainBarrierPassed(PhysicalIdleVerifiedAtGameTick.Value, ownPrelaunchCacheClear: true)
        && Phase == "completed_idle" && StopReason == "construction_complete_draining";

    public bool NativeDrainBarrierPassed(long currentGameTick, bool ownPrelaunchCacheClear) =>
        DispatchDisabled && DrainStartedAtGameTick.HasValue && currentGameTick > DrainStartedAtGameTick.Value
        && DisabledNativeFactoryPassGameTick.HasValue && DisabledNativeFactoryPassGameTick >= DrainStartedAtGameTick
        && DisabledNativeFactoryPassGameTick <= currentGameTick && ownPrelaunchCacheClear;

    public bool RecordDisabledNativeFactoryPass(long nativePassGameTick)
    {
        if (!DispatchDisabled || !DrainStartedAtGameTick.HasValue || nativePassGameTick < DrainStartedAtGameTick
            || DisabledNativeFactoryPassGameTick.HasValue) return false;
        DisabledNativeFactoryPassGameTick = nativePassGameTick;
        return true;
    }

    public bool TryMarkPhysicalIdle(long currentGameTick, bool ownPrelaunchCacheClear, bool nativePoolAndModuleIdle)
    {
        if (!nativePoolAndModuleIdle || !NativeDrainBarrierPassed(currentGameTick, ownPrelaunchCacheClear)) return false;
        PhysicalIdleVerified = true;
        PhysicalIdleVerifiedAtGameTick ??= currentGameTick;
        return true;
    }

    public void Validate(long capturedTick)
    {
        if (Anchor is null || new[] { Anchor.X, Anchor.Y, Anchor.Z }.Any(v => float.IsNaN(v) || float.IsInfinity(v))
            || string.IsNullOrWhiteSpace(NativeCostHash) || StartedAtGameTick < capturedTick
            || WorkDeadlineGameTick <= StartedAtGameTick || WorkDeadlineGameTick - (decimal)StartedAtGameTick > 3600
            || StartedAtUtc == default || WorkDeadlineUtc <= StartedAtUtc
            || (WorkDeadlineUtc - StartedAtUtc).TotalSeconds > 60d || ObservationLeaseExpiresAtUtc == default
            || TailGameTicks < 1 || TailGameTicks > 7200 || !Finite(TailAndNextTickEnergyJoules)
            || !Finite(NativePowerUpperWatts) || PreexistingEntityIds is null || PreexistingEntityIds.Count > 1024
            || (NativeCreateActionId is not null && !Guid.TryParse(NativeCreateActionId, out _))
            || PreexistingEntityIds.Any(id => id <= 0) || PreexistingEntityIds.Distinct().Count() != PreexistingEntityIds.Count
            || !Phases.Contains(Phase) || DrainStartedAtGameTick.HasValue != DrainStartedAtUtc.HasValue
            || (DisabledNativeFactoryPassGameTick.HasValue && (!DispatchDisabled || !DrainStartedAtGameTick.HasValue
                || DisabledNativeFactoryPassGameTick < DrainStartedAtGameTick))
            || PhysicalIdleVerified != PhysicalIdleVerifiedAtGameTick.HasValue
            || (PhysicalIdleVerified && (!DrainStartedAtGameTick.HasValue
                || PhysicalIdleVerifiedAtGameTick <= DrainStartedAtGameTick
                || !DisabledNativeFactoryPassGameTick.HasValue || DisabledNativeFactoryPassGameTick < DrainStartedAtGameTick
                || DisabledNativeFactoryPassGameTick > PhysicalIdleVerifiedAtGameTick))
            || (DrainStartedAtGameTick.HasValue && (DrainStartedAtGameTick < StartedAtGameTick || DrainStartedAtUtc!.Value == default)))
            throw new InvalidDataException("Invalid finite structured construction control record.");
    }

    public string EvidenceHash() => CanonicalStateHash.Combine("structured-control-evidence-v1", Phase,
        Anchor.X, Anchor.Y, Anchor.Z, NativeCostHash, StartedAtGameTick, StartedAtUtc, WorkDeadlineGameTick, WorkDeadlineUtc,
        TailGameTicks, TailAndNextTickEnergyJoules, NativePowerUpperWatts, DrainStartedAtGameTick, DrainStartedAtUtc,
        DispatchDisabled, PhysicalIdleVerified, PhysicalIdleVerifiedAtGameTick, DisabledNativeFactoryPassGameTick,
        BindingLost, OriginalDroneEnabled, NativeCreateActionId, StopReason,
        CanonicalStateHash.Combine("old-entities", PreexistingEntityIds.Cast<object>().ToArray()));

    private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x) && x >= 0d;
    private static readonly HashSet<string> Phases = new HashSet<string>(StringComparer.Ordinal)
    { "reserved", "working", "stop_draining", "completed_idle", "paused_idle", "quarantined", "interrupted" };
}
