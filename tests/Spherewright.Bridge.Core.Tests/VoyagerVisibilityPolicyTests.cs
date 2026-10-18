using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Celestial;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerVisibilityPolicyTests
{
    [Theory]
    [InlineData(0, true, true, false)]
    [InlineData(1, true, true, true)]
    [InlineData(1, false, true, false)]
    [InlineData(2, false, true, true)]
    [InlineData(3, false, false, true)]
    public void PlanetVisibilityUsesNativeCurrentPlanetAndLocalStarLevels(int level, bool current, bool local, bool allowed)
    {
        var result = VoyagerVisibilityPolicy.EvaluatePlanet(level, current, local, false, 1000d, true, true);
        Assert.Equal(allowed, result.ObservationAllowed);
        Assert.Equal(allowed, result.ResourceSummaryComplete);
        Assert.Equal(allowed ? VoyagerResourceVisibility.Observed : VoyagerResourceVisibility.Unknown, result.ResourceVisibility);
    }

    [Fact]
    public void NearbyBoundaryIsStrictAndPlanetDistanceIsEvaluatedIndependently()
    {
        var nearby = VoyagerVisibilityPolicy.NearbyObservationDistanceMeters;
        Assert.True(VoyagerVisibilityPolicy.EvaluateStar(3, false, nearby - 1d, true, true).ObservationAllowed);
        Assert.False(VoyagerVisibilityPolicy.EvaluateStar(3, false, nearby, true, true).ObservationAllowed);
        Assert.Equal(4, VoyagerVisibilityPolicy.EvaluateStar(3, false, nearby, true, true).RequiredObserveLevel);
        Assert.True(VoyagerVisibilityPolicy.EvaluateStar(4, false, nearby, true, true).ObservationAllowed);
        Assert.False(VoyagerVisibilityPolicy.EvaluatePlanet(3, false, false, false, nearby, true, true).ObservationAllowed);
        Assert.True(VoyagerVisibilityPolicy.EvaluatePlanet(3, false, false, false, nearby - 1d, true, true).ObservationAllowed);
    }

    [Fact]
    public void FactoryResourceExceptionStillRequiresObserveLevelOne()
    {
        var far = VoyagerVisibilityPolicy.NearbyObservationDistanceMeters * 2d;
        Assert.True(VoyagerVisibilityPolicy.EvaluatePlanet(1, false, false, true, far, true, true).ObservationAllowed);
        Assert.False(VoyagerVisibilityPolicy.EvaluatePlanet(0, false, false, true, far, true, true).ObservationAllowed);
        Assert.False(VoyagerVisibilityPolicy.EvaluateStar(1, false, far, true, true).ObservationAllowed);
    }

    [Fact]
    public void MapFactoryBranchPreservesNativeElseIfOrderAtLevelThree()
    {
        var far = VoyagerVisibilityPolicy.NearbyObservationDistanceMeters;
        Assert.False(VoyagerVisibilityPolicy.CanViewStarPlanets(3, false, far, true));
        Assert.True(VoyagerVisibilityPolicy.CanViewStarPlanets(3, false, far - 1d, false));
        Assert.True(VoyagerVisibilityPolicy.CanViewStarPlanets(0, false, far, true));
        Assert.False(VoyagerVisibilityPolicy.CanViewStarPlanets(0, false, far, false));
        Assert.True(VoyagerVisibilityPolicy.CanViewStarPlanets(0, true, far, false));
        Assert.True(VoyagerVisibilityPolicy.CanViewStarPlanets(4, false, far, false));
    }

    [Theory]
    [InlineData(false, false, "native_scan_pending")]
    [InlineData(false, true, "native_scan_pending")]
    [InlineData(true, false, "native_resource_summary_incomplete")]
    public void IncompleteObservationRemainsUnknownRatherThanEmptyOrAbsent(bool scan, bool complete, string reason)
    {
        var result = VoyagerVisibilityPolicy.EvaluateStar(4, false, 20000000d, scan, complete);
        Assert.True(result.ObservationAllowed);
        Assert.Equal(VoyagerResourceVisibility.Unknown, result.ResourceVisibility);
        Assert.False(result.ResourceSummaryComplete);
        Assert.Equal(reason, result.ReasonCode);
        Assert.Null(VoyagerVisibilityPolicy.ProjectResources(result, HiddenSource()));
        Assert.False(VoyagerVisibilityPolicy.CanClaimVeinResourceAbsent(result, ExplicitZero()));
    }

    [Fact]
    public void HiddenResourceSourceIsNotEnumeratedEvenWhenNativeScanHasCompleted()
    {
        var hidden = VoyagerVisibilityPolicy.EvaluateStar(1, false, 20000000d, true, true);
        Assert.Null(VoyagerVisibilityPolicy.ProjectResources(hidden, HiddenSource()));
        Assert.False(VoyagerVisibilityPolicy.CanClaimVeinResourceAbsent(hidden, ExplicitZero()));
        Assert.Equal("native_observation_level_required", hidden.ReasonCode);
    }

    [Fact]
    public void CompleteObservationCopiesExplicitZeroWithoutAliasingNativeRows()
    {
        var observed = VoyagerVisibilityPolicy.EvaluateStar(4, false, 20000000d, true, true);
        var source = ExplicitZero();
        var projected = Assert.Single(VoyagerVisibilityPolicy.ProjectResources(observed, new[] { source })!);
        Assert.Equal(0L, projected.Amount);
        Assert.Equal(0, projected.VeinCount);
        Assert.True(VoyagerVisibilityPolicy.CanClaimVeinResourceAbsent(observed, projected));
        source.Amount = 12;
        Assert.Equal(0L, projected.Amount);
        Assert.NotSame(source, projected);
    }

    [Fact]
    public void MissingValuesAndPositiveVeinEvidenceCannotBecomeAbsence()
    {
        var observed = VoyagerVisibilityPolicy.EvaluateStar(4, false, 20000000d, true, true);
        Assert.False(VoyagerVisibilityPolicy.CanClaimVeinResourceAbsent(observed, null));
        var resource = ExplicitZero();
        resource.Amount = null;
        Assert.False(VoyagerVisibilityPolicy.CanClaimVeinResourceAbsent(observed, resource));
        resource.Amount = 0;
        resource.VeinCount = 1;
        Assert.False(VoyagerVisibilityPolicy.CanClaimVeinResourceAbsent(observed, resource));
        resource.VeinCount = 0;
        resource.Amount = 1;
        Assert.False(VoyagerVisibilityPolicy.CanClaimVeinResourceAbsent(observed, resource));
        resource.Amount = 0;
        resource.Kind = VoyagerResourceKinds.Gas;
        Assert.False(VoyagerVisibilityPolicy.CanClaimVeinResourceAbsent(observed, resource));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1d)]
    public void InvalidDistanceFailsClosedWithoutResourceEnumeration(double distance)
    {
        var observation = VoyagerVisibilityPolicy.EvaluatePlanet(4, true, true, true, distance, true, true);
        Assert.False(observation.ObservationAllowed);
        Assert.Equal("invalid_observation_inputs", observation.ReasonCode);
        Assert.Null(VoyagerVisibilityPolicy.ProjectResources(observation, HiddenSource()));
        Assert.False(VoyagerVisibilityPolicy.CanViewStarPlanets(4, true, distance, true));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(16, true)]
    [InlineData(32, true)]
    [InlineData(33, false)]
    public void CatalogRequestIsExplicitlyBounded(int limit, bool valid) =>
        Assert.Equal(valid, VoyagerVisibilityPolicy.IsCatalogLimitValid(limit));

    [Fact]
    public void NativeDistanceUnitsAreNotRealWorldAstronomicalMeters()
    {
        Assert.Equal(2400000d, VoyagerVisibilityPolicy.MetersPerLightYear);
        Assert.Equal(40000d, VoyagerVisibilityPolicy.MetersPerAstronomicalUnit);
        Assert.Equal(6d, VoyagerVisibilityPolicy.NearbyObservationDistanceMeters / VoyagerVisibilityPolicy.MetersPerLightYear);
    }

    [Fact]
    public void SystemAbsenceRequiresEveryMatchingVeinToBeExplicitlyZero()
    {
        var star = CompleteSystemSummary(ExplicitZero(), ExplicitZero());
        Assert.True(VoyagerVisibilityPolicy.CanClaimSystemResourceAbsent(star, 1011));
        star.Resources![1].Amount = 1L;
        Assert.False(VoyagerVisibilityPolicy.CanClaimSystemResourceAbsent(star, 1011));
        star.Resources[1].Amount = 0L;
        star.Resources[1].VeinCount = 1;
        Assert.False(VoyagerVisibilityPolicy.CanClaimSystemResourceAbsent(star, 1011));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(0.5d)]
    [InlineData(null)]
    public void GasFireIcePresenceBlocksSystemAbsenceDespiteZeroVeins(double? rate)
    {
        var zeroVein = ExplicitZero();
        zeroVein.ItemId = 1011;
        var star = CompleteSystemSummary(zeroVein, new VoyagerResourceSummary
        {
            Kind = VoyagerResourceKinds.Gas, ItemId = 1011, Name = "Synthetic fire ice", RatePerSecond = rate,
        });
        Assert.True(VoyagerVisibilityPolicy.CanClaimVeinResourceAbsent(
            VoyagerVisibilityPolicy.EvaluateStar(4, false, 20000000d, true, true), zeroVein));
        Assert.False(VoyagerVisibilityPolicy.CanClaimSystemResourceAbsent(star, 1011));
    }

    [Fact]
    public void OceanPresenceBlocksSystemAbsenceEvenWhenAZeroRateWasSupplied()
    {
        var zeroVein = ExplicitZero();
        zeroVein.ItemId = 1000;
        var star = CompleteSystemSummary(zeroVein, new VoyagerResourceSummary
        {
            Kind = VoyagerResourceKinds.Ocean, ItemId = 1000, Amount = 0L, VeinCount = 0, RatePerSecond = 0d,
        });
        Assert.False(VoyagerVisibilityPolicy.CanClaimSystemResourceAbsent(star, 1000));
    }

    [Fact]
    public void OtherItemsGasAndOceanPresenceDoesNotInvalidateExplicitTargetAbsence()
    {
        var star = CompleteSystemSummary(ExplicitZero(), new VoyagerResourceSummary
        {
            Kind = VoyagerResourceKinds.Gas, ItemId = 1120, RatePerSecond = 1d,
        }, new VoyagerResourceSummary
        {
            Kind = VoyagerResourceKinds.Ocean, ItemId = 1000,
        }, new VoyagerResourceSummary
        {
            Kind = VoyagerResourceKinds.Vein, ItemId = 1001, VeinType = 1, Amount = 500L, VeinCount = 5,
        });
        Assert.True(VoyagerVisibilityPolicy.CanClaimSystemResourceAbsent(star, 1011));
    }

    [Fact]
    public void UnknownIncompleteOrUnscannedSystemSummaryCannotProveAbsence()
    {
        var changes = new Action<VoyagerStarSnapshot>[]
        {
            star => star.ResourceVisibility = VoyagerResourceVisibility.Unknown,
            star => star.ScanReady = false,
            star => star.ResourceSummaryComplete = false,
            star => star.ResourceSummaryScope = "unknown",
            star => star.ResourceSummaryScope = "remaining_veins",
            star => star.ResourceSummaryScope = "Remaining_veins_and_native_gas_oceans",
            star => star.Resources = null,
        };
        foreach (var change in changes)
        {
            var star = CompleteSystemSummary(ExplicitZero());
            change(star);
            Assert.False(VoyagerVisibilityPolicy.CanClaimSystemResourceAbsent(star, 1011));
        }
        Assert.False(VoyagerVisibilityPolicy.CanClaimSystemResourceAbsent(null, 1011));
    }

    [Fact]
    public void MissingTargetRowOrMissingZeroMeasurementCannotProveSystemAbsence()
    {
        Assert.False(VoyagerVisibilityPolicy.CanClaimSystemResourceAbsent(CompleteSystemSummary(), 1011));
        var otherItem = ExplicitZero();
        otherItem.ItemId = 1001;
        Assert.False(VoyagerVisibilityPolicy.CanClaimSystemResourceAbsent(CompleteSystemSummary(otherItem), 1011));
        var missingAmount = ExplicitZero();
        missingAmount.Amount = null;
        Assert.False(VoyagerVisibilityPolicy.CanClaimSystemResourceAbsent(CompleteSystemSummary(missingAmount), 1011));
        var missingCount = ExplicitZero();
        missingCount.VeinCount = null;
        Assert.False(VoyagerVisibilityPolicy.CanClaimSystemResourceAbsent(CompleteSystemSummary(missingCount), 1011));
        var unknownForm = ExplicitZero();
        unknownForm.Kind = "unrecognized_native_form";
        Assert.False(VoyagerVisibilityPolicy.CanClaimSystemResourceAbsent(CompleteSystemSummary(ExplicitZero(), unknownForm), 1011));
        Assert.False(VoyagerVisibilityPolicy.CanClaimSystemResourceAbsent(CompleteSystemSummary(ExplicitZero(), null!), 1011));
        Assert.False(VoyagerVisibilityPolicy.CanClaimSystemResourceAbsent(CompleteSystemSummary(ExplicitZero()), 0));
    }

    private static VoyagerStarSnapshot CompleteSystemSummary(params VoyagerResourceSummary[] resources) => new()
    {
        StarId = 2,
        ResourceVisibility = VoyagerResourceVisibility.Observed,
        ScanReady = true,
        ResourceSummaryComplete = true,
        ResourceSummaryScope = VoyagerVisibilityPolicy.CompleteSystemResourceSummaryScope,
        Resources = resources.ToList(),
    };

    private static VoyagerResourceSummary ExplicitZero() => new()
    {
        Kind = VoyagerResourceKinds.Vein, VeinType = 8, ItemId = 1011, Amount = 0L, VeinCount = 0,
    };

    private static IEnumerable<VoyagerResourceSummary> HiddenSource()
    {
        throw new InvalidOperationException("An unobservable native resource source must never be enumerated.");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
