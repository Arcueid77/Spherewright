using Spherewright.Contracts.Celestial;

namespace Spherewright.Bridge.Core.Safety;

// Only copied native facts enter this policy. No Unity objects or inventory writes.
public sealed class VoyagerRouteBudgetInput
{
    public string SessionId { get; set; } = string.Empty;
    public string NativeVersion { get; set; } = string.Empty;
    public int OriginPlanetId { get; set; }
    public int OriginStarId { get; set; }
    public int DestinationPlanetId { get; set; }
    public int DestinationStarId { get; set; }
    public long CapturedAtGameTick { get; set; }
    public string PlayerStateHash { get; set; } = string.Empty;
    public string CatalogStateHash { get; set; } = string.Empty;
    public bool DestinationVisible { get; set; }
    public bool DestinationIsGasGiant { get; set; }
    public double DistanceMeters { get; set; }
    public bool IsAlive { get; set; }
    public bool IsGrounded { get; set; }
    public bool ControllerEnabled { get; set; }
    public bool PlayerOrderIdle { get; set; }
    public bool BuildUiIdle { get; set; }
    public bool NativeGuideRunning { get; set; }
    public int ThrusterLevel { get; set; }
    public double MaxSailSpeed { get; set; }
    public double MaxWarpSpeed { get; set; }
    public double CoreEnergyJoules { get; set; }
    public double CoreCapacityJoules { get; set; }
    public double ReactorEnergyJoules { get; set; }
    public double FuelStorageEnergyJoules { get; set; }
    public double CoreGenerationLowerBoundWatts { get; set; }
    public double WarpKeepingPowerPerSpeed { get; set; }
    public double WarpStartPowerPerSpeed { get; set; }
    public double EnergyConsumptionCoefficient { get; set; }
    public double ThrustPowerPerAcceleration { get; set; }
    public int NativeWarperCount { get; set; }
    public int PackageWarperCount { get; set; }
    public bool AutoReplenishWarper { get; set; }
}

public static class VoyagerRouteBudgetPolicy
{
    public const long MinimumFlightGameTicks = 7200;
    public const long MaximumFlightGameTicks = 108000;
    public const long MinimumLegGameTicks = 3600;
    public const int MaximumExtraWarperReserve = 18;
    public const float NativeWarpStateRisePerTick = 0.0055655558f;
    public const double TicksPerSecond = 60d;

    public static string? ValidateRequest(PreviewVoyagerRouteRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (request.PlanetId <= 0 || request.DestinationPlanetId <= 0 || request.PlanetId == request.DestinationPlanetId)
            return "The origin and destination must be different positive planet IDs.";
        if (request.StateHashVersion != 1 || string.IsNullOrWhiteSpace(request.ExpectedPlayerStateHash)
            || string.IsNullOrWhiteSpace(request.ExpectedCatalogStateHash))
            return "State-hash version 1 and both exact inspected state hashes are required.";
        if (request.MaximumFlightGameTicks < MinimumFlightGameTicks || request.MaximumFlightGameTicks > MaximumFlightGameTicks
            || request.ReturnReserveGameTicks < MinimumLegGameTicks
            || request.ReturnReserveGameTicks > request.MaximumFlightGameTicks - MinimumLegGameTicks)
            return "The whole return-trip horizon must be 7200 through 108000 game ticks, with at least 3600 ticks reserved for each leg.";
        if (!IsFinite(request.MinimumCoreEnergyRatio) || request.MinimumCoreEnergyRatio < 0.8d || request.MinimumCoreEnergyRatio > 1d)
            return "Minimum core-energy ratio must be finite and from 0.8 through 1.0.";
        if (request.ExtraWarperReserve < 0 || request.ExtraWarperReserve > MaximumExtraWarperReserve)
            return "Extra warper reserve must be from 0 through 18, in addition to the two normal leg entries.";
        return null;
    }

    public static VoyagerRouteBudgetSnapshot Evaluate(VoyagerRouteBudgetInput input, PreviewVoyagerRouteRequest request)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        if (request is null) throw new ArgumentNullException(nameof(request));
        var result = new VoyagerRouteBudgetSnapshot
        {
            SessionId = input.SessionId,
            OriginPlanetId = input.OriginPlanetId,
            OriginStarId = input.OriginStarId,
            DestinationPlanetId = input.DestinationPlanetId,
            DestinationStarId = input.DestinationStarId,
            CapturedAtGameTick = input.CapturedAtGameTick,
            PlayerStateHash = input.PlayerStateHash,
            CatalogStateHash = input.CatalogStateHash,
            DestinationVisible = input.DestinationVisible,
            DistanceMeters = FiniteOrZero(input.DistanceMeters),
            DistanceLightYears = FiniteOrZero(input.DistanceMeters / VoyagerVisibilityPolicy.MetersPerLightYear),
            ThrusterLevel = input.ThrusterLevel,
            MaxSailSpeed = FiniteOrZero(input.MaxSailSpeed),
            MaxWarpSpeed = FiniteOrZero(input.MaxWarpSpeed),
            CoreEnergyJoules = FiniteOrZero(input.CoreEnergyJoules),
            CoreCapacityJoules = FiniteOrZero(input.CoreCapacityJoules),
            MinimumCoreEnergyRatio = FiniteOrZero(request.MinimumCoreEnergyRatio),
            CoreGenerationLowerBoundWatts = FiniteOrZero(input.CoreGenerationLowerBoundWatts),
            NativeWarperCount = input.NativeWarperCount,
            PackageWarperCount = input.PackageWarperCount,
            AutoReplenishWarper = input.AutoReplenishWarper,
            MaximumFlightGameTicks = request.MaximumFlightGameTicks,
            ReturnReserveGameTicks = request.ReturnReserveGameTicks,
        };
        var invalidRequest = ValidateRequest(request);
        if (invalidRequest is not null)
        {
            Block(result, "invalid_voyager_request", invalidRequest);
            return result;
        }

        result.OutboundMaximumGameTicks = request.MaximumFlightGameTicks - request.ReturnReserveGameTicks;
        result.RequiredWarpers = 2 + request.ExtraWarperReserve;
        result.TechnologyAllowsWarp = input.ThrusterLevel >= 3;
        if (!result.TechnologyAllowsWarp)
            Block(result, "warp_technology_required", "Native mecha thrusterLevel must be at least 3 before a cross-star warp entry.");
        if (input.OriginPlanetId != request.PlanetId || input.DestinationPlanetId != request.DestinationPlanetId
            || input.OriginStarId <= 0 || input.DestinationStarId <= 0 || input.OriginStarId == input.DestinationStarId)
            Block(result, "cross_star_route_identity_invalid", "Voyager requires the exact origin and destination planets in different known star systems.");
        if (!input.DestinationVisible)
            Block(result, "destination_not_natively_visible", "Native map visibility does not yet expose this landing destination; hidden resource data cannot establish visibility.");
        if (input.DestinationIsGasGiant)
            Block(result, "destination_not_landable", "This bounded Voyager preview requires a normal solid-planet landing destination.");
        if (!string.Equals(input.PlayerStateHash, request.ExpectedPlayerStateHash, StringComparison.Ordinal))
            Block(result, "stale_player_state", "The live player state changed after inspection; inspect and preview again.");
        if (!string.Equals(input.CatalogStateHash, request.ExpectedCatalogStateHash, StringComparison.Ordinal))
            Block(result, "stale_catalog_state", "The legally visible catalogue changed after inspection; inspect and preview again.");
        if (!input.IsAlive || !input.IsGrounded || !input.ControllerEnabled)
            Block(result, "grounded_live_player_required", "Departure requires an alive, grounded player with the native controller enabled.");
        if (!input.PlayerOrderIdle || !input.BuildUiIdle)
            Block(result, "native_movement_channel_busy", "A native player order or build preview still owns the movement channel.");
        if (input.NativeGuideRunning)
            Block(result, "native_guide_energy_mode_active", "Tutorial flight changes native energy accounting; this ordinary-flight budget does not adopt it.");

        if (!InputsAreFiniteAndValid(input))
        {
            Block(result, "invalid_native_budget_inputs", "Native distance, speed, energy, fuel, generation, coefficients or warper counts are invalid or non-finite; no budget was calculated.");
            return result;
        }

        result.AvailableWarpers = (int)Math.Min(int.MaxValue,
            (long)input.NativeWarperCount + (input.AutoReplenishWarper ? input.PackageWarperCount : 0));
        result.WarperReserveSufficient = result.AvailableWarpers >= result.RequiredWarpers;
        if (!result.WarperReserveSufficient)
            Block(result, "warper_reserve_insufficient", "Real native warp-storage items plus proved native auto-replenishment must cover both leg entries and the declared extra reserve.");

        var startThreshold = input.WarpStartPowerPerSpeed * input.MaxWarpSpeed;
        var startPower = startThreshold * input.EnergyConsumptionCoefficient;
        var keepingPower = input.WarpKeepingPowerPerSpeed * input.MaxWarpSpeed * input.EnergyConsumptionCoefficient;
        var ramp = BuildNativeStartRamp();
        var startEnergy = startPower * ramp.Sum() / TicksPerSecond;
        // Existing same-star control supplies a declared local reserve. This is not a proof of a new landing controller.
        var localReservePerLeg = Math.Max(input.CoreCapacityJoules * 1.5d,
            3d * input.ThrustPowerPerAcceleration * input.EnergyConsumptionCoefficient * input.MaxSailSpeed * 2.5d);
        var withdrawalReserve = Math.Max(input.CoreCapacityJoules, startThreshold);
        var outbound = startEnergy + keepingPower * result.OutboundMaximumGameTicks / TicksPerSecond + localReservePerLeg;
        var returning = startEnergy + keepingPower * request.ReturnReserveGameTicks / TicksPerSecond + localReservePerLeg;
        var requiredEnergy = outbound + returning + withdrawalReserve;
        var availableEnergy = input.CoreEnergyJoules + input.ReactorEnergyJoules + input.FuelStorageEnergyJoules;
        var minimumLegTicks = Math.Ceiling(input.DistanceMeters / (input.MaxWarpSpeed + input.MaxSailSpeed) * TicksPerSecond);
        if (new[] { startThreshold, startPower, keepingPower, startEnergy, localReservePerLeg, withdrawalReserve,
                outbound, returning, requiredEnergy, availableEnergy, minimumLegTicks }.Any(value => !IsFinite(value))
            || minimumLegTicks > long.MaxValue / 2d)
        {
            Block(result, "native_budget_overflow", "Finite native inputs overflowed the route calculation; no partial budget is usable.");
            return result;
        }

        result.BudgetComputed = true;
        result.AvailableEnergyJoules = availableEnergy;
        result.RequiredEnergyJoules = requiredEnergy;
        result.OutboundEnergyBudgetJoules = outbound;
        result.ReturnEnergyBudgetJoules = returning;
        result.LandingReserveJoules = localReservePerLeg * 2d;
        result.WithdrawalReserveJoules = withdrawalReserve;
        result.WarpStartEnergyThresholdJoules = startThreshold;
        result.WarpStartEnergyUpperBoundJoules = startEnergy;
        result.WarpKeepingPowerUpperBoundWatts = keepingPower;
        result.MinimumFlightGameTicksAtMaximumSpeed = (long)minimumLegTicks * 2L;
        if (minimumLegTicks > result.OutboundMaximumGameTicks || minimumLegTicks > request.ReturnReserveGameTicks)
            Block(result, "route_exceeds_even_maximum_speed_horizon", "Even the optimistic maximum-speed lower bound exceeds a reserved leg; it cannot prove a safe route window.");

        result.NativeWarpEntryEnergySufficient = input.CoreEnergyJoules > startThreshold;
        if (!result.NativeWarpEntryEnergySufficient)
            Block(result, "native_warp_start_energy_insufficient", "The native entry gate requires current core energy strictly greater than warpStartPowerPerSpeed times maxWarpSpeed.");
        if (input.CoreEnergyJoules / input.CoreCapacityJoules + 1e-12d < request.MinimumCoreEnergyRatio)
            Block(result, "core_energy_ratio_insufficient", "Current core-energy ratio is below the declared departure minimum.");
        result.StoredEnergySufficient = availableEnergy >= requiredEnergy;
        if (!result.StoredEnergySufficient)
            Block(result, "fuel_energy_reserve_insufficient", "Current core, unspent reactor energy and already loaded native fuel do not cover outbound, return and withdrawal reserves.");

        var buffer = RequiredCoreBuffer(input, result.OutboundMaximumGameTicks, request.ReturnReserveGameTicks,
            startThreshold, startPower, keepingPower, ramp);
        result.CoreBufferRequiredJoules = buffer.Initial;
        result.CoreBufferSufficient = buffer.Peak <= input.CoreCapacityJoules && buffer.Initial <= input.CoreEnergyJoules;
        if (!result.CoreBufferSufficient)
            Block(result, "core_buffer_or_generation_insufficient", "Native core capacity, current charge and conservative replenishment rate cannot cover both finite warp envelopes and their strict entry gates.");
        result.RouteStateHash = BuildRouteStateHash(input, request);
        result.BudgetSufficient = result.Blockers.Count == 0;
        // Even a sufficient preview is tokenless and never grants movement or commit authority.
        return result;
    }

    private static (double Initial, double Peak) RequiredCoreBuffer(VoyagerRouteBudgetInput input,
        long outboundTicks, long returnTicks, double threshold, double startPower, double keepingPower, double[] ramp)
    {
        var needed = 0d;
        var peak = 0d;
        var generation = input.CoreGenerationLowerBoundWatts / TicksPerSecond;
        var strictEntry = threshold + Math.Max(1d, threshold * 1e-12d);
        // Backward reserve calculation spends before crediting regeneration: conservative for either native tick order.
        foreach (var legTicks in new[] { returnTicks, outboundTicks })
        {
            for (var tick = legTicks - 1L; tick >= 0L; tick--)
            {
                var starting = tick < ramp.Length ? startPower * ramp[(int)tick] : 0d;
                var demand = (keepingPower + starting) / TicksPerSecond;
                needed = demand + Math.Max(0d, needed - generation);
                peak = Math.Max(peak, needed);
            }
            needed = Math.Max(needed, strictEntry);
            peak = Math.Max(peak, needed);
        }
        return (needed, peak);
    }

    private static double[] BuildNativeStartRamp()
    {
        var values = new List<double>();
        var state = 0f;
        while (state < 1f)
        {
            values.Add(Math.Pow(1f - state, 3d));
            state = Math.Min(1f, state + NativeWarpStateRisePerTick);
        }
        return values.ToArray();
    }

    private static string BuildRouteStateHash(VoyagerRouteBudgetInput input, PreviewVoyagerRouteRequest request) =>
        CanonicalStateHash.Combine("voyager-route-budget-native-envelope-v1",
            input.SessionId, input.NativeVersion, input.OriginPlanetId, input.OriginStarId, input.DestinationPlanetId,
            input.DestinationStarId, input.CapturedAtGameTick, input.PlayerStateHash, input.CatalogStateHash,
            input.DestinationVisible, input.DestinationIsGasGiant, HashNumber(input.DistanceMeters),
            input.IsAlive, input.IsGrounded, input.ControllerEnabled, input.PlayerOrderIdle, input.BuildUiIdle,
            input.NativeGuideRunning, input.ThrusterLevel, HashNumber(input.MaxSailSpeed), HashNumber(input.MaxWarpSpeed),
            HashNumber(input.CoreEnergyJoules), HashNumber(input.CoreCapacityJoules), HashNumber(input.ReactorEnergyJoules),
            HashNumber(input.FuelStorageEnergyJoules), HashNumber(input.CoreGenerationLowerBoundWatts),
            HashNumber(input.WarpKeepingPowerPerSpeed), HashNumber(input.WarpStartPowerPerSpeed),
            HashNumber(input.EnergyConsumptionCoefficient), HashNumber(input.ThrustPowerPerAcceleration),
            input.NativeWarperCount, input.PackageWarperCount, input.AutoReplenishWarper,
            request.PlanetId, request.DestinationPlanetId, request.ExpectedPlayerStateHash, request.ExpectedCatalogStateHash,
            request.StateHashVersion, request.MaximumFlightGameTicks, request.ReturnReserveGameTicks,
            HashNumber(request.MinimumCoreEnergyRatio), request.ExtraWarperReserve);

    private static string HashNumber(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    private static bool InputsAreFiniteAndValid(VoyagerRouteBudgetInput input) =>
        Positive(input.DistanceMeters) && Positive(input.MaxSailSpeed) && Positive(input.MaxWarpSpeed)
        && Positive(input.CoreCapacityJoules) && Nonnegative(input.CoreEnergyJoules)
        && input.CoreEnergyJoules <= input.CoreCapacityJoules
        && Nonnegative(input.ReactorEnergyJoules) && Nonnegative(input.FuelStorageEnergyJoules)
        && Nonnegative(input.CoreGenerationLowerBoundWatts)
        && Positive(input.WarpKeepingPowerPerSpeed) && Positive(input.WarpStartPowerPerSpeed)
        && Positive(input.EnergyConsumptionCoefficient) && Positive(input.ThrustPowerPerAcceleration)
        && input.NativeWarperCount >= 0 && input.NativeWarperCount <= 20 && input.PackageWarperCount >= 0;

    private static void Block(VoyagerRouteBudgetSnapshot result, string code, string message) =>
        result.Blockers.Add(new VoyagerRouteBlocker { Code = code, Message = message });

    private static bool Positive(double value) => IsFinite(value) && value > 0d;
    private static bool Nonnegative(double value) => IsFinite(value) && value >= 0d;
    private static double FiniteOrZero(double value) => IsFinite(value) ? value : 0d;
    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
