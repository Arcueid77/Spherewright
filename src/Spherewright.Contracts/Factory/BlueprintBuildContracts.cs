namespace Spherewright.Contracts.Factory;

public sealed class PrepareBlueprintBuildRequest
{
    public int PlanetId { get; set; }
    // Exactly one source: a new explicit code/site, or an owned persisted finite plan.
    public string? BlueprintCode { get; set; }
    public BlueprintSiteRequest? Site { get; set; }
    public string? ResumeBuildId { get; set; }
    public string ExpectedStateHash { get; set; } = string.Empty;
    public string ExpectedPlayerStateHash { get; set; } = string.Empty;
    public int StateHashVersion { get; set; } = 1;
    public int MaximumObjectsToSubmit { get; set; } = 32;
    public FoundryConstructionIntent? FoundryIntent { get; set; }
    public string? ExpectedFoundryPlanHash { get; set; }
}

public sealed class BlueprintBuildRequest
{
    public int PlanetId { get; set; }
    public string? BuildId { get; set; }
}

public sealed class PrepareCancelBlueprintRequest
{
    public int PlanetId { get; set; }
    public string BuildId { get; set; } = string.Empty;
    public string ExpectedStateHash { get; set; } = string.Empty;
    public int StateHashVersion { get; set; } = 1;
}

public static class BlueprintObjectStates
{
    public const string NotSubmitted = "not_submitted";
    public const string Submitting = "submitting";
    public const string PendingConstruction = "pending_construction";
    public const string Completed = "completed";
    public const string Blocked = "blocked";
    public const string OutcomeUnknown = "outcome_unknown";
}

public sealed class BlueprintObjectProgress
{
    public int Index { get; set; }
    public string State { get; set; } = BlueprintObjectStates.NotSubmitted;
    public int? PrebuildId { get; set; }
    public int? EntityId { get; set; }
    public long? SubmittedAtGameTick { get; set; }
    public long? CompletedAtGameTick { get; set; }
    public int? InventoryBefore { get; set; }
    public int? InventoryAfter { get; set; }
    public string? BeforeInventoryHash { get; set; }
    public string? AfterInventoryHash { get; set; }
    public string? FailureKind { get; set; }
}

public sealed class BlueprintBuildProgress
{
    // Null for the original blueprint executor. A structured receipt does not
    // make mining parameters part of the generic blueprint input format.
    public ConstructionControlReadback? ConstructionControl { get; set; }
    public string BuildId { get; set; } = string.Empty;
    public string BlueprintHash { get; set; } = string.Empty;
    public string PlanHash { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public int PlanetId { get; set; }
    public long Revision { get; set; }
    public long CapturedAtGameTick { get; set; }
    public string StateHash { get; set; } = string.Empty;
    public int StateHashVersion { get; set; } = 1;
    public string Phase { get; set; } = "prepared";
    public string? ActionId { get; set; }
    public bool PersistenceHealthy { get; set; }
    public bool FreshWorldReconciled { get; set; }
    public bool RequiresFreshPrepare { get; set; }
    public bool DoNotReplayWholeBlueprint { get; set; } = true;
    public int SubmittedCount { get; set; }
    public int CompletedCount { get; set; }
    public List<BlueprintObjectProgress> Objects { get; set; } = new List<BlueprintObjectProgress>();
    public List<string> Blockers { get; set; } = new List<string>();
    public BlueprintSiteSnapshot? Site { get; set; }
    public string? FoundryPlanHash { get; set; }
    public FoundryConstructionPlan? FoundryPlan { get; set; }
}

public sealed class ConstructionControlReadback
{
    public string ExecutionKind { get; set; } = "structured_native";
    public string Phase { get; set; } = string.Empty;
    public bool DispatchDisabled { get; set; }
    public bool PhysicalIdleVerified { get; set; }
    public bool ExactBindingVerified { get; set; }
    public bool AnchorHeld { get; set; }
    public bool ReserveProofStillValid { get; set; }
    public DateTimeOffset ObservationLeaseExpiresAtUtc { get; set; }
    public long TailGameTicks { get; set; }
    public double TailAndNextTickEnergyJoules { get; set; }
    public string? StopReason { get; set; }
    public bool WholeBatchDebitSharedByObjectReceipts { get; set; } = true;
}

public sealed class BlueprintBuildList
{
    public List<BlueprintBuildProgress> Builds { get; set; } = new List<BlueprintBuildProgress>();
}
