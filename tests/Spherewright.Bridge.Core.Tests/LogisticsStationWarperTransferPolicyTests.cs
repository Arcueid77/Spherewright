using Spherewright.Bridge.Core.Logistics;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Logistics;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class LogisticsStationWarperTransferPolicyTests
{
    [Theory]
    [InlineData(true, false, false, true, 0, 50, 20, 20, 0, true, true)]
    [InlineData(true, false, false, true, 49, 50, 1, 1, 0, true, true)]
    [InlineData(true, false, false, true, 49, 50, 2, 2, 0, true, false)]
    [InlineData(true, false, false, false, 0, 50, 1, 1, 0, true, false)]
    [InlineData(true, false, false, true, 0, 0, 1, 1, 0, true, false)]
    [InlineData(false, false, false, true, 0, 50, 1, 1, 0, true, false)]
    [InlineData(true, true, false, true, 0, 50, 1, 1, 0, true, false)]
    [InlineData(true, false, true, true, 0, 50, 1, 1, 0, true, false)]
    [InlineData(true, false, false, true, -1, 50, 1, 1, 0, true, false)]
    [InlineData(true, false, false, true, 51, 50, 1, 1, 0, true, false)]
    [InlineData(true, false, false, true, 0, 50, 2, 1, 0, true, false)]
    [InlineData(true, false, false, true, 0, 50, 1, 1, 1, true, false)]
    [InlineData(true, false, false, true, 0, 50, 0, 1, 0, true, false)]
    [InlineData(true, false, false, true, 100, 500, 101, 101, 0, true, false)]
    [InlineData(true, false, false, true, 20, 50, 20, 0, 0, false, true)]
    [InlineData(true, false, false, true, 19, 50, 20, 0, 0, false, false)]
    public void NativeWarperCounterUsesExactTechnologyCapacityAndZeroInc(
        bool stellar, bool collector, bool veinCollector, bool technology, int stationCount,
        int capacity, int count, int packageCount, int inc, bool deposit, bool expected)
    {
        Assert.Equal(expected, LogisticsStationFleetTransferPolicy.TryValidate(
            stellar, collector, veinCollector,
            deposit ? LogisticsStationFleetTransferDirections.PlayerToStation : LogisticsStationFleetTransferDirections.StationToPlayer,
            LogisticsFleetItemIds.Warper, count, packageCount, inc,
            5, 2, 50, 3, 1, 10, true, technology, stationCount, capacity, out _));
    }

    [Fact]
    public void WithdrawalCannotDiscardWarperItemsWhenPlayerPackageIsFull()
    {
        Assert.False(LogisticsStationFleetTransferPolicy.TryValidate(true, false, false,
            LogisticsStationFleetTransferDirections.StationToPlayer, LogisticsFleetItemIds.Warper,
            1, 0, 0, 0, 0, 50, 0, 0, 10, false, true, 1, 50, out _));
    }

    [Fact]
    public void FleetHashBindsWarperCountAndCapacityButNotChargingOrItemStock()
    {
        var snapshot = new LogisticsStationSnapshot { IsInterstellar = true, WarperCount = 10, WarperCapacity = 50 };
        var original = CanonicalStateHash.LogisticsStationFleet(snapshot);
        snapshot.Energy++;
        snapshot.StorageSlots.Add(new LogisticsStationStorageSlotSnapshot { ItemId = 1001, Count = 50 });
        Assert.Equal(original, CanonicalStateHash.LogisticsStationFleet(snapshot));
        snapshot.WarperCount++;
        Assert.NotEqual(original, CanonicalStateHash.LogisticsStationFleet(snapshot));
        snapshot.WarperCount--;
        snapshot.WarperCapacity++;
        Assert.NotEqual(original, CanonicalStateHash.LogisticsStationFleet(snapshot));
    }

    [Fact]
    public void LegacyPolicyOverloadStillAdmitsCraftAndDoesNotInventWarperTechnology()
    {
        Assert.True(LogisticsStationFleetTransferPolicy.TryValidate(true, false, false,
            LogisticsStationFleetTransferDirections.PlayerToStation, LogisticsFleetItemIds.Vessel,
            1, 1, 0, 0, 0, 50, 0, 0, 10, true, out _));
        Assert.False(LogisticsStationFleetTransferPolicy.TryValidate(true, false, false,
            LogisticsStationFleetTransferDirections.PlayerToStation, LogisticsFleetItemIds.Warper,
            1, 1, 0, 0, 0, 50, 0, 0, 10, true, out _));
    }
}
