using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Factory;

// Finite execution bookkeeping shared with normal actions; no goal selection or game API.
// Stores never serialize a prepare token or restore write authority from this record.
public sealed class BlueprintBuildState
{
    public string BuildId { get; set; } = string.Empty;
    public string PlanHash { get; set; } = string.Empty;
    public BlueprintSiteSnapshot Site { get; set; } = new BlueprintSiteSnapshot();
    public List<BlueprintObjectProgress> Objects { get; set; } = new List<BlueprintObjectProgress>();
    public string Phase { get; set; } = "prepared";
    public string ActiveSessionId { get; set; } = string.Empty;
    public string? ActionId { get; set; }
    public long LastEvidenceGameTick { get; set; }
    public int SubmittedThisAction { get; set; }
    public int MaximumObjectsToSubmit { get; set; }
    public long Generation { get; set; }
    public FoundryConstructionPlan? FoundryPlan { get; set; }
    public FoundryConstructionIntent? FoundryIntent { get; set; }
    public StructuredConstructionBinding? StructuredConstruction { get; set; }
    public StructuredConstructionControlState? ConstructionControl { get; set; }

    public static BlueprintBuildState Create(BlueprintSiteSnapshot site, FoundryConstructionPlan? foundryPlan = null,
        FoundryConstructionIntent? foundryIntent = null, StructuredConstructionBinding? structuredConstruction = null)
    {
        if (!site.NativeCheckPerformed || !site.NativeCheckPassed || !site.InventorySufficient
            || !site.TechnologySatisfied || site.Blockers.Count != 0)
            throw new InvalidOperationException("Blueprint placement, technology and whole budget must pass before execution.");
        var state = new BlueprintBuildState
        {
            BuildId = Guid.NewGuid().ToString("D"), Site = site, LastEvidenceGameTick = site.CapturedAtGameTick,
            Objects = site.Objects.Select(o => new BlueprintObjectProgress { Index = o.Index }).ToList(),
            FoundryPlan = foundryPlan, FoundryIntent = foundryIntent,
            StructuredConstruction = structuredConstruction,
        };
        FoundryConstructionCompiler.ValidateBinding(foundryPlan, site, foundryIntent);
        state.PlanHash = state.ImmutableHash();
        state.Validate();
        return state;
    }

    public void Validate()
    {
        if (!Guid.TryParse(BuildId, out _) || Site is null || Site.Objects is null || Objects is null
            || Site.Objects.Count < 1 || Site.Objects.Count > BlueprintSitePolicy.MaximumPlacementObjects
            || Objects.Count != Site.Objects.Count || Site.CapturedAtGameTick < 0
            || LastEvidenceGameTick < Site.CapturedAtGameTick || Generation < 0 || !AllowedPhases.Contains(Phase)
            || Site.Connections is null || Site.ConstructionItems is null || Site.Blockers is null || Site.Position is null
            || Site.Connections.Count > BlueprintSitePolicy.MaximumPlacementObjects * 16
            || Site.Connections.Any(e => e is null || e.FromIndex < 0 || e.ToIndex < 0
                || e.FromIndex >= Objects.Count || e.ToIndex >= Objects.Count || e.FromIndex == e.ToIndex
                || e.FromSlot < -1 || e.FromSlot >= 16 || e.ToSlot < -1 || e.ToSlot >= 16)
            || Site.ConstructionItems.Count > Objects.Count || Site.ConstructionItems.Any(i => i is null)
            || MaximumObjectsToSubmit < 0 || MaximumObjectsToSubmit > BlueprintSitePolicy.MaximumPlacementObjects
            || SubmittedThisAction < 0 || SubmittedThisAction > MaximumObjectsToSubmit
            || Site.Objects.Where((o, i) => o is null || o.Index != i
                || !(StructuredConstruction is null ? BoundedBlueprintReader.SupportsItem(o.ItemId) : StructuredConstructionBinding.SupportsItem(o.ItemId))
                || o.Dependencies is null || o.Parameters is null || o.Position is null || o.Position2 is null
                || o.Dependencies.Count > Objects.Count || o.Parameters.Length > 128
                || o.Rotation is null || o.Rotation2 is null
                || o.Dependencies.Any(d => d < 0 || d >= Objects.Count || d == i)).Any()
            || Objects.Where((o, i) => o is null || o.Index != i || !AllowedStates.Contains(o.State)).Any()
            || PlanHash != ImmutableHash())
            throw new InvalidDataException("Invalid bounded blueprint progress or immutable plan identity.");
        FoundryConstructionCompiler.ValidateBinding(FoundryPlan, Site, FoundryIntent);
        if (StructuredConstruction is not null)
        {
            StructuredConstruction.Validate(Site);
            if (FoundryPlan is not null || FoundryIntent is not null) throw new InvalidDataException("Structured provenance is not blueprint/Foundry authority.");
            ConstructionControl?.Validate(Site.CapturedAtGameTick);
            if (ConstructionControl?.PhysicalIdleVerifiedAtGameTick > LastEvidenceGameTick
                || ConstructionControl?.DisabledNativeFactoryPassGameTick > LastEvidenceGameTick)
                throw new InvalidDataException("Physical drone idle must be covered by the durable native evidence watermark.");
            if (Phase == "running" && ConstructionControl is null)
                throw new InvalidDataException("Structured execution requires its original finite control lease.");
            if (Phase == "completed" && (ConstructionControl?.PhysicalIdleVerified != true
                || ConstructionControl.Phase != "completed_idle"))
                throw new InvalidDataException("A structured build is not complete before physical drone idle proof.");
        }
        else if (ConstructionControl is not null) throw new InvalidDataException("A generic blueprint cannot inherit structured control authority.");
        var visited = new HashSet<int>();
        while (visited.Count < Objects.Count)
        {
            var ready = Site.Objects.Where(o => !visited.Contains(o.Index) && o.Dependencies.All(visited.Contains)).ToArray();
            if (ready.Length == 0) throw new InvalidDataException("Cyclic construction dependencies cannot resume.");
            foreach (var item in ready) visited.Add(item.Index);
        }
        if ((StructuredConstruction is null && Objects.Count(o => o.State == BlueprintObjectStates.PendingConstruction || o.State == BlueprintObjectStates.Submitting) > 1)
            || Objects.Where(o => o.EntityId.HasValue).Select(o => o.EntityId).Distinct().Count() != Objects.Count(o => o.EntityId.HasValue)
            || (Phase == "completed" && Objects.Any(o => o.State != BlueprintObjectStates.Completed)))
            throw new InvalidDataException("Finite progress contains impossible object identities or phase.");
        foreach (var step in Objects)
        {
            var untouched = step.State == BlueprintObjectStates.NotSubmitted || step.State == BlueprintObjectStates.Blocked;
            if ((untouched && (step.PrebuildId.HasValue || step.EntityId.HasValue || step.SubmittedAtGameTick.HasValue
                    || step.CompletedAtGameTick.HasValue || step.InventoryBefore.HasValue || step.InventoryAfter.HasValue
                    || step.BeforeInventoryHash is not null || step.AfterInventoryHash is not null))
                || (step.SubmittedAtGameTick.HasValue && (step.SubmittedAtGameTick < Site.CapturedAtGameTick
                    || step.SubmittedAtGameTick > LastEvidenceGameTick))
                || (step.CompletedAtGameTick.HasValue && (!step.SubmittedAtGameTick.HasValue
                    || step.CompletedAtGameTick < step.SubmittedAtGameTick || step.CompletedAtGameTick > LastEvidenceGameTick)))
                throw new InvalidDataException("Object state contradicts its durable receipt or time watermark.");
            if (step.State == BlueprintObjectStates.Submitting
                && (!step.SubmittedAtGameTick.HasValue || step.InventoryBefore.GetValueOrDefault() < 1
                    || string.IsNullOrWhiteSpace(step.BeforeInventoryHash) || step.PrebuildId.HasValue || step.InventoryAfter.HasValue))
                throw new InvalidDataException("Submitting object lacks an unambiguous write-ahead record.");
            var submitted = step.State == BlueprintObjectStates.PendingConstruction || step.State == BlueprintObjectStates.Completed;
            if (submitted && (step.PrebuildId.GetValueOrDefault() <= 0 || !step.SubmittedAtGameTick.HasValue
                || !step.InventoryBefore.HasValue || step.InventoryBefore < 1
                || step.InventoryAfter != step.InventoryBefore - (StructuredConstruction is null ? 1 : Objects.Count)
                || string.IsNullOrWhiteSpace(step.BeforeInventoryHash) || string.IsNullOrWhiteSpace(step.AfterInventoryHash)))
                throw new InvalidDataException("Submitted object lacks durable exact-cost evidence.");
            if (step.State == BlueprintObjectStates.Completed
                && (step.EntityId.GetValueOrDefault() <= 0 || !step.CompletedAtGameTick.HasValue))
                throw new InvalidDataException("Completed object lacks its result identity.");
        }
    }

    private string ImmutableHash()
    {
        var native = BlueprintSitePolicy.AssessmentHash(Site, "immutable-approved-site");
        if (StructuredConstruction is not null) return CanonicalStateHash.Combine("structured-approved-site-v1", native, StructuredConstruction.ImmutableHash());
        return FoundryPlan is null ? native : CanonicalStateHash.Combine("foundry-bound-blueprint-v1", native,
            FoundryPlan.PlanHash, FoundryConstructionCompiler.IntentHash(FoundryIntent!));
    }

    public void Begin(string sessionId, string actionId, int maximumObjects, long tick)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !Guid.TryParse(actionId, out _)
            || maximumObjects < 1 || maximumObjects > BlueprintSitePolicy.MaximumPlacementObjects
            || tick < LastEvidenceGameTick || Phase == "completed" || Phase == "outcome_unknown"
            || (Phase == "running" && ActiveSessionId == sessionId)
            || Objects.Any(o => o.State == BlueprintObjectStates.Submitting
                || o.State == BlueprintObjectStates.OutcomeUnknown))
            throw new InvalidOperationException("Fresh authority and proven previous outcomes are required.");
        foreach (var step in Objects.Where(o => o.State == BlueprintObjectStates.Blocked))
        { step.State = BlueprintObjectStates.NotSubmitted; step.FailureKind = null; }
        Phase = "running"; ActiveSessionId = sessionId; ActionId = actionId;
        MaximumObjectsToSubmit = maximumObjects; SubmittedThisAction = 0; Generation++;
    }

    public int? NextReadyIndex()
    {
        if (StructuredConstruction is not null || Phase != "running" || SubmittedThisAction >= MaximumObjectsToSubmit
            || Objects.Any(o => o.State == BlueprintObjectStates.PendingConstruction
                || o.State == BlueprintObjectStates.Submitting || o.State == BlueprintObjectStates.OutcomeUnknown)) return null;
        foreach (var step in Objects)
            if (step.State == BlueprintObjectStates.NotSubmitted
                && Site.Objects[step.Index].Dependencies.All(i => Objects[i].State == BlueprintObjectStates.Completed)) return step.Index;
        return null;
    }

    public void BeforeSubmit(int index, long tick, int inventoryCount, string inventoryHash)
    {
        if (NextReadyIndex() != index || inventoryCount < 1 || tick < LastEvidenceGameTick || string.IsNullOrWhiteSpace(inventoryHash))
            throw new InvalidOperationException("Only one proven unsubmitted dependency-ready object may be submitted.");
        var step = Objects[index]; step.State = BlueprintObjectStates.Submitting;
        step.SubmittedAtGameTick = tick; step.InventoryBefore = inventoryCount; step.BeforeInventoryHash = inventoryHash;
        LastEvidenceGameTick = tick; Generation++;
    }

    public void BeforeStructuredSubmission(long tick, int inventoryCount, string inventoryHash)
    {
        if (StructuredConstruction is null || Phase != "running" || MaximumObjectsToSubmit < Objects.Count
            || Objects.Any(o => o.State != BlueprintObjectStates.NotSubmitted)
            || inventoryCount < Objects.Count || tick < LastEvidenceGameTick || string.IsNullOrWhiteSpace(inventoryHash))
            throw new InvalidOperationException("A native structured batch requires one complete durable write-ahead receipt.");
        foreach (var step in Objects)
        {
            step.State = BlueprintObjectStates.Submitting; step.SubmittedAtGameTick = tick;
            step.InventoryBefore = inventoryCount; step.BeforeInventoryHash = inventoryHash;
        }
        LastEvidenceGameTick = tick; Generation++;
    }

    public void ConfirmStructuredSubmission(IReadOnlyList<int> prebuildIds, int inventoryAfter, string inventoryHash)
    {
        if (StructuredConstruction is null || prebuildIds.Count != Objects.Count || prebuildIds.Any(id => id <= 0)
            || prebuildIds.Distinct().Count() != Objects.Count || string.IsNullOrWhiteSpace(inventoryHash)
            || Objects.Any(o => o.State != BlueprintObjectStates.Submitting || inventoryAfter != o.InventoryBefore - Objects.Count))
            throw new InvalidOperationException("Native structured IDs and one measured whole-batch debit are required.");
        for (var index = 0; index < Objects.Count; index++)
        {
            var step = Objects[index]; step.PrebuildId = prebuildIds[index]; step.InventoryAfter = inventoryAfter;
            step.AfterInventoryHash = inventoryHash; step.State = BlueprintObjectStates.PendingConstruction;
        }
        SubmittedThisAction = Objects.Count; Generation++;
    }

    public void ConfirmSubmission(int index, int prebuildId, int inventoryAfter, string inventoryHash)
    {
        var step = Objects[index];
        if (step.State != BlueprintObjectStates.Submitting || prebuildId <= 0 || inventoryAfter != step.InventoryBefore - 1
            || string.IsNullOrWhiteSpace(inventoryHash))
            throw new InvalidOperationException("Native prebuild and exact one-item debit are required.");
        step.PrebuildId = prebuildId; step.InventoryAfter = inventoryAfter; step.AfterInventoryHash = inventoryHash;
        step.State = BlueprintObjectStates.PendingConstruction; SubmittedThisAction++; Generation++;
    }

    public void ConfirmCompletion(int index, int entityId, long tick)
    {
        var step = Objects[index];
        if (step.State != BlueprintObjectStates.PendingConstruction || entityId <= 0 || tick < LastEvidenceGameTick)
            throw new InvalidOperationException("Only a proven pending object can complete.");
        step.EntityId = entityId; step.CompletedAtGameTick = tick; step.State = BlueprintObjectStates.Completed;
        LastEvidenceGameTick = tick; Generation++;
    }

    public void Stop(string phase, int? blockedIndex = null, string? reason = null)
    {
        if (phase != "cancelled" && phase != "paused" && phase != "blocked" && phase != "completed" && phase != "outcome_unknown")
            throw new ArgumentException("Invalid finite execution stop phase.");
        if (phase == "completed" && Objects.Any(o => o.State != BlueprintObjectStates.Completed))
            throw new InvalidOperationException("Partial success cannot be marked completed.");
        if (blockedIndex.HasValue)
        {
            var step = Objects[blockedIndex.Value];
            if (step.State == BlueprintObjectStates.Submitting || phase == "outcome_unknown") step.State = BlueprintObjectStates.OutcomeUnknown;
            else if (step.State == BlueprintObjectStates.NotSubmitted) step.State = BlueprintObjectStates.Blocked;
            step.FailureKind = reason;
        }
        Phase = phase; Generation++;
    }

    public string ProgressHash(string currentSession, long revision)
    {
        var original = CanonicalStateHash.Combine(
            "blueprint-progress-v1", currentSession, revision, BuildId, PlanHash, Generation, Phase,
            LastEvidenceGameTick, CanonicalStateHash.Combine("objects", Objects.Select(o => (object)CanonicalStateHash.Combine(
                "object", o.Index, o.State, o.PrebuildId, o.EntityId, o.SubmittedAtGameTick, o.CompletedAtGameTick,
                o.InventoryBefore, o.InventoryAfter, o.BeforeInventoryHash, o.AfterInventoryHash, o.FailureKind)).ToArray()));
        return ConstructionControl is null ? original
            : CanonicalStateHash.Combine("structured-control-progress-v1", original, ConstructionControl.EvidenceHash());
    }

    private static readonly HashSet<string> AllowedStates = new HashSet<string>(StringComparer.Ordinal)
    { BlueprintObjectStates.NotSubmitted, BlueprintObjectStates.Submitting, BlueprintObjectStates.PendingConstruction,
      BlueprintObjectStates.Completed, BlueprintObjectStates.Blocked, BlueprintObjectStates.OutcomeUnknown };
    private static readonly HashSet<string> AllowedPhases = new HashSet<string>(StringComparer.Ordinal)
    { "prepared", "running", "paused", "blocked", "cancelled", "completed", "outcome_unknown" };
}
