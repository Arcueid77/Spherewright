namespace Spherewright.Contracts.Celestial;

public sealed class GetVoyagerStarCatalogRequest
{
    public int PlanetId { get; set; }
    public int Limit { get; set; } = 16;
    public int StateHashVersion { get; set; } = 1;
}

public sealed class VoyagerStarCatalogSnapshot
{
    public string Phase { get; set; } = "voyager_star_catalog_v1";
    public string SessionId { get; set; } = string.Empty;
    public int OriginPlanetId { get; set; }
    public int OriginStarId { get; set; }
    public int ObserveLevel { get; set; }
    public long CapturedAtGameTick { get; set; }
    public string StateHash { get; set; } = string.Empty;
    public int StateHashVersion { get; set; } = 1;
    public int TotalStarCount { get; set; }
    public bool Truncated { get; set; }
    public bool Prepared { get; set; }
    public bool Executable { get; set; }
    public bool CommitAllowedNow { get; set; }
    public List<VoyagerStarSnapshot> Stars { get; set; } = new List<VoyagerStarSnapshot>();
}

public sealed class VoyagerStarSnapshot
{
    public int StarId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StarType { get; set; } = string.Empty;
    public double DistanceMeters { get; set; }
    public double DistanceLightYears { get; set; }
    public int RequiredObserveLevel { get; set; }
    public bool PlanetsVisible { get; set; }
    public string ResourceVisibility { get; set; } = VoyagerResourceVisibility.Unknown;
    public bool ScanReady { get; set; }
    public bool ResourceSummaryComplete { get; set; }
    public string ResourceSummaryScope { get; set; } = "unknown";
    public List<VoyagerResourceSummary>? Resources { get; set; }
    public List<VoyagerPlanetSnapshot>? Planets { get; set; }
}

public sealed class VoyagerPlanetSnapshot
{
    public int PlanetId { get; set; }
    public int StarId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PlanetType { get; set; } = string.Empty;
    public bool IsGasGiant { get; set; }
    public float RealRadius { get; set; }
    public double DistanceMeters { get; set; }
    public double DistanceLightYears { get; set; }
    public UniversalPositionSnapshot UniversalPosition { get; set; } = new UniversalPositionSnapshot();
    public int RequiredObserveLevel { get; set; }
    public string ResourceVisibility { get; set; } = VoyagerResourceVisibility.Unknown;
    public bool ScanReady { get; set; }
    public bool ResourceSummaryComplete { get; set; }
    public string ResourceSummaryScope { get; set; } = "unknown";
    public List<VoyagerResourceSummary>? Resources { get; set; }
}

public static class VoyagerResourceVisibility
{
    public const string Unknown = "unknown";
    public const string Observed = "observed";
}

public static class VoyagerResourceKinds
{
    public const string Vein = "vein";
    public const string Gas = "gas";
    public const string Ocean = "ocean";
}

public sealed class VoyagerResourceSummary
{
    public string Kind { get; set; } = VoyagerResourceKinds.Vein;
    public int VeinType { get; set; }
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? Amount { get; set; }
    public int? VeinCount { get; set; }
    public double? RatePerSecond { get; set; }
}

public sealed class PreviewVoyagerRouteRequest
{
    public int PlanetId { get; set; }
    public int DestinationPlanetId { get; set; }
    public string ExpectedPlayerStateHash { get; set; } = string.Empty;
    public string ExpectedCatalogStateHash { get; set; } = string.Empty;
    public int StateHashVersion { get; set; } = 1;
    public long MaximumFlightGameTicks { get; set; } = 36000;
    public long ReturnReserveGameTicks { get; set; } = 18000;
    public double MinimumCoreEnergyRatio { get; set; } = 0.8d;
    public int ExtraWarperReserve { get; set; } = 1;
}

public sealed class VoyagerRouteBudgetSnapshot
{
    public string Phase { get; set; } = "voyager_route_budget_preview_v1";
    public string SessionId { get; set; } = string.Empty;
    public int OriginPlanetId { get; set; }
    public int OriginStarId { get; set; }
    public int DestinationPlanetId { get; set; }
    public int DestinationStarId { get; set; }
    public long CapturedAtGameTick { get; set; }
    public string RouteStateHash { get; set; } = string.Empty;
    public string PlayerStateHash { get; set; } = string.Empty;
    public string CatalogStateHash { get; set; } = string.Empty;
    public int StateHashVersion { get; set; } = 1;
    public bool Prepared { get; set; }
    public bool Executable { get; set; }
    public bool CommitAllowedNow { get; set; }
    public bool BudgetComputed { get; set; }
    public string BudgetBasis { get; set; } = "finite_horizon_native_warp_upper_bound_with_local_reserves_v1";
    public bool BudgetSufficient { get; set; }
    public bool TechnologyAllowsWarp { get; set; }
    public bool DestinationVisible { get; set; }
    public bool NativeWarpEntryEnergySufficient { get; set; }
    public bool WarperReserveSufficient { get; set; }
    public bool StoredEnergySufficient { get; set; }
    public bool CoreBufferSufficient { get; set; }
    public double DistanceMeters { get; set; }
    public double DistanceLightYears { get; set; }
    public int ThrusterLevel { get; set; }
    public double MaxSailSpeed { get; set; }
    public double MaxWarpSpeed { get; set; }
    public double CoreEnergyJoules { get; set; }
    public double CoreCapacityJoules { get; set; }
    public double MinimumCoreEnergyRatio { get; set; }
    public double AvailableEnergyJoules { get; set; }
    public double RequiredEnergyJoules { get; set; }
    public double OutboundEnergyBudgetJoules { get; set; }
    public double ReturnEnergyBudgetJoules { get; set; }
    public double LandingReserveJoules { get; set; }
    public double WithdrawalReserveJoules { get; set; }
    public double WarpStartEnergyThresholdJoules { get; set; }
    public double WarpStartEnergyUpperBoundJoules { get; set; }
    public double WarpKeepingPowerUpperBoundWatts { get; set; }
    public double CoreGenerationLowerBoundWatts { get; set; }
    public double CoreBufferRequiredJoules { get; set; }
    public string CoreBufferScope { get; set; } = "warp_entries_and_finite_warp_ticks_only";
    public bool LocalFlightReserveIsControllerProof { get; set; }
    public int NativeWarperCount { get; set; }
    public int PackageWarperCount { get; set; }
    public bool AutoReplenishWarper { get; set; }
    public int AvailableWarpers { get; set; }
    public int RequiredWarpers { get; set; }
    public long MaximumFlightGameTicks { get; set; }
    public long OutboundMaximumGameTicks { get; set; }
    public long ReturnReserveGameTicks { get; set; }
    public long? MinimumFlightGameTicksAtMaximumSpeed { get; set; }
    public List<VoyagerRouteBlocker> Blockers { get; set; } = new List<VoyagerRouteBlocker>();
}

public sealed class VoyagerRouteBlocker
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
