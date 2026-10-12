using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Contracts.Actions;

public static class NormalActionKinds
{
    public const string Move = "move";
    public const string InterplanetaryFlight = "interplanetary-flight";
    public const string Harvest = "harvest";
    public const string Handcraft = "handcraft";
    public const string SelectResearch = "select-research";
    public const string Build = "build";
    public const string Dismantle = "dismantle";
    public const string Upgrade = "upgrade";
    public const string BlueprintBuild = "blueprint-build";
    public const string CancelBlueprintBuild = "cancel-blueprint-build";
    public const string Transfer = "transfer";
    public const string LogisticsStationFleetTransfer = "logistics-station-fleet-transfer";
    public const string ConfigureBuilding = "configure-building";
    public const string Refuel = "refuel";
    public const string Save = "save";
    public const string ReconcileQuarantine = "reconcile-quarantine";
    public const string UserSaveImport = "user-save-import";
}

public static class NormalBuildKinds
{
    public const string Core = "core";
    public const string Resource = "resource";
    public const string Belt = "belt";
    public const string Inserter = "inserter";
}

public static class TransferDirections
{
    public const string PlayerToStorage = "player-to-storage";
    public const string StorageToPlayer = "storage-to-player";
}

public static class LogisticsStationFleetTransferDirections
{
    public const string PlayerToStation = "player-to-station";
    public const string StationToPlayer = "station-to-player";
}

public static class LogisticsFleetItemIds
{
    public const int Drone = 5001;
    public const int Vessel = 5002;
}

public static class BuildingConfigurationModes
{
    public const string Production = "production";
    public const string Research = "research";
    public const string SorterFilter = "sorter-filter";
    public const string StorageCapacity = "storage-capacity";
    public const string LogisticsStationStorage = "logistics-station-storage";
    public const string LogisticsStationBelt = "logistics-station-belt";
    public const string LogisticsStationCharge = "logistics-station-charge";
}

public static class StorageConfigurationOperations
{
    public const string SetBans = "set-bans";
    public const string LockOccupied = "lock-occupied";
    public const string FilterEmptyOrMatching = "filter-empty-or-matching";
    public const string ClearFilters = "clear-filters";
}

public static class LogisticsStorageLogics
{
    public const string None = "none";
    public const string Supply = "supply";
    public const string Demand = "demand";
}

public static class NormalActionStates
{
    public const string Reserved = "reserved";
    public const string Queued = "queued";
    public const string Executing = "executing";
    public const string WaitingForGame = "waiting_for_game";
    public const string Completed = "completed";
    public const string ActionFailed = "action_failed";
    public const string RecoveryRequired = "recovery_required";
    public const string OutcomeUnknown = "outcome_unknown";
}

public sealed class PrepareMoveRequest
{
    public int PlanetId { get; set; }

    public Vector3Snapshot Target { get; set; } = new Vector3Snapshot();

    public float ArrivalTolerance { get; set; } = 1.5f;

    public string ExpectedPlayerStateHash { get; set; } = string.Empty;

    public int StateHashVersion { get; set; } = 1;

    public bool AllowPassiveDrift { get; set; }
}

public sealed class PrepareHarvestRequest
{
    public int PlanetId { get; set; }

    public string ResourceKind { get; set; } = string.Empty;

    public int NodeId { get; set; }

    public int RequestedYieldCount { get; set; } = 1;

    public string ExpectedResourceStateHash { get; set; } = string.Empty;

    public string ExpectedPlayerStateHash { get; set; } = string.Empty;

    public int StateHashVersion { get; set; } = 1;
}

public sealed class PrepareHandcraftRequest
{
    public int PlanetId { get; set; }

    public int RecipeId { get; set; }

    public int Count { get; set; } = 1;

    public string ExpectedPlayerStateHash { get; set; } = string.Empty;

    public int StateHashVersion { get; set; } = 1;
}

public sealed class PrepareSelectResearchRequest
{
    public int PlanetId { get; set; }

    public int TechId { get; set; }

    public string ExpectedSelectionStateHash { get; set; } = string.Empty;

    public bool PrioritizeQueued { get; set; }

    public int StateHashVersion { get; set; } = 1;
}

public sealed class PrepareBuildRequest
{
    /// <summary>Explicit bounded multispan READ-ONLY qualification, never a build plan.</summary>
    public BeltRoutePreviewRequest? BeltRoutePreview { get; set; }
    /// <summary>Optional read-only qualification of exact existing endpoints against one NEW belt span. Never creates a commit capability.</summary>
    public BeltEndpointPreviewRequest? BeltEndpointPreview { get; set; }

    public int PlanetId { get; set; }

    public int BuildingItemId { get; set; }

    /// <summary>Native initial filter for ordinary2011/2012 sorters; zero is unfiltered.</summary>
    public int InitialSorterFilterItemId { get; set; }

    public float PreferredDistance { get; set; } = 12f;

    public Vector3Snapshot? PreferredPosition { get; set; }

    public float? PreferredYaw { get; set; }

    public int? ResourceNodeId { get; set; }

    public string? ExpectedResourceStateHash { get; set; }

    public int? SourceObjectId { get; set; }

    public string? ExpectedSourceStateHash { get; set; }

    public int? DestinationObjectId { get; set; }

    public string? ExpectedDestinationStateHash { get; set; }

    public Vector3Snapshot? PathEnd { get; set; }

    public float PathLength { get; set; } = 6f;

    /// <summary>Default grid routing; geodesic is an explicit bounded free-ground or dual empty-cover belt subset.</summary>
    public string BeltPathMode { get; set; } = BeltPathModes.NativeGrid;

    /// <summary>Explicit native layer numbers; only native_elevated_grid accepts these fields.</summary>
    public int? BeltStartAltitudeLevel { get; set; }

    public int? BeltEndAltitudeLevel { get; set; }

    public string ExpectedPlayerStateHash { get; set; } = string.Empty;

    public int StateHashVersion { get; set; } = 1;
}

public sealed class PrepareConfigureBuildingRequest
{
    public int PlanetId { get; set; }

    public int EntityId { get; set; }

    public int RecipeId { get; set; }

    public string Mode { get; set; } = BuildingConfigurationModes.Production;

    public int TechId { get; set; }

    public int FilterItemId { get; set; }

    public string StorageOperation { get; set; } = string.Empty;

    public int StorageBannedGridCount { get; set; } = -1;

    public int StationStorageIndex { get; set; } = -1;

    public int StationBeltSlotIndex { get; set; } = -1;

    public int StationBeltStorageIndex { get; set; } = -1;

    public int StationItemId { get; set; }

    public int StationMaximumCount { get; set; }

    public string StationLocalLogic { get; set; } = LogisticsStorageLogics.None;

    public string StationRemoteLogic { get; set; } = LogisticsStorageLogics.None;

    public long StationMaximumChargePowerWatts { get; set; }

    public string ExpectedStationConfigurationStateHash { get; set; } = string.Empty;

    public string ExpectedFactoryStateHash { get; set; } = string.Empty;

    public int StateHashVersion { get; set; } = 1;
}

public sealed class PrepareTransferRequest
{
    public int PlanetId { get; set; }

    public string Direction { get; set; } = string.Empty;

    public int StorageEntityId { get; set; }

    public int ItemId { get; set; }

    public int Count { get; set; } = 1;

    public string ExpectedPlayerStateHash { get; set; } = string.Empty;

    public string ExpectedStorageStateHash { get; set; } = string.Empty;

    public int StateHashVersion { get; set; } = 1;
}

public sealed class PrepareRefuelRequest
{
    public int PlanetId { get; set; }

    public int ItemId { get; set; }

    public int Count { get; set; } = 1;

    public string ExpectedPlayerStateHash { get; set; } = string.Empty;

    public int StateHashVersion { get; set; } = 1;
}

public sealed class PrepareSaveRequest
{
    public int PlanetId { get; set; }

    public long ExpectedRevision { get; set; }

    public int StateHashVersion { get; set; } = 1;
}

public sealed class PrepareDismantleRequest
{
    public int PlanetId { get; set; }

    public int ObjectId { get; set; }

    public string ExpectedEndpointStateHash { get; set; } = string.Empty;

    public string ExpectedPlayerStateHash { get; set; } = string.Empty;

    public int StateHashVersion { get; set; } = 1;
}

public sealed class PrepareLogisticsStationFleetTransferRequest
{
    public int PlanetId { get; set; }

    public int StationEntityId { get; set; }

    public string Direction { get; set; } = string.Empty;

    public int ItemId { get; set; }

    public int Count { get; set; } = 1;

    public string ExpectedPlayerStateHash { get; set; } = string.Empty;

    public string ExpectedStationFleetStateHash { get; set; } = string.Empty;

    public int StateHashVersion { get; set; } = 1;
}

public sealed class PrepareInterplanetaryFlightRequest
{
    public int PlanetId { get; set; }

    public int DestinationPlanetId { get; set; }

    public string ExpectedPlayerStateHash { get; set; } = string.Empty;

    public string ExpectedStarSystemStateHash { get; set; } = string.Empty;

    public double MinimumCoreEnergyRatio { get; set; } = 0.95d;

    public int StateHashVersion { get; set; } = 1;
}

public sealed class PrepareQuarantineReconciliationRequest
{
    public int PlanetId { get; set; }

    public string ActionId { get; set; } = string.Empty;

    public long ExpectedRevision { get; set; }

    public int StateHashVersion { get; set; } = 1;
}

public sealed class CommitNormalActionRequest
{
    public string SessionId { get; set; } = string.Empty;

    public int PlanetId { get; set; }

    public string PlanToken { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class PreparedNormalAction
{
    public BeltRoutePreviewSnapshot? BeltRoutePreview { get; set; }
    public MoveStateBindingSnapshot? MoveStateBinding { get; set; }

    public BeltEndpointPreviewSnapshot? BeltEndpointPreview { get; set; }

    // Null on older plugins and non-Move plans; absence never means a dry/clear route.
    public MovementSurfacePreview? SurfacePreview { get; set; }

    public string? PlannedStorageOperation { get; set; }

    public StorageConfigurationSnapshot? PlannedStorageConfiguration { get; set; }

    public BlueprintBuildProgress? BlueprintBuild { get; set; }
    public bool Prepared { get; set; }

    public string ActionKind { get; set; } = string.Empty;

    public string PlanToken { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string ExpectedStateHash { get; set; } = string.Empty;

    public int StateHashVersion { get; set; } = 1;

    public bool CommitAllowedNow { get; set; }

    public double EstimatedDistance { get; set; }

    public long EstimatedGameTicks { get; set; }

    public List<ActionItemBudget> ItemBudget { get; set; } = new List<ActionItemBudget>();

    public List<WriteBlocker> CommitBlockers { get; set; } = new List<WriteBlocker>();

    public string CompletionCondition { get; set; } = string.Empty;

    public Vector3Snapshot? PlannedPosition { get; set; }

    public float? PlannedYaw { get; set; }

    public string? BuildKind { get; set; }

    /// <summary>Null for non-sorter builds; zero explicitly means unfiltered.</summary>
    public int? PlannedSorterFilterItemId { get; set; }

    public InserterAttachmentPlanSnapshot? PlannedInserterAttachment { get; set; }

    public BeltPathPlanSnapshot? PlannedBeltPath { get; set; }

    public int? TargetObjectId { get; set; }

    public int? SourceObjectId { get; set; }

    public int? DestinationObjectId { get; set; }

    public List<Vector3Snapshot> PlannedPath { get; set; } = new List<Vector3Snapshot>();

    public List<int> PlannedResourceNodeIds { get; set; } = new List<int>();

    public string? ReconcilesActionId { get; set; }

    public List<int> ProvedObjectIds { get; set; } = new List<int>();
}

public sealed class ActionItemBudget
{
    public int ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Count { get; set; }

    public string Direction { get; set; } = string.Empty;
}

public sealed class NormalActionCommitResult
{
    public string ActionId { get; set; } = string.Empty;

    public string ActionKind { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public bool Accepted { get; set; }

    public bool IdempotentReplay { get; set; }
}

public sealed class ActionItemDelta
{
    public int ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int BeforeCount { get; set; }

    public int AfterCount { get; set; }

    public int Delta { get; set; }
}
