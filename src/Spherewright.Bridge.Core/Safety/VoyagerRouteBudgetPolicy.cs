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
    public bool DestinationIsStarOnly { get; set; }
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
    // Copied only after the reader verifies the protected active journey,
    // its original remote/home direction, current identity and deadline.
    public bool ReturnJourneyVerified { get; set; }
    public string ReturnJourneyId { get; set; } = string.Empty;
    public long ReturnLegMaximumGameTicks { get; set; }
    public long RemainingJourneyGameTicks { get; set; }
    public double LockedReturnEnergyFloorJoules { get; set; }
    public double LockedWithdrawalReserveJoules { get; set; }
    public double LockedReturnCoreMinimumJoules { get; set; }
    public int OriginalExtraWarperReserve { get; set; }
}

public static class VoyagerRouteBudgetPolicy
{
    public const long MinimumFlightGameTicks = 7200;
    public const long MaximumFlightGameTicks = 108000;
    public const long MinimumLegGameTicks = 3600;
    public const int MaximumExtraWarperReserve = 18;
    public const long MinimumWarpGameTicksPerLeg = 180;
    public const double MinimumArrivalOffsetMeters = 100000d;
    public const double MaximumArrivalOffsetMeters = 3000000d;
    public const float NativeWarpStateRisePerTick = 0.0055655558f;
    public const double TicksPerSecond = 60d;

    public static string? ValidateRequest(PreviewVoyagerRouteRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        var isReturn = !string.IsNullOrEmpty(request.JourneyId);
        if (isReturn && !Guid.TryParse(request.JourneyId, out _))
            return "A return must name the exact protected journey identifier.";
        if (request.PlanetId <= 0 || request.DestinationPlanetId < 0 || request.DestinationStarId < 0
            || (request.DestinationPlanetId == 0 && request.DestinationStarId <= 0)
            || request.PlanetId == request.DestinationPlanetId)
            return "Use a positive origin planet and either a different positive destination planet or an explicit positive destination star with planet zero.";
        if (request.StateHashVersion != 1 || string.IsNullOrWhiteSpace(request.ExpectedPlayerStateHash)
            || string.IsNullOrWhiteSpace(request.ExpectedCatalogStateHash))
            return "State-hash version 1 and both exact inspected state hashes are required.";
        if (!isReturn && (request.MaximumFlightGameTicks < MinimumFlightGameTicks || request.MaximumFlightGameTicks > MaximumFlightGameTicks
            || request.ReturnReserveGameTicks < MinimumLegGameTicks
            || request.ReturnReserveGameTicks > request.MaximumFlightGameTicks - MinimumLegGameTicks))
            return "The whole return-trip horizon must be 7200 through 108000 game ticks, with at least 3600 ticks reserved for each leg.";
        if (request.MaximumWarpGameTicksPerLeg < MinimumWarpGameTicksPerLeg
            || request.MaximumWarpGameTicksPerLeg > (isReturn ? MaximumFlightGameTicks
                : Math.Min(request.MaximumFlightGameTicks - request.ReturnReserveGameTicks, request.ReturnReserveGameTicks)))
            return "Each native warp interval must be at least 180 ticks and fit inside both explicitly reserved leg horizons.";
        if (!IsFinite(request.MaximumArrivalOffsetMeters) || request.MaximumArrivalOffsetMeters < MinimumArrivalOffsetMeters
            || request.MaximumArrivalOffsetMeters > MaximumArrivalOffsetMeters)
            return "The declared maximum arrival offset must be finite and from 100000 through 3000000 game meters.";
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
        var isReturn = !string.IsNullOrEmpty(request.JourneyId);
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
            DestinationIsStarOnly = input.DestinationIsStarOnly,
            IsReturnLeg = isReturn,
            BudgetBasis = isReturn ? "finite_native_single_return_and_locked_journey_v3"
                : "finite_native_warp_duration_and_explicit_return_recharge_v2",
            RequiresReturnCoreRecharge = !isReturn,
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
            MaximumFlightGameTicks = isReturn ? input.RemainingJourneyGameTicks : request.MaximumFlightGameTicks,
            ReturnReserveGameTicks = isReturn ? 0L : request.ReturnReserveGameTicks,
            MaximumWarpGameTicksPerLeg = request.MaximumWarpGameTicksPerLeg,
            MaximumArrivalOffsetMeters = FiniteOrZero(request.MaximumArrivalOffsetMeters),
        };
        var invalidRequest = ValidateRequest(request);
        if (invalidRequest is not null)
        {
            Block(result, "invalid_voyager_request", invalidRequest);
            return result;
        }

        if (isReturn)
        {
            if (!input.ReturnJourneyVerified || !string.Equals(request.JourneyId, input.ReturnJourneyId, StringComparison.Ordinal))
                Block(result, "return_journey_not_verified", "The exact active protected journey, unexpired deadline and original remote/home direction must be freshly verified by the native reader.");
            if (input.ReturnLegMaximumGameTicks < MinimumLegGameTicks
                || input.RemainingJourneyGameTicks < input.ReturnLegMaximumGameTicks
                || input.RemainingJourneyGameTicks > MaximumFlightGameTicks
                || input.OriginalExtraWarperReserve < 0 || input.OriginalExtraWarperReserve > MaximumExtraWarperReserve
                || !Positive(input.LockedReturnEnergyFloorJoules) || !Positive(input.LockedWithdrawalReserveJoules)
                || !Positive(input.LockedReturnCoreMinimumJoules)
                || request.MaximumWarpGameTicksPerLeg > input.ReturnLegMaximumGameTicks)
            {
                Block(result, "return_journey_reserve_invalid", "The protected remaining horizon, single return-leg limit and finite energy/core/extra-warper floors cannot be replaced by client declarations.");
                return result;
            }
        }
        result.OutboundMaximumGameTicks = isReturn ? input.ReturnLegMaximumGameTicks
            : request.MaximumFlightGameTicks - request.ReturnReserveGameTicks;
        result.RequiredWarpers = isReturn ? 1 + Math.Max(request.ExtraWarperReserve, input.OriginalExtraWarperReserve)
            : 2 + request.ExtraWarperReserve;
        result.TechnologyAllowsWarp = input.ThrusterLevel >= 3;
        if (!result.TechnologyAllowsWarp)
            Block(result, "warp_technology_required", "Native mecha thrusterLevel must be at least 3 before a cross-star warp entry.");
        if (input.OriginPlanetId != request.PlanetId || input.DestinationPlanetId != request.DestinationPlanetId
            || input.OriginStarId <= 0 || input.DestinationStarId <= 0 || input.OriginStarId == input.DestinationStarId
            || input.DestinationIsStarOnly != (request.DestinationPlanetId == 0)
            || (request.DestinationStarId > 0 && input.DestinationStarId != request.DestinationStarId)
            || (input.DestinationIsStarOnly && input.DestinationIsGasGiant))
            Block(result, "cross_star_route_identity_invalid", "Bind the exact origin and either its legally visible destination planet or an explicit different target star, without inventing hidden planets.");
        if (!input.DestinationVisible)
            Block(result, "destination_not_natively_visible", "The target star or explicit planet is not legally visible; hidden data cannot establish destination visibility.");
        if (!input.DestinationIsStarOnly && input.DestinationIsGasGiant)
            Block(result, "destination_not_landable", "An explicitly selected planet must be a normal solid-planet landing destination.");
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
            Block(result, "warper_reserve_insufficient", isReturn
                ? "Native available warpers must cover the single original return entry and the larger original/requested extra reserve."
                : "Real native warp-storage items plus proved native auto-replenishment must cover both leg entries and the declared extra reserve.");

        var startThreshold = input.WarpStartPowerPerSpeed * input.MaxWarpSpeed;
        var startPower = startThreshold * input.EnergyConsumptionCoefficient;
        var keepingPower = input.WarpKeepingPowerPerSpeed * input.MaxWarpSpeed * input.EnergyConsumptionCoefficient;
        var ramp = BuildNativeStartRamp();
        var startEnergy = startPower * ramp.Sum() / TicksPerSecond;
        // Existing same-star control supplies a declared local reserve. This is not a proof of a new landing controller.
        var localReservePerLeg = Math.Max(input.CoreCapacityJoules * 1.5d,
            3d * input.ThrustPowerPerAcceleration * input.EnergyConsumptionCoefficient * input.MaxSailSpeed * 2.5d);
        var withdrawalReserve = Math.Max(input.CoreCapacityJoules, startThreshold);
        if (isReturn) withdrawalReserve = Math.Max(withdrawalReserve, input.LockedWithdrawalReserveJoules);
        // The whole leg also includes local launch/approach/landing; it is not spent entirely in warp.
        // The controller must enforce this separately locked finite native-warp duration.
        var outbound = startEnergy + keepingPower * request.MaximumWarpGameTicksPerLeg / TicksPerSecond + localReservePerLeg;
        var returning = isReturn ? 0d : outbound;
        var requiredEnergy = outbound + returning + withdrawalReserve;
        if (isReturn) requiredEnergy = Math.Max(requiredEnergy, input.LockedReturnEnergyFloorJoules);
        var availableEnergy = input.CoreEnergyJoules + input.ReactorEnergyJoules + input.FuelStorageEnergyJoules;
        // A star-only target declares an arrival envelope, not a preselected hidden planet.
        var distanceEnvelope = input.DistanceMeters + (input.DestinationIsStarOnly ? request.MaximumArrivalOffsetMeters : 0d);
        var maximumSpeed = input.MaxWarpSpeed + input.MaxSailSpeed;
        var minimumLegTicks = Math.Ceiling(distanceEnvelope / maximumSpeed * TicksPerSecond);
        if (new[] { startThreshold, startPower, keepingPower, startEnergy, localReservePerLeg, withdrawalReserve,
                outbound, returning, requiredEnergy, availableEnergy, distanceEnvelope, maximumSpeed, minimumLegTicks }.Any(value => !IsFinite(value))
            || minimumLegTicks >= long.MaxValue / 2d)
        {
            Block(result, "native_budget_overflow", "Finite native inputs overflowed the route calculation; no partial budget is usable.");
            return result;
        }

        var buffer = RequiredCoreBuffer(input, request.MaximumWarpGameTicksPerLeg,
            startThreshold, startPower, keepingPower, ramp);
        var returnCoreMinimum = Math.Max(request.MinimumCoreEnergyRatio * input.CoreCapacityJoules,
            Math.Max(buffer.Initial, StrictEntryFloor(startThreshold)));
        if (isReturn) returnCoreMinimum = Math.Max(returnCoreMinimum, input.LockedReturnCoreMinimumJoules);
        if (!IsFinite(buffer.Initial) || !IsFinite(buffer.Peak) || !IsFinite(returnCoreMinimum))
        {
            Block(result, "native_budget_overflow", "The single-leg core/recharge calculation overflowed; no partial budget is usable.");
            return result;
        }
        result.BudgetComputed = true;
        result.AvailableEnergyJoules = availableEnergy;
        result.RequiredEnergyJoules = requiredEnergy;
        result.OutboundEnergyBudgetJoules = outbound;
        result.ReturnEnergyBudgetJoules = returning;
        result.LandingReserveJoules = localReservePerLeg * (isReturn ? 1d : 2d);
        result.WithdrawalReserveJoules = withdrawalReserve;
        result.WarpStartEnergyThresholdJoules = startThreshold;
        result.WarpStartEnergyUpperBoundJoules = startEnergy;
        result.WarpKeepingPowerUpperBoundWatts = keepingPower;
        result.MinimumFlightGameTicksAtMaximumSpeed = (long)minimumLegTicks * (isReturn ? 1L : 2L);
        if (minimumLegTicks > result.OutboundMaximumGameTicks || (!isReturn && minimumLegTicks > request.ReturnReserveGameTicks))
            Block(result, "route_exceeds_even_maximum_speed_horizon", "Even the optimistic maximum-speed time for the declared distance envelope exceeds a whole reserved leg.");
        if (minimumLegTicks > request.MaximumWarpGameTicksPerLeg)
            Block(result, "route_exceeds_finite_warp_duration", "Even the optimistic maximum-speed time for the declared distance envelope exceeds the locked native-warp duration; it cannot prove safe travel time.");

        result.NativeWarpEntryEnergySufficient = input.CoreEnergyJoules > startThreshold;
        if (!result.NativeWarpEntryEnergySufficient)
            Block(result, "native_warp_start_energy_insufficient", "The native entry gate requires current core energy strictly greater than warpStartPowerPerSpeed times maxWarpSpeed.");
        if (input.CoreEnergyJoules / input.CoreCapacityJoules + 1e-12d < request.MinimumCoreEnergyRatio)
            Block(result, "core_energy_ratio_insufficient", "Current core-energy ratio is below the declared departure minimum.");
        result.StoredEnergySufficient = availableEnergy >= requiredEnergy;
        if (!result.StoredEnergySufficient)
            Block(result, "fuel_energy_reserve_insufficient", "Current core, unspent reactor energy and already loaded native fuel do not cover outbound, return and withdrawal reserves.");

        result.CoreBufferRequiredJoules = buffer.Initial;
        result.ReturnCoreEnergyMinimumJoules = returnCoreMinimum;
        result.CoreBufferSufficient = buffer.Peak <= input.CoreCapacityJoules && buffer.Initial <= input.CoreEnergyJoules
            && returnCoreMinimum <= input.CoreCapacityJoules && (!isReturn || input.CoreEnergyJoules >= returnCoreMinimum);
        if (!result.CoreBufferSufficient)
            Block(result, "core_buffer_or_generation_insufficient", "Native core capacity, current charge or conservative generation cannot sustain one finite warp leg and its declared return-recharge minimum.");
        result.RouteStateHash = BuildRouteStateHash(input, request);
        result.BudgetSufficient = result.Blockers.Count == 0;
        // Even a sufficient preview is tokenless and never grants movement or commit authority.
        return result;
    }

    private static (double Initial, double Peak) RequiredCoreBuffer(VoyagerRouteBudgetInput input,
        long warpTicks, double threshold, double startPower, double keepingPower, double[] ramp)
    {
        var needed = 0d;
        var peak = 0d;
        var generation = input.CoreGenerationLowerBoundWatts / TicksPerSecond;
        // Spend before crediting generation: conservative for either native tick order.
        // A mandatory fresh core-recharge/readback gate separates the two identical leg envelopes.
        for (var tick = warpTicks - 1L; tick >= 0L; tick--)
        {
            var starting = tick < ramp.Length ? startPower * ramp[(int)tick] : 0d;
            var demand = (keepingPower + starting) / TicksPerSecond;
            needed = demand + Math.Max(0d, needed - generation);
            peak = Math.Max(peak, needed);
        }
        needed = Math.Max(needed, StrictEntryFloor(threshold));
        return (needed, Math.Max(peak, needed));
    }

    private static double StrictEntryFloor(double threshold) => threshold + Math.Max(1d, threshold * 1e-12d);

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
        CanonicalStateHash.Combine("voyager-route-budget-native-warp-recharge-v2",
            input.SessionId, input.NativeVersion, input.OriginPlanetId, input.OriginStarId, input.DestinationPlanetId,
            input.DestinationStarId, input.CapturedAtGameTick, input.PlayerStateHash, input.CatalogStateHash,
            input.DestinationVisible, input.DestinationIsGasGiant, input.DestinationIsStarOnly, HashNumber(input.DistanceMeters),
            input.IsAlive, input.IsGrounded, input.ControllerEnabled, input.PlayerOrderIdle, input.BuildUiIdle,
            input.NativeGuideRunning, input.ThrusterLevel, HashNumber(input.MaxSailSpeed), HashNumber(input.MaxWarpSpeed),
            HashNumber(input.CoreEnergyJoules), HashNumber(input.CoreCapacityJoules), HashNumber(input.ReactorEnergyJoules),
            HashNumber(input.FuelStorageEnergyJoules), HashNumber(input.CoreGenerationLowerBoundWatts),
            HashNumber(input.WarpKeepingPowerPerSpeed), HashNumber(input.WarpStartPowerPerSpeed),
            HashNumber(input.EnergyConsumptionCoefficient), HashNumber(input.ThrustPowerPerAcceleration),
            input.NativeWarperCount, input.PackageWarperCount, input.AutoReplenishWarper,
            input.ReturnJourneyVerified, input.ReturnJourneyId, input.ReturnLegMaximumGameTicks, input.RemainingJourneyGameTicks,
            HashNumber(input.LockedReturnEnergyFloorJoules), HashNumber(input.LockedWithdrawalReserveJoules),
            HashNumber(input.LockedReturnCoreMinimumJoules), input.OriginalExtraWarperReserve,
            request.PlanetId, request.DestinationPlanetId, request.DestinationStarId, request.ExpectedPlayerStateHash, request.ExpectedCatalogStateHash,
            request.StateHashVersion, request.MaximumFlightGameTicks, request.ReturnReserveGameTicks,
            request.MaximumWarpGameTicksPerLeg, HashNumber(request.MaximumArrivalOffsetMeters),
            HashNumber(request.MinimumCoreEnergyRatio), request.ExtraWarperReserve, request.JourneyId);

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
