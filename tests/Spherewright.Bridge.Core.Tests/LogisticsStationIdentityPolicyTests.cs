using Spherewright.Bridge.Core.Logistics;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class LogisticsStationIdentityPolicyTests
{
    [Theory]
    [InlineData(2103, false, false, false, 0, 0, true)]
    [InlineData(2103, false, false, false, 104, 0, true)]
    [InlineData(2104, true, false, false, 104, 12, true)]
    [InlineData(2103, false, false, false, 105, 0, false)]
    [InlineData(2103, false, false, false, 0, 12, false)]
    [InlineData(2103, true, false, false, 104, 12, false)]
    [InlineData(2103, false, true, false, 0, 0, false)]
    [InlineData(2103, false, false, true, 0, 0, false)]
    [InlineData(2104, false, false, false, 104, 0, false)]
    [InlineData(2104, true, false, false, 0, 12, false)]
    [InlineData(2104, true, false, false, 104, 0, false)]
    [InlineData(2105, true, true, false, 104, 12, false)]
    public void PaidConstructionBindsTheNativeStationKindAndPlanet(
        int itemId, bool interstellar, bool collector, bool veinCollector, int stationPlanetId, int galacticId, bool expected)
    {
        Assert.Equal(expected, LogisticsStationIdentityPolicy.MatchesPaidConstruction(
            itemId, 7, 42, 104, 7, 42, stationPlanetId, galacticId, interstellar, collector, veinCollector));
    }

    [Fact]
    public void PaidConstructionRejectsForeignOrMissingStationIdentity()
    {
        Assert.False(LogisticsStationIdentityPolicy.MatchesPaidConstruction(2103, 0, 42, 104, 0, 42, 0, 0, false, false, false));
        Assert.False(LogisticsStationIdentityPolicy.MatchesPaidConstruction(2103, 7, 0, 104, 7, 0, 0, 0, false, false, false));
        Assert.False(LogisticsStationIdentityPolicy.MatchesPaidConstruction(2103, 7, 42, 104, 8, 42, 0, 0, false, false, false));
        Assert.False(LogisticsStationIdentityPolicy.MatchesPaidConstruction(2103, 7, 42, 104, 7, 43, 0, 0, false, false, false));
        Assert.False(LogisticsStationIdentityPolicy.MatchesPaidConstruction(2103, 7, 42, 0, 7, 42, 0, 0, false, false, false));
    }

    [Theory]
    [InlineData(false, 0, 104, true)]
    [InlineData(false, 104, 104, true)]
    [InlineData(false, 102, 104, false)]
    [InlineData(true, 104, 104, true)]
    [InlineData(true, 0, 104, false)]
    [InlineData(true, 102, 104, false)]
    [InlineData(false, 0, 0, false)]
    public void MatchesLocalPlanet_AcceptsOnlyTheNativeLocalStationSentinelOrExactPlanet(
        bool isInterstellar,
        int stationPlanetId,
        int factoryPlanetId,
        bool expected)
    {
        Assert.Equal(
            expected,
            LogisticsStationIdentityPolicy.MatchesLocalPlanet(
                isInterstellar,
                stationPlanetId,
                factoryPlanetId));
    }
}
