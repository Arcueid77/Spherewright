using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Factory;

// Internal durable provenance for an already native-validated structured build.
// It is not blueprint syntax, a prepare token, or authority to submit again.
public sealed class StructuredConstructionBinding
{
    public NativeStackConstructionPlan? StackConstruction { get; set; }
    public bool ShouldSerializeStackConstruction() => StackConstruction is not null;
    public string Kind { get; set; } = string.Empty;
    public string JourneyId { get; set; } = string.Empty;
    public string NativePlanHash { get; set; } = string.Empty;
    public int ResourceNodeId { get; set; }
    public string ResourceStateHash { get; set; } = string.Empty;
    public int SourceObjectId { get; set; }
    public int DestinationObjectId { get; set; }
    public List<StructuredConstructionConnection> Steps { get; set; } = new List<StructuredConstructionConnection>();
    public List<StructuredConstructionEndpoint> Endpoints { get; set; } = new List<StructuredConstructionEndpoint>();
    public StructuredEmptyBeltSource? EmptyBeltSource { get; set; }
    public bool ShouldSerializeEmptyBeltSource() => EmptyBeltSource is not null;

    public static bool SupportsItem(int id) => id == 2301 || id == 2104 || id == 2201 || id == 2203
        || id == 2205 || id == 2001 || id == 2011 || IsFactoryCoreItem(id);

    // These ordinary core types still need current-version Native placement,
    // paid drone work and exact component/pose/topology completion proof.
    public static bool IsFactoryCoreItem(int id) => id == 2302 || id == 2304 || id == 2309
        || id == 2101 || id == 2102 || id == 2106 || id == 2020 || id == 2901 || id == 2902
        || id == 3009 || id == 2040 || id == 2313 || id == 2211 || id == 2311 || id == 2312 || id == 2208;

    public static int MaximumNewObjects(int itemId) => itemId == 2001 ? 12 : 8;

    public string ImmutableHash()
    {
        if (Steps is null || Steps.Any(s => s is null) || Endpoints is null || Endpoints.Any(e => e is null))
            throw new InvalidDataException("Invalid structured provenance collections.");
        var original = CanonicalStateHash.Combine("structured-native-construction-v1", Kind, JourneyId,
            NativePlanHash, ResourceNodeId, ResourceStateHash, SourceObjectId, DestinationObjectId,
            CanonicalStateHash.Combine("connections", Steps.Select(s => (object)s.Hash()).ToArray()),
            CanonicalStateHash.Combine("endpoints", Endpoints.Select(e => (object)e.Hash()).ToArray()));
        var result = EmptyBeltSource is null ? original
            : CanonicalStateHash.Combine("structured-empty-belt-source-v1", original, EmptyBeltSource.Hash());
        return StackConstruction is null ? result
            : CanonicalStateHash.Combine("structured-native-stack-v1", result, NativeStackingPolicy.PlanHash(StackConstruction));
    }

    public void Validate(BlueprintSiteSnapshot site)
    {
        if (!Guid.TryParse(JourneyId, out _) || string.IsNullOrEmpty(NativePlanHash) || !NativePlanHash.StartsWith("sha256:", StringComparison.Ordinal)
            || site.Objects.Count < 1 || site.Objects.Count > MaximumNewObjects(site.Objects[0].ItemId)
            || Steps is null || Steps.Count != site.Objects.Count
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
                || s.NativeModelIndex.HasValue && s.NativeModelIndex.Value <= 0
                || s.OutputStepIndex < -1 || s.OutputStepIndex >= Steps.Count
                || s.InputObjectId < 0 || s.OutputObjectId < 0
                || s.InputFromSlot < -1 || s.InputFromSlot >= 16 || s.InputToSlot < 0 || s.InputToSlot >= 16
                || s.OutputFromSlot < 0 || s.OutputFromSlot >= 16 || s.OutputToSlot < -1 || s.OutputToSlot >= 16
                || !Finite(s.Yaw)))
            throw new InvalidDataException("Invalid native structured construction provenance.");
        foreach (var endpoint in Endpoints) endpoint.Validate();
        EmptyBeltSource?.Validate(this, site);
        if (Steps.Where((step, index) => step.NativeModelIndex != site.Objects[index].NativeModelIndex).Any())
            throw new InvalidDataException("Structured native model binding differs from its approved site.");
        if (StackConstruction is { } stack)
        {
            NativeStackingPolicy.ValidatePlan(stack);
            if (Kind != "core" || Steps.Count != 1
            || stack.AnchorObjectId != SourceObjectId || stack.BuildingItemId != site.Objects[0].ItemId
            || stack.ModelIndex != Steps[0].NativeModelIndex
            || Steps[0].InputObjectId != stack.AnchorObjectId || Steps[0].InputFromSlot != 15 || Steps[0].InputToSlot != 14)
                throw new InvalidDataException("Invalid native stack construction binding.");
        }
    }

    private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
}

// A copied empty OPEN TAIL admission, never a Native object or a replay grant.
// Ordinary Native source-cover creation still proves exact cargo and neighbours.
public sealed class StructuredEmptyBeltSource
{
    public int ObjectId { get; set; }
    public string BindingHash { get; set; } = string.Empty;
    public string CargoHash { get; set; } = string.Empty;
    public string CompletionTopologyHash { get; set; } = string.Empty;
    public int CargoStackCount { get; set; }

    public bool MatchesCurrent(int objectId, string bindingHash, string cargoHash, string completionTopologyHash, int cargoStackCount)
    {
        Hash();
        return ObjectId == objectId && cargoStackCount == 0
            && BeltSourceReusePolicy.SameEvidence(BindingHash, bindingHash)
            && BeltSourceReusePolicy.SameEvidence(CargoHash, cargoHash)
            && BeltSourceReusePolicy.SameEvidence(CompletionTopologyHash, completionTopologyHash);
    }

    public string Hash()
    {
        if (ObjectId <= 0 || CargoStackCount != 0
            || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(BindingHash)
            || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(CargoHash)
            || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(CompletionTopologyHash))
            throw new InvalidDataException("Invalid empty belt source evidence.");
        return CanonicalStateHash.Combine("structured-empty-belt-source-evidence-v1", ObjectId,
            BindingHash, CargoHash, CompletionTopologyHash, CargoStackCount);
    }

    public void Validate(StructuredConstructionBinding binding, BlueprintSiteSnapshot site)
    {
        Hash();
        if (!site.NativeCheckPerformed || !site.NativeCheckPassed
            || binding.Kind != "belt" || binding.SourceObjectId != ObjectId || binding.DestinationObjectId != 0
            || site.Objects.Count < 2 || site.Objects.Any(o => o.ItemId != 2001)
            || binding.Endpoints.Count != 1 || binding.Endpoints[0].ObjectId != ObjectId || binding.Endpoints[0].ItemId != 2001
            || binding.Endpoints[0].ExistingConnections.Any(c => c.Slot == 0)
            || binding.Steps[0].InputObjectId != ObjectId || binding.Steps[0].InputStepIndex != -1
            || binding.Steps[0].InputFromSlot != 0 || binding.Steps[0].InputToSlot != 1
            || binding.Steps[binding.Steps.Count - 1].OutputObjectId != 0)
            throw new InvalidDataException("Empty source extension requires one exact existing open belt tail and only new output objects.");
        for (var i = 0; i < binding.Steps.Count; i++)
        {
            var step = binding.Steps[i];
            if (step.InputStepIndex != (i == 0 ? -1 : i - 1) || step.InputObjectId != (i == 0 ? ObjectId : 0)
                || step.InputFromSlot != 0 || step.InputToSlot != 1
                || step.OutputStepIndex != (i + 1 < binding.Steps.Count ? i + 1 : -1)
                || step.OutputObjectId != 0 || step.OutputFromSlot != 0
                || i + 1 < binding.Steps.Count && step.OutputToSlot != 1)
                throw new InvalidDataException("Empty source extension requires the exact directed NEW belt chain.");
        }
    }
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
    public int? NativeModelIndex { get; set; }
    public bool ShouldSerializeNativeModelIndex() => NativeModelIndex.HasValue;
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
    public string Hash()
    {
        var original = CanonicalStateHash.Combine("structured-connection-v1", InputStepIndex, OutputStepIndex,
            InputObjectId, OutputObjectId, InputFromSlot, InputToSlot, OutputFromSlot, OutputToSlot, Yaw, IsConnectionNode);
        return NativeModelIndex.HasValue
            ? CanonicalStateHash.Combine("structured-native-model-v1", original, NativeModelIndex.Value) : original;
    }
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
    public double? NativeFixedDeltaTimeSeconds { get; set; }
    public bool ShouldSerializeNativeFixedDeltaTimeSeconds() => NativeFixedDeltaTimeSeconds.HasValue;
    public double DrainWallSeconds() => NativeFixedDeltaTimeSeconds.HasValue
        ? NativeConstructionCadencePolicy.WallBudget(TailGameTicks,NativeFixedDeltaTimeSeconds.Value) ?? double.NaN
        : TailGameTicks/60d;
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
        var wallTicks = NativeFixedDeltaTimeSeconds.HasValue
            ? Math.Floor((WorkDeadlineUtc-nowUtc).TotalSeconds/Math.Max(1d/60d,NativeFixedDeltaTimeSeconds.Value))
            : Math.Floor((WorkDeadlineUtc-nowUtc).TotalSeconds*60d);
        if (wallTicks < 1d) return null;
        return (long)Math.Min(WorkDeadlineGameTick - tick, wallTicks);
    }

    public bool RetainsOriginalWindowAndSubmission(StructuredConstructionControlState original) =>
        StartedAtGameTick == original.StartedAtGameTick && StartedAtUtc == original.StartedAtUtc
        && WorkDeadlineGameTick == original.WorkDeadlineGameTick && WorkDeadlineUtc == original.WorkDeadlineUtc
        && NativeFixedDeltaTimeSeconds == original.NativeFixedDeltaTimeSeconds
        && OriginalDroneEnabled == original.OriginalDroneEnabled
        && (original.NativeCreateActionId is null || NativeCreateActionId == original.NativeCreateActionId)
        && PreexistingEntityIds.SequenceEqual(original.PreexistingEntityIds);

    // A new task window never revives the old one. It requires a known safely
    // stopped paid batch; the caller archives this exact old record first and
    // independently proves current Native idle and original Journey reserves.
    public bool CanReadmitPaidPending(long tick, DateTimeOffset nowUtc) =>
        Phase == "paused_idle" && DispatchDisabled && PhysicalIdleVerified && !BindingLost
        && PhysicalIdleVerifiedAtGameTick.HasValue && tick >= PhysicalIdleVerifiedAtGameTick
        && StopReason is not null && Guid.TryParse(NativeCreateActionId, out _)
        && tick >= StartedAtGameTick && nowUtc >= StartedAtUtc
        && (tick >= WorkDeadlineGameTick || nowUtc >= WorkDeadlineUtc);

    public bool IsNewWindowFor(StructuredConstructionControlState original) =>
        original.CanReadmitPaidPending(StartedAtGameTick, StartedAtUtc)
        && NativeCreateActionId == original.NativeCreateActionId && OriginalDroneEnabled == original.OriginalDroneEnabled
        && PreexistingEntityIds.SequenceEqual(original.PreexistingEntityIds);

    public bool RenewObservationLease(DateTimeOffset nowUtc)
    {
        var deadline=StopReason is null ? WorkDeadlineUtc
            : Phase=="stop_draining" && DrainStartedAtUtc.HasValue && Finite(DrainWallSeconds())
                ? DrainStartedAtUtc.Value.AddSeconds(DrainWallSeconds()) : default;
        if ((Phase!="working" && (Phase!="stop_draining" || !NativeFixedDeltaTimeSeconds.HasValue))
            || nowUtc==default || nowUtc<StartedAtUtc || nowUtc>=deadline) return false;
        ObservationLeaseExpiresAtUtc = nowUtc.AddSeconds(30) < deadline ? nowUtc.AddSeconds(30) : deadline;
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
            || !Finite(DrainWallSeconds()) || nowUtc.Value >= DrainStartedAtUtc.Value.AddSeconds(DrainWallSeconds());
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
        // A disabled Native pass remains historical proof when a known, fully
        // idle completion restores the original enabled setting. Active drains
        // and every incomplete/unknown outcome still require disabled dispatch.
        var restoredCompletedIdle = Phase == "completed_idle" && OriginalDroneEnabled
            && !BindingLost && PhysicalIdleVerified && StopReason == "construction_complete_draining";
        if (Anchor is null || new[] { Anchor.X, Anchor.Y, Anchor.Z }.Any(v => float.IsNaN(v) || float.IsInfinity(v))
            || string.IsNullOrWhiteSpace(NativeCostHash) || StartedAtGameTick < capturedTick
            || WorkDeadlineGameTick <= StartedAtGameTick || WorkDeadlineGameTick - (decimal)StartedAtGameTick > 3600
            || StartedAtUtc == default || WorkDeadlineUtc <= StartedAtUtc
            || (WorkDeadlineUtc - StartedAtUtc).TotalSeconds > (NativeFixedDeltaTimeSeconds.HasValue
                ? NativeConstructionCadencePolicy.WallBudget(3600,NativeFixedDeltaTimeSeconds.Value)??0d : 60d)
            || NativeFixedDeltaTimeSeconds.HasValue && NativeConstructionCadencePolicy.WallBudget(TailGameTicks,NativeFixedDeltaTimeSeconds.Value) is null
            || ObservationLeaseExpiresAtUtc == default
            || TailGameTicks < 1 || TailGameTicks > 7200 || !Finite(TailAndNextTickEnergyJoules)
            || !Finite(NativePowerUpperWatts) || PreexistingEntityIds is null || PreexistingEntityIds.Count > 1024
            || (NativeCreateActionId is not null && !Guid.TryParse(NativeCreateActionId, out _))
            || PreexistingEntityIds.Any(id => id <= 0) || PreexistingEntityIds.Distinct().Count() != PreexistingEntityIds.Count
            || !Phases.Contains(Phase) || DrainStartedAtGameTick.HasValue != DrainStartedAtUtc.HasValue
            || (DisabledNativeFactoryPassGameTick.HasValue && ((!DispatchDisabled && !restoredCompletedIdle) || !DrainStartedAtGameTick.HasValue
                || DisabledNativeFactoryPassGameTick < DrainStartedAtGameTick))
            || PhysicalIdleVerified != PhysicalIdleVerifiedAtGameTick.HasValue
            || (PhysicalIdleVerified && (!DrainStartedAtGameTick.HasValue
                || PhysicalIdleVerifiedAtGameTick <= DrainStartedAtGameTick
                || !DisabledNativeFactoryPassGameTick.HasValue || DisabledNativeFactoryPassGameTick < DrainStartedAtGameTick
                || DisabledNativeFactoryPassGameTick > PhysicalIdleVerifiedAtGameTick))
            || (DrainStartedAtGameTick.HasValue && (DrainStartedAtGameTick < StartedAtGameTick || DrainStartedAtUtc!.Value == default)))
            throw new InvalidDataException("Invalid finite structured construction control record.");
    }

    public string EvidenceHash()
    {
        var old=CanonicalStateHash.Combine("structured-control-evidence-v1", Phase,
        Anchor.X, Anchor.Y, Anchor.Z, NativeCostHash, StartedAtGameTick, StartedAtUtc, WorkDeadlineGameTick, WorkDeadlineUtc,
        TailGameTicks, TailAndNextTickEnergyJoules, NativePowerUpperWatts, DrainStartedAtGameTick, DrainStartedAtUtc,
        DispatchDisabled, PhysicalIdleVerified, PhysicalIdleVerifiedAtGameTick, DisabledNativeFactoryPassGameTick,
        BindingLost, OriginalDroneEnabled, NativeCreateActionId, StopReason,
        CanonicalStateHash.Combine("old-entities", PreexistingEntityIds.Cast<object>().ToArray()));
        return NativeFixedDeltaTimeSeconds.HasValue ? CanonicalStateHash.Combine("structured-control-cadence-v1",old,NativeFixedDeltaTimeSeconds) : old;
    }

    private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x) && x >= 0d;
    private static readonly HashSet<string> Phases = new HashSet<string>(StringComparer.Ordinal)
    { "reserved", "working", "stop_draining", "completed_idle", "paused_idle", "quarantined", "interrupted" };
}
