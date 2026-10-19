namespace Spherewright.Contracts.Actions;

public static class MovementFailureKinds
{
    public const string PositionStalled = "position_stalled";

    public const string RouteStalled = "route_stalled";

    public const string BoundedTimeout = "bounded_timeout";
}

public sealed class GetActionResultRequest
{
    public string ActionId { get; set; } = string.Empty;
}

public sealed class ActionResultSnapshot
{
    public string ActionId { get; set; } = string.Empty;

    public string ActionKind { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public bool Terminal { get; set; }

    public bool Succeeded { get; set; }

    public string? SessionId { get; set; }

    public int? PlanetId { get; set; }

    public string? Message { get; set; }

    public string? IdempotencyKey { get; set; }

    public long? StartedAtGameTick { get; set; }

    public long? CompletedAtGameTick { get; set; }

    public string? BeforeStateHash { get; set; }

    public string? AfterStateHash { get; set; }

    public int? TargetObjectId { get; set; }

    public List<int> TargetObjectIds { get; set; } = new List<int>();

    public int? TargetItemId { get; set; }

    public int? RequestedCount { get; set; }

    public int? BeforeTargetAmount { get; set; }

    public int? AfterTargetAmount { get; set; }

    public List<ActionItemDelta> ItemDeltas { get; set; } = new List<ActionItemDelta>();

    public UpgradeReadback? UpgradeReadback { get; set; }
    public ResearchQueueReadback? ResearchQueueReadback { get; set; }
    public StorageConfigurationReadback? StorageConfigurationReadback { get; set; }
    public MechaWarperSupplyReadback? MechaWarperSupplyReadback { get; set; }
    public VoyagerFlightReadback? VoyagerFlightReadback { get; set; }
    public string? BlueprintBuildId { get; set; }
    public string? BlueprintBuildPhase { get; set; }
    public List<Spherewright.Contracts.Factory.BlueprintObjectProgress>? BlueprintObjects { get; set; }
    public Spherewright.Contracts.Factory.ConstructionControlReadback? ConstructionControl { get; set; }

    public bool ReconciledFromOutcomeUnknown { get; set; }

    public long? ReconciledAtGameTick { get; set; }

    public string? FlightCheckpointId { get; set; }

    public string? FlightCheckpointReloadToken { get; set; }

    public long? FlightCheckpointGameTick { get; set; }

    public bool Stalled { get; set; }

    public bool RecoveryRequired { get; set; }

    public string? FailureKind { get; set; }

    public long? StalledGameTicks { get; set; }

    public double? RemainingDistance { get; set; }

    public bool DoNotRetrySameTarget { get; set; }

    public string? RecommendedRecovery { get; set; }

    public double? RecommendedShortMoveDistanceMeters { get; set; }

    public double? OrthogonalProbeDistanceMeters { get; set; }

    public int? MaximumOrthogonalProbeAttempts { get; set; }
}
