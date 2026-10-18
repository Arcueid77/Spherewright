using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Errors;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    internal Spherewright.Plugin.RuntimeDescriptor.FlightCheckpointStore? VoyagerCheckpoints { get; set; }
    private VoyagerStarCatalogSnapshot? _lastVoyagerCatalog;
    private int _lastVoyagerCatalogLimit;
    private DateTimeOffset _lastVoyagerCatalogAtUtc;
    private PreviewVoyagerRouteRequest? _lastVoyagerRouteRequest;
    private VoyagerRouteBudgetSnapshot? _lastVoyagerRouteBudget;
    private DateTimeOffset _lastVoyagerRouteAtUtc;

    internal bool TryGetVoyagerRouteAnchor(string sessionId, int originPlanetId, string routeHash,
        out PreviewVoyagerRouteRequest? request, out VoyagerRouteBudgetSnapshot? budget)
    {
        request = _lastVoyagerRouteRequest;
        budget = _lastVoyagerRouteBudget;
        return request is not null && budget is not null && budget.BudgetSufficient
            && budget.SessionId == sessionId && budget.OriginPlanetId == originPlanetId
            && !string.IsNullOrWhiteSpace(routeHash) && budget.RouteStateHash == routeHash
            && DateTimeOffset.UtcNow - _lastVoyagerRouteAtUtc <= TimeSpan.FromSeconds(30);
    }

    internal GameCallResult<VoyagerRouteBudgetSnapshot> RefreshVoyagerBudgetOnMainThread(string sessionId,
        PreviewVoyagerRouteRequest inspected)
    {
        var request = CopyVoyagerRequest(inspected);
        var catalog = GetVoyagerStarCatalogOnMainThread(sessionId,
            new GetVoyagerStarCatalogRequest { PlanetId = request.PlanetId, Limit = 32 });
        if (!catalog.Success || catalog.Value is null) return GameCallResult<VoyagerRouteBudgetSnapshot>.Failed(catalog.Error!);
        var player = GetPlayerStateOnMainThread(sessionId, new Spherewright.Contracts.Sessions.LocalPlanetRequest { PlanetId = request.PlanetId });
        if (!player.Success || player.Value is null) return GameCallResult<VoyagerRouteBudgetSnapshot>.Failed(player.Error!);
        request.ExpectedCatalogStateHash = catalog.Value.StateHash;
        request.ExpectedPlayerStateHash = player.Value.StateHash;
        return PreviewVoyagerRouteOnMainThread(sessionId, request);
    }

    public GameCallResult<VoyagerStarCatalogSnapshot> GetVoyagerStarCatalogOnMainThread(
        string? requestedSessionId,
        GetVoyagerStarCatalogRequest request)
    {
        var accessError = ValidateOwnedPlanetOnMainThread(requestedSessionId, request.PlanetId, out _);
        if (accessError is not null) return GameCallResult<VoyagerStarCatalogSnapshot>.Failed(accessError);
        if (request.StateHashVersion != 1 || request.Limit < 1 || request.Limit > 32)
            return GameCallResult<VoyagerStarCatalogSnapshot>.Failed(InvalidRequest(
                "Voyager catalogue requires state-hash version 1 and a limit from 1 through 32.",
                "Use the bounded current request schema."));

        var player = GameMain.mainPlayer;
        var galaxy = GameMain.galaxy;
        var history = GameMain.history;
        var localStar = GameMain.localStar;
        if (player is null || galaxy?.stars is null || history is null || localStar is null)
            return GameCallResult<VoyagerStarCatalogSnapshot>.Failed(NotReady("The native owned star map is unavailable."));
        if (galaxy.stars.Length > 1024 || galaxy.stars.Any(star => star?.planets?.Length > 32))
            return GameCallResult<VoyagerStarCatalogSnapshot>.Failed(InvalidRequest(
                "The native catalogue exceeds its bounded star/planet scope.", "Use a supported finite catalogue scope."));

        var stars = galaxy.stars.Where(star => star is not null)
            .OrderBy(star => (star.uPosition - player.uPosition).sqrMagnitude)
            .ThenBy(star => star.id).Take(request.Limit)
            .Select(star => CaptureVoyagerStar(star, player, localStar.id, history.universeObserveLevel)).ToList();
        var snapshot = new VoyagerStarCatalogSnapshot
        {
            SessionId = _sessions.SessionId!, OriginPlanetId = request.PlanetId,
            OriginStarId = localStar.id, ObserveLevel = history.universeObserveLevel,
            CapturedAtGameTick = GameMain.gameTick, TotalStarCount = galaxy.starCount,
            Truncated = galaxy.starCount > stars.Count, Stars = stars,
        };
        // Navigation eligibility is stable across orbital ticks. Exact distance and
        // resource amounts remain separately timestamped observations, never a lease.
        snapshot.StateHash = CanonicalStateHash.Combine("voyager-catalog-navigation-v1", snapshot.SessionId,
            snapshot.OriginPlanetId, snapshot.ObserveLevel, request.Limit,
            string.Join("|", stars.Select(star => $"{star.StarId}:{star.RequiredObserveLevel}:{star.PlanetsVisible}:"
                + $"{star.ResourceVisibility}:{star.ResourceSummaryComplete}:"
                + string.Join(",", star.Planets?.Select(planet =>
                    $"{planet.PlanetId}:{planet.RequiredObserveLevel}:{planet.IsGasGiant}:{planet.ResourceVisibility}:{planet.ResourceSummaryComplete}")
                    ?? Enumerable.Empty<string>()))));
        _lastVoyagerCatalog = snapshot;
        _lastVoyagerCatalogLimit = request.Limit;
        _lastVoyagerCatalogAtUtc = DateTimeOffset.UtcNow;
        return GameCallResult<VoyagerStarCatalogSnapshot>.Succeeded(snapshot);
    }

    private static VoyagerStarSnapshot CaptureVoyagerStar(StarData star, Player player, int localStarId, int observeLevel)
    {
        var distance = (star.uPosition - player.uPosition).magnitude;
        var visibility = VoyagerVisibilityPolicy.EvaluateStar(observeLevel, star.id == localStarId, distance, false, false);
        var visible = VoyagerVisibilityPolicy.CanViewStarPlanets(observeLevel, star.id == localStarId, distance, star.hasAnyFactory);
        var observed = visibility.ObservationAllowed;
        // Run only the normal UI scan of a legally observable star. No hidden
        // resource pool or generator is queried to fill in an unknown result.
        if (observed && !star.scanned) star.RunScanningThread();
        var snapshot = new VoyagerStarSnapshot
        {
            StarId = star.id, Name = star.displayName, StarType = star.type.ToString(),
            UniversalPosition = new Spherewright.Contracts.Celestial.UniversalPositionSnapshot
                { X = star.uPosition.x, Y = star.uPosition.y, Z = star.uPosition.z },
            VisibleStarRadiusMeters = star.viewRadius,
            DistanceMeters = distance, DistanceLightYears = distance / 2400000d,
            RequiredObserveLevel = visibility.RequiredObserveLevel, PlanetsVisible = visible,
            ScanReady = observed && star.scanned,
        };
        if (visible)
            snapshot.Planets = (star.planets ?? Array.Empty<PlanetData>()).Where(planet => planet is not null)
                .OrderBy(planet => planet.id).Select(planet => CaptureVoyagerPlanet(planet, player, localStarId, observeLevel)).ToList();
        if (!observed || !star.scanned || star.planets is null
            || star.planets.Any(planet => planet is null || !planet.scanned
                || planet.runtimeVeinGroups is null || planet.runtimeVeinGroups.Length > 65536))
            return snapshot;
        long[] amounts = new long[64];
        int[] counts = new int[64];
        var amountComplete = star.CalcVeinAmounts(ref amounts, new HashSet<int>(), 0);
        var countComplete = star.CalcVeinCounts(ref counts, new HashSet<int>(), 0);
        if (!amountComplete || !countComplete) return snapshot;
        var allResources = CaptureVoyagerVeinSummary(amounts, counts);
        foreach (var planet in star.planets)
            if (!TryAddVoyagerGasAndOceanSummary(planet, allResources)) return snapshot;
        snapshot.Resources = allResources;
        snapshot.ResourceSummaryComplete = true;
        snapshot.ResourceSummaryScope = VoyagerVisibilityPolicy.CompleteSystemResourceSummaryScope;
        snapshot.ResourceVisibility = VoyagerResourceVisibility.Observed;
        return snapshot;
    }

    private static VoyagerPlanetSnapshot CaptureVoyagerPlanet(PlanetData planet, Player player, int localStarId, int observeLevel)
    {
        var distance = (planet.uPosition - player.uPosition).magnitude;
        var visibility = VoyagerVisibilityPolicy.EvaluatePlanet(observeLevel, planet.id == player.planetId,
            planet.star?.id == localStarId, planet.factory is not null, distance, false, false);
        var observed = visibility.ObservationAllowed;
        if (observed && !planet.scanned) planet.RunScanThread();
        var position = planet.uPosition;
        var snapshot = new VoyagerPlanetSnapshot
        {
            PlanetId = planet.id, StarId = planet.star?.id ?? 0, Name = planet.displayName,
            PlanetType = planet.type.ToString(), IsGasGiant = planet.type == EPlanetType.Gas,
            RealRadius = planet.realRadius, DistanceMeters = distance, DistanceLightYears = distance / 2400000d,
            UniversalPosition = new UniversalPositionSnapshot { X = position.x, Y = position.y, Z = position.z },
            RequiredObserveLevel = visibility.RequiredObserveLevel, ScanReady = observed && planet.scanned,
        };
        if (!observed || !planet.scanned || planet.runtimeVeinGroups is null || planet.runtimeVeinGroups.Length > 65536)
            return snapshot;
        long[] amounts = new long[64];
        int[] counts = new int[64];
        var amountComplete = planet.SummarizeVeinAmountsByFilter(ref amounts, new HashSet<int>(), 0);
        var countComplete = planet.SummarizeVeinCountsByFilter(ref counts, new HashSet<int>(), 0);
        if (!amountComplete || !countComplete) return snapshot;
        var resources = CaptureVoyagerVeinSummary(amounts, counts);
        if (!TryAddVoyagerGasAndOceanSummary(planet, resources)) return snapshot;
        snapshot.Resources = resources;
        snapshot.ResourceSummaryComplete = true;
        snapshot.ResourceSummaryScope = VoyagerVisibilityPolicy.CompleteSystemResourceSummaryScope;
        snapshot.ResourceVisibility = VoyagerResourceVisibility.Observed;
        return snapshot;
    }

    private static bool TryAddVoyagerGasAndOceanSummary(PlanetData planet, List<VoyagerResourceSummary> resources)
    {
        if (planet.type == EPlanetType.Gas)
        {
            if (planet.gasItems is null || planet.gasSpeeds is null || planet.gasItems.Length != planet.gasSpeeds.Length
                || planet.gasItems.Length > 64) return false;
            for (var index = 0; index < planet.gasItems.Length; index++)
            {
                if (planet.gasItems[index] <= 0 || float.IsNaN(planet.gasSpeeds[index])
                    || float.IsInfinity(planet.gasSpeeds[index]) || planet.gasSpeeds[index] < 0f) return false;
                resources.Add(new VoyagerResourceSummary { Kind = VoyagerResourceKinds.Gas,
                    ItemId = planet.gasItems[index], Name = GetItemName(planet.gasItems[index]), RatePerSecond = planet.gasSpeeds[index] });
            }
        }
        else if (planet.waterItemId > 0)
            resources.Add(new VoyagerResourceSummary { Kind = VoyagerResourceKinds.Ocean,
                ItemId = planet.waterItemId, Name = GetItemName(planet.waterItemId) });
        return true;
    }

    private static List<VoyagerResourceSummary> CaptureVoyagerVeinSummary(long[] amounts, int[] counts)
    {
        var rows = new List<VoyagerResourceSummary>();
        foreach (var vein in LDB.veins.dataArray.Where(vein => vein is not null).OrderBy(vein => vein.ID))
        {
            if (vein.ID <= 0 || vein.ID >= amounts.Length || vein.ID >= counts.Length) continue;
            rows.Add(new VoyagerResourceSummary { VeinType = vein.ID, ItemId = vein.MiningItem,
                Name = GetItemName(vein.MiningItem), Amount = amounts[vein.ID], VeinCount = counts[vein.ID] });
        }
        return rows;
    }

    public GameCallResult<VoyagerRouteBudgetSnapshot> PreviewVoyagerRouteOnMainThread(
        string? requestedSessionId,
        PreviewVoyagerRouteRequest request)
    {
        var accessError = ValidateOwnedPlanetOnMainThread(requestedSessionId, request.PlanetId, out _);
        if (accessError is not null) return GameCallResult<VoyagerRouteBudgetSnapshot>.Failed(accessError);
        var requestError = VoyagerRouteBudgetPolicy.ValidateRequest(request);
        if (requestError is not null)
            return GameCallResult<VoyagerRouteBudgetSnapshot>.Failed(InvalidRequest(requestError, "Use the finite current Voyager request schema."));
        if (request.StateHashVersion != 1 || _lastVoyagerCatalog is null
            || _lastVoyagerCatalog.SessionId != requestedSessionId
            || _lastVoyagerCatalog.OriginPlanetId != request.PlanetId
            || DateTimeOffset.UtcNow - _lastVoyagerCatalogAtUtc > TimeSpan.FromSeconds(30)
            || !string.Equals(request.ExpectedCatalogStateHash, _lastVoyagerCatalog.StateHash, StringComparison.Ordinal))
            return GameCallResult<VoyagerRouteBudgetSnapshot>.Failed(VoyagerStaleObservation(
                "Inspect a fresh bounded Voyager catalogue from this origin before requesting its route budget."));
        var limit = _lastVoyagerCatalogLimit;
        var catalogue = GetVoyagerStarCatalogOnMainThread(requestedSessionId,
            new GetVoyagerStarCatalogRequest { PlanetId = request.PlanetId, Limit = limit });
        if (!catalogue.Success || catalogue.Value is null)
            return GameCallResult<VoyagerRouteBudgetSnapshot>.Failed(catalogue.Error!);
        if (!string.Equals(request.ExpectedCatalogStateHash, catalogue.Value.StateHash, StringComparison.Ordinal))
            return GameCallResult<VoyagerRouteBudgetSnapshot>.Failed(VoyagerStaleObservation(
                "Native catalogue visibility changed after inspection; read the updated catalogue."));
        var observedPlayer = GetPlayerStateOnMainThread(requestedSessionId,
            new Spherewright.Contracts.Sessions.LocalPlanetRequest { PlanetId = request.PlanetId });
        if (!observedPlayer.Success || observedPlayer.Value is null)
            return GameCallResult<VoyagerRouteBudgetSnapshot>.Failed(observedPlayer.Error!);
        if (!string.Equals(request.ExpectedPlayerStateHash, observedPlayer.Value.StateHash, StringComparison.Ordinal))
            return GameCallResult<VoyagerRouteBudgetSnapshot>.Failed(VoyagerStaleObservation(
                "Player state changed after inspection; inspect the player and request a fresh budget."));

        var player = GameMain.mainPlayer;
        var mecha = player.mecha;
        var mappedDestination = catalogue.Value.Stars.Where(star => star.PlanetsVisible)
            .SelectMany(star => star.Planets ?? new List<VoyagerPlanetSnapshot>())
            .FirstOrDefault(planet => planet.PlanetId == request.DestinationPlanetId);
        var mappedStar = request.DestinationPlanetId == 0
            ? catalogue.Value.Stars.FirstOrDefault(star => star.StarId == request.DestinationStarId) : null;
        var destination = mappedDestination is null ? null : GameMain.galaxy?.PlanetById(mappedDestination.PlanetId);
        var destinationStar = mappedStar is null ? null : GameMain.galaxy?.StarById(mappedStar.StarId);
        var fuelEnergy = 0d;
        var minimumFuelRatio = 1d;
        var fuelAvailable = mecha.reactorEnergy > 0d;
        if (fuelAvailable)
        {
            var burningItem = LDB.items.Select(mecha.reactorItemId);
            minimumFuelRatio = IsOrdinaryVoyagerFuel(burningItem) ? Math.Min(1d, 1d + burningItem!.ReactorInc) : 0d;
        }
        var fuel = mecha.reactorStorage;
        var fuelGrids = fuel?.grids ?? Array.Empty<StorageComponent.GRID>();
        for (var index = 0; index < Math.Min(fuel?.size ?? 0, fuelGrids.Length); index++)
        {
            var grid = fuelGrids[index];
            var item = grid.itemId > 0 && grid.count > 0 ? LDB.items.Select(grid.itemId) : null;
            if (!IsOrdinaryVoyagerFuel(item))
            {
                if (grid.count > 0) minimumFuelRatio = 0d;
                continue;
            }
            fuelEnergy += item!.HeatValue * (double)grid.count;
            minimumFuelRatio = Math.Min(minimumFuelRatio, 1d + item.ReactorInc);
            fuelAvailable = true;
        }
        var input = new VoyagerRouteBudgetInput
        {
            SessionId = _sessions.SessionId!, OriginPlanetId = request.PlanetId,
            NativeVersion = _sessions.CaptureOnMainThread().GameVersion,
            OriginStarId = GameMain.localStar?.id ?? 0, DestinationPlanetId = request.DestinationPlanetId,
            DestinationStarId = mappedDestination?.StarId ?? mappedStar?.StarId ?? 0, CapturedAtGameTick = GameMain.gameTick,
            PlayerStateHash = observedPlayer.Value.StateHash, CatalogStateHash = catalogue.Value.StateHash,
            DestinationVisible = mappedDestination is not null || mappedStar is not null,
            DestinationIsStarOnly = request.DestinationPlanetId == 0,
            DestinationIsGasGiant = mappedDestination?.IsGasGiant ?? false,
            DistanceMeters = mappedDestination is not null && destination is not null
                ? Math.Max(0d, (destination.uPosition - player.uPosition).magnitude - destination.realRadius)
                : destinationStar is not null ? (destinationStar.uPosition - player.uPosition).magnitude : 0d,
            IsAlive = player.isAlive, IsGrounded = player.movementState == EMovementState.Walk && player.planetId == request.PlanetId,
            ControllerEnabled = player.controller is not null && GameMain.data is not null && !GameMain.data.disableController,
            PlayerOrderIdle = player.currentOrder is null,
            BuildUiIdle = player.controller is not null && NormalGameActionCoordinator.BuildUiIsIdle(player),
            NativeGuideRunning = GameMain.data?.guideRunning ?? true,
            ThrusterLevel = mecha.thrusterLevel, MaxSailSpeed = mecha.maxSailSpeed, MaxWarpSpeed = mecha.maxWarpSpeed,
            CoreEnergyJoules = mecha.coreEnergy, CoreCapacityJoules = mecha.coreEnergyCap,
            ReactorEnergyJoules = mecha.reactorEnergy, FuelStorageEnergyJoules = fuelEnergy,
            CoreGenerationLowerBoundWatts = Math.Max(0d, mecha.corePowerGen)
                + (fuelAvailable ? Math.Max(0d, mecha.reactorPowerGen * minimumFuelRatio) : 0d),
            WarpKeepingPowerPerSpeed = mecha.warpKeepingPowerPerSpeed,
            WarpStartPowerPerSpeed = mecha.warpStartPowerPerSpeed,
            EnergyConsumptionCoefficient = mecha.reactorPowerConsRatio,
            ThrustPowerPerAcceleration = mecha.thrustPowerPerAcc,
            NativeWarperCount = CountVoyagerItem(mecha.warpStorage, 1210),
            PackageWarperCount = CountVoyagerItem(player.package, 1210),
            AutoReplenishWarper = mecha.autoReplenishWarper,
        };
        if (!string.IsNullOrWhiteSpace(request.JourneyId))
        {
            var journey = VoyagerCheckpoints?.VoyagerJourney;
            var session = _sessions.CaptureOnMainThread();
            input.ReturnJourneyVerified = journey is not null && journey.JourneyId == request.JourneyId
                && journey.Phase == "remote_landed" && journey.OwnedSaveName == session.SaveName
                && journey.GameVersion == session.GameVersion && DateTimeOffset.UtcNow < journey.DeadlineUtc
                && journey.RemotePlanetId == request.PlanetId && journey.HomePlanetId == request.DestinationPlanetId
                && journey.HomeStarId == input.DestinationStarId && journey.DestinationStarId == input.OriginStarId
                && journey.LastLandedGameTick.HasValue && GameMain.gameTick >= journey.LastLandedGameTick.Value;
            if (input.ReturnJourneyVerified)
            {
                input.ReturnJourneyId = journey!.JourneyId;
                input.ReturnLegMaximumGameTicks = journey.ReturnReserveGameTicks;
                input.RemainingJourneyGameTicks = journey.MaximumGameTicks - journey.SpentGameTicks
                    - (GameMain.gameTick - journey.LastLandedGameTick!.Value);
                input.LockedReturnEnergyFloorJoules = journey.ReturnEnergyFloorJoules;
                input.LockedWithdrawalReserveJoules = journey.WithdrawalReserveJoules;
                input.LockedReturnCoreMinimumJoules = journey.ReturnCoreEnergyMinimumJoules;
                input.OriginalExtraWarperReserve = journey.ExtraWarperReserve;
            }
        }
        var evaluated = VoyagerRouteBudgetPolicy.Evaluate(input, request);
        _lastVoyagerRouteRequest = CopyVoyagerRequest(request);
        _lastVoyagerRouteBudget = evaluated;
        _lastVoyagerRouteAtUtc = DateTimeOffset.UtcNow;
        return GameCallResult<VoyagerRouteBudgetSnapshot>.Succeeded(evaluated);
    }

    internal static PreviewVoyagerRouteRequest CopyVoyagerRequest(PreviewVoyagerRouteRequest request) =>
        new PreviewVoyagerRouteRequest { JourneyId = request.JourneyId, PlanetId = request.PlanetId, DestinationPlanetId = request.DestinationPlanetId,
            DestinationStarId = request.DestinationStarId, StateHashVersion = request.StateHashVersion,
            ExpectedPlayerStateHash = request.ExpectedPlayerStateHash, ExpectedCatalogStateHash = request.ExpectedCatalogStateHash,
            MaximumFlightGameTicks = request.MaximumFlightGameTicks, ReturnReserveGameTicks = request.ReturnReserveGameTicks,
            MaximumWarpGameTicksPerLeg = request.MaximumWarpGameTicksPerLeg, MaximumArrivalOffsetMeters = request.MaximumArrivalOffsetMeters,
            MinimumCoreEnergyRatio = request.MinimumCoreEnergyRatio, ExtraWarperReserve = request.ExtraWarperReserve };

    private static BridgeError VoyagerStaleObservation(string message) => BridgeError.Create(
        BridgeErrorCodes.StaleState, message, true, "Inspect the current player and catalogue; do not reuse the stale observation.");

    private static bool IsOrdinaryVoyagerFuel(ItemProto? item) => item is not null
        && StorageComponent.itemIsFuel is not null && item.ID > 0 && item.ID < StorageComponent.itemIsFuel.Length
        && OrdinaryMechaFuelPolicy.IsAccepted(item.HeatValue, item.FuelType, StorageComponent.itemIsFuel[item.ID]);

    private static int CountVoyagerItem(StorageComponent? storage, int itemId)
    {
        if (storage?.grids is null || storage.size < 0 || storage.size > 8192 || storage.size > storage.grids.Length) return -1;
        long count = 0;
        for (var index = 0; index < storage.size; index++)
            if (storage.grids[index].itemId == itemId && storage.grids[index].count > 0) count += storage.grids[index].count;
        return count <= int.MaxValue ? (int)count : -1;
    }
}
