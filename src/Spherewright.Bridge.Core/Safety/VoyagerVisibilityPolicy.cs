using Spherewright.Contracts.Celestial;

namespace Spherewright.Bridge.Core.Safety;

public sealed class VoyagerResourceObservation
{
    internal VoyagerResourceObservation(int requiredLevel, bool allowed, bool scanReady, bool summaryComplete, string reasonCode)
    {
        RequiredObserveLevel = requiredLevel;
        ObservationAllowed = allowed;
        ScanReady = scanReady;
        ResourceSummaryComplete = allowed && scanReady && summaryComplete;
        ResourceVisibility = ResourceSummaryComplete ? VoyagerResourceVisibility.Observed : VoyagerResourceVisibility.Unknown;
        ReasonCode = reasonCode;
    }

    public int RequiredObserveLevel { get; }
    public bool ObservationAllowed { get; }
    public bool ScanReady { get; }
    public bool ResourceSummaryComplete { get; }
    public string ResourceVisibility { get; }
    public string ReasonCode { get; }
}

public static class VoyagerVisibilityPolicy
{
    public const double MetersPerLightYear = 2400000d;
    public const double MetersPerAstronomicalUnit = 40000d;
    public const double NearbyObservationDistanceMeters = 14400000d;
    public const int MinimumCatalogLimit = 1;
    public const int MaximumCatalogLimit = 32;
    public const string CompleteSystemResourceSummaryScope = "remaining_veins_and_native_gas_oceans";

    public static bool IsCatalogLimitValid(int limit) => limit >= MinimumCatalogLimit && limit <= MaximumCatalogLimit;

    public static VoyagerResourceObservation EvaluateStar(
        int observeLevel, bool isLocalStar, double distanceMeters, bool scanReady, bool summaryComplete)
    {
        var valid = observeLevel >= 0 && IsFiniteDistance(distanceMeters);
        var required = isLocalStar ? 2 : (distanceMeters < NearbyObservationDistanceMeters ? 3 : 4);
        return Evaluate(required, valid && observeLevel >= required, scanReady, summaryComplete, valid);
    }

    public static VoyagerResourceObservation EvaluatePlanet(
        int observeLevel, bool isCurrentPlanet, bool isLocalStar, bool hasFactory,
        double distanceMeters, bool scanReady, bool summaryComplete)
    {
        var valid = observeLevel >= 0 && IsFiniteDistance(distanceMeters);
        var required = isCurrentPlanet ? 1 : (isLocalStar ? 2 : (distanceMeters < NearbyObservationDistanceMeters ? 3 : 4));
        var allowed = valid && (observeLevel >= required || (hasFactory && observeLevel >= 1));
        return Evaluate(required, allowed, scanReady, summaryComplete, valid);
    }

    public static bool CanViewStarPlanets(int observeLevel, bool isLocalStar, double distanceMeters, bool hasAnyFactory)
    {
        if (observeLevel < 0 || !IsFiniteDistance(distanceMeters)) return false;
        if (isLocalStar) return true;
        // Exact UIStarmap.OnStarClick else-if order: level 3/far cannot fall through to factory.
        if (observeLevel == 3) return distanceMeters < NearbyObservationDistanceMeters;
        if (observeLevel >= 4) return true;
        return hasAnyFactory;
    }

    public static List<VoyagerResourceSummary>? ProjectResources(
        VoyagerResourceObservation observation, IEnumerable<VoyagerResourceSummary>? resources)
    {
        if (observation is null) throw new ArgumentNullException(nameof(observation));
        // Return before even enumerating a hidden native source.
        if (!observation.ResourceSummaryComplete || resources is null) return null;
        return resources.Select(resource => new VoyagerResourceSummary
        {
            Kind = resource.Kind,
            VeinType = resource.VeinType,
            ItemId = resource.ItemId,
            Name = resource.Name,
            Amount = resource.Amount,
            VeinCount = resource.VeinCount,
            RatePerSecond = resource.RatePerSecond,
        }).ToList();
    }

    public static bool CanClaimVeinResourceAbsent(VoyagerResourceObservation observation, VoyagerResourceSummary? resource)
    {
        if (observation is null) throw new ArgumentNullException(nameof(observation));
        return observation.ResourceSummaryComplete
            && resource is not null
            && resource.Kind == VoyagerResourceKinds.Vein
            && resource.VeinType >= 1 && resource.VeinType <= 14
            && resource.ItemId > 0
            && resource.Amount == 0L
            && resource.VeinCount == 0;
    }

    public static bool CanClaimSystemResourceAbsent(VoyagerStarSnapshot? star, int itemId)
    {
        if (star is null || itemId <= 0
            || !string.Equals(star.ResourceVisibility, VoyagerResourceVisibility.Observed, StringComparison.Ordinal)
            || !star.ScanReady || !star.ResourceSummaryComplete
            || !string.Equals(star.ResourceSummaryScope, CompleteSystemResourceSummaryScope, StringComparison.Ordinal)
            || star.Resources is null)
        {
            return false;
        }

        var observation = new VoyagerResourceObservation(star.RequiredObserveLevel, true, true, true, string.Empty);
        var matchingVeinFound = false;
        foreach (var resource in star.Resources)
        {
            if (resource is null) return false;
            if (resource.ItemId != itemId) continue;
            // A gas/ocean row is presence evidence even if its rate is zero or unknown.
            // Unknown kinds also fail closed instead of being mistaken for a zero vein.
            if (!CanClaimVeinResourceAbsent(observation, resource)) return false;
            matchingVeinFound = true;
        }
        // No row is unknown evidence, not explicit zero evidence.
        return matchingVeinFound;
    }

    private static VoyagerResourceObservation Evaluate(int required, bool allowed, bool scanReady, bool complete, bool valid)
    {
        var reason = !valid ? "invalid_observation_inputs"
            : !allowed ? "native_observation_level_required"
            : !scanReady ? "native_scan_pending"
            : !complete ? "native_resource_summary_incomplete" : string.Empty;
        return new VoyagerResourceObservation(required, allowed, scanReady, complete, reason);
    }

    private static bool IsFiniteDistance(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
}
