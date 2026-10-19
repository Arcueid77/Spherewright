using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Logistics;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Logistics;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class LogisticsActiveVesselObservationPolicyTests
{
    private static LogisticsActiveVesselFacts Ship(int shipIndex = 9) => new()
    {
        ShipIndex = shipIndex, Gene = 31, OtherGId = 8,
        ItemId = 1001, ItemCount = 100, Inc = 200, Stage = 0, Direction = 1,
        T = 0.5f, USpeed = 1000, WarpState = 0, WarperCount = 2,
    };

    private static LogisticsActiveVesselObservation Capture(params LogisticsActiveVesselFacts[] ships) =>
        LogisticsActiveVesselObservationPolicy.Capture(true, 7, ships.Length, 10, ships, 100);

    [Fact]
    public void CompressedActiveSlotRetainsNativeShipNumberAndAllowsDispatchGeneWrap()
    {
        var ship = Ship(); ship.Gene = int.MinValue;
        var result = Capture(ship);
        var observed = Assert.Single(result.Vessels);
        Assert.Equal("observed", result.State);
        Assert.True(result.Complete);
        Assert.Equal(7, observed.OwnerGId);
        Assert.Equal(9, observed.ShipIndex); // Native active slot was zero.
        Assert.Equal(int.MinValue, observed.Gene);
        ship.ItemCount = 0;
        Assert.Equal(100, observed.ItemCount); // Detached snapshot, no live alias.
    }

    [Theory]
    [InlineData(-2, 1)] [InlineData(-1, 1)] [InlineData(0, 1)] [InlineData(1, 1)] [InlineData(2, 1)]
    [InlineData(2, -1)] [InlineData(1, -1)] [InlineData(0, -1)] [InlineData(-1, -1)] [InlineData(-2, -1)]
    public void RawNativeRoundTripStagesRemainObservationsRatherThanDeliveryEvents(int stage, int direction)
    {
        var ship = Ship(); ship.Stage = stage; ship.Direction = direction;
        if (direction == -1) ship.ItemCount = 0;
        var observed = Assert.Single(Capture(ship).Vessels);
        Assert.Equal(stage, observed.Stage);
        Assert.Equal(direction, observed.Direction);
        Assert.Equal(ship.ItemCount, observed.ItemCount);
    }

    [Theory]
    [InlineData(-3.5f)] [InlineData(0f)] [InlineData(1.5f)] [InlineData(120f)]
    public void CruiseTIsNotClampedToInventedTotalRouteProgress(float t)
    {
        var ship = Ship(); ship.T = t;
        Assert.Equal(t, Assert.Single(Capture(ship).Vessels).T);
    }

    [Fact]
    public void EmptyReturnCargoDoesNotRequireAnInventedPositiveItemOrZeroResidualInc()
    {
        var ship = Ship(); ship.Direction = -1; ship.ItemId = 0; ship.ItemCount = 0; ship.Inc = 2;
        var observed = Assert.Single(Capture(ship).Vessels);
        Assert.Equal(0, observed.ItemCount);
        Assert.Equal(2, observed.Inc);
    }

    [Fact]
    public void VerifiedZeroCounterDiffersFromUnavailablePositivePool()
    {
        var empty = LogisticsActiveVesselObservationPolicy.Capture(true, 7, 0, -1, null, 100);
        Assert.Equal("observed", empty.State);
        Assert.True(empty.Complete);
        Assert.Equal(0, empty.NativeWorkingVesselCount);
        var unknown = LogisticsActiveVesselObservationPolicy.Capture(true, 7, 1, -1, null, 100);
        Assert.Equal("unavailable", unknown.State);
        Assert.False(unknown.Complete);
        Assert.Null(unknown.NativeWorkingVesselCount);
        Assert.Empty(unknown.Vessels);
    }

    [Fact]
    public void BoundedPrefixCannotClaimThatTheRemainingVesselsAreAbsent()
    {
        var prefix = Enumerable.Range(0, 32).Select(Ship).ToArray();
        var result = LogisticsActiveVesselObservationPolicy.Capture(true, 7, 40, 40, prefix, 100);
        Assert.Equal("partial", result.State);
        Assert.Equal(40, result.NativeWorkingVesselCount);
        Assert.Equal(32, result.Vessels.Count);
        Assert.True(result.Truncated);
        Assert.False(result.Complete);
        Assert.Equal("active_vessel_limit", result.ReasonCode);
    }

    [Fact]
    public void CallerMustSupplyTheExactBoundedNativePrefix()
    {
        var result = LogisticsActiveVesselObservationPolicy.Capture(true, 7, 2, 10, new[] { Ship() }, 100);
        Assert.Equal("unavailable", result.State);
        Assert.Null(result.NativeWorkingVesselCount);
        Assert.Empty(result.Vessels);
    }

    [Fact]
    public void DuplicatePhysicalShipCannotMasqueradeAsTwoActiveDispatches()
    {
        var first = Ship(); var second = Ship(); second.Gene++;
        var result = Capture(first, second);
        Assert.Equal("unavailable", result.State);
        Assert.Empty(result.Vessels);
    }

    public static IEnumerable<object[]> InvalidScalars()
    {
        yield return new object[] { "negative_ship", (Action<LogisticsActiveVesselFacts>)(v => v.ShipIndex = -1) };
        yield return new object[] { "outside_physical_ship_pool", (Action<LogisticsActiveVesselFacts>)(v => v.ShipIndex = 10) };
        yield return new object[] { "unknown_other_endpoint", (Action<LogisticsActiveVesselFacts>)(v => v.OtherGId = 0) };
        yield return new object[] { "self_endpoint", (Action<LogisticsActiveVesselFacts>)(v => v.OtherGId = 7) };
        yield return new object[] { "cargo_without_item", (Action<LogisticsActiveVesselFacts>)(v => v.ItemId = 0) };
        yield return new object[] { "negative_count", (Action<LogisticsActiveVesselFacts>)(v => v.ItemCount = -1) };
        yield return new object[] { "negative_inc", (Action<LogisticsActiveVesselFacts>)(v => v.Inc = -1) };
        yield return new object[] { "negative_warpers", (Action<LogisticsActiveVesselFacts>)(v => v.WarperCount = -1) };
        yield return new object[] { "outside_stage", (Action<LogisticsActiveVesselFacts>)(v => v.Stage = 3) };
        yield return new object[] { "invalid_direction", (Action<LogisticsActiveVesselFacts>)(v => v.Direction = 0) };
        yield return new object[] { "nan_animation", (Action<LogisticsActiveVesselFacts>)(v => v.T = float.NaN) };
        yield return new object[] { "infinite_speed", (Action<LogisticsActiveVesselFacts>)(v => v.USpeed = float.PositiveInfinity) };
        yield return new object[] { "negative_speed", (Action<LogisticsActiveVesselFacts>)(v => v.USpeed = -1) };
        yield return new object[] { "infinite_warp_state", (Action<LogisticsActiveVesselFacts>)(v => v.WarpState = float.NegativeInfinity) };
    }

    [Theory]
    [MemberData(nameof(InvalidScalars))]
    public void CorruptActiveDataIsUnknownInsteadOfAnObservedEmptyRoute(string _, Action<LogisticsActiveVesselFacts> corrupt)
    {
        var ship = Ship(); corrupt(ship);
        var result = Capture(ship);
        Assert.Equal("unavailable", result.State);
        Assert.False(result.Complete);
        Assert.Null(result.NativeWorkingVesselCount);
        Assert.Empty(result.Vessels);
    }

    [Theory]
    [InlineData(-1, 10, 100)] [InlineData(1, 0, 100)] [InlineData(1, 10, -1)]
    public void InvalidClockOrPoolNeverBecomesZero(int count, int length, long tick)
    {
        var result = LogisticsActiveVesselObservationPolicy.Capture(true, 7, count, length, new[] { Ship() }, tick);
        Assert.Equal("unavailable", result.State);
        Assert.Null(result.NativeWorkingVesselCount);
    }

    [Fact]
    public void PlanetaryStationHasExplicitNotApplicableCoverage()
    {
        var result = LogisticsActiveVesselObservationPolicy.Capture(false, 0, 0, -1, null, 100);
        Assert.Equal("not_applicable", result.State);
        Assert.Equal(0, result.NativeWorkingVesselCount);
        Assert.True(result.Complete);
        Assert.Equal("unavailable", LogisticsActiveVesselObservationPolicy.Capture(false, 0, 1, 10, new[] { Ship() }, 100).State);
    }

    [Theory]
    [InlineData(false, 104, 8, false, false)]
    [InlineData(true, 0, 8, false, false)]
    [InlineData(true, 104, -8, false, false)]
    [InlineData(true, 104, 0, false, false)]
    [InlineData(true, 104, 8, true, false)]
    [InlineData(true, 104, 8, false, true)]
    public void OwnedRemoteStationOptInCannotWidenAnActionOrBecomeGeometryAndInventoryRead(
        bool direct, int planet, int entity, bool geometry, bool materials)
    {
        Assert.NotNull(LogisticsActiveVesselObservationPolicy.ValidateOwnedRemoteStationRequest(direct, planet, entity, geometry, materials));
        Assert.False(OwnedFactoryScopePolicy.IsCurrentLocalActionTarget(104, 105));
    }

    [Fact]
    public void DirectStationOnlyRequestIsEligibleButStillNeedsNativeOwnedFactoryIdentity()
    {
        Assert.Null(LogisticsActiveVesselObservationPolicy.ValidateOwnedRemoteStationRequest(true, 104, 8, false, false));
    }

    [Fact]
    public void ChangingReadOnlyShipsDoesNotInvalidateAnyActionConfigurationOrFleetHash()
    {
        var station = new LogisticsStationSnapshot { PlanetId = 104, StationId = 2, EntityId = 8, GalacticStationId = 7 };
        var live = CanonicalStateHash.LogisticsStation(station);
        var config = CanonicalStateHash.LogisticsStationConfiguration(station);
        var fleet = CanonicalStateHash.LogisticsStationFleet(station);
        station.ActiveVessels = Capture(Ship());
        Assert.Equal(live, CanonicalStateHash.LogisticsStation(station));
        Assert.Equal(config, CanonicalStateHash.LogisticsStationConfiguration(station));
        Assert.Equal(fleet, CanonicalStateHash.LogisticsStationFleet(station));
    }
}
