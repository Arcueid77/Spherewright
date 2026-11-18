using Spherewright.Bridge.Core.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class LogisticsStationStorageSlotPolicyTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, 0, 1303)]
    [InlineData(1301, 0, 0, 0, 0, 1303)]
    [InlineData(1303, 12, 4, 0, 0, 1303)]
    public void AcceptsUnassignedOrEmptyConfiguredSlotAndSameItemSettings(
        int item, int count, int inc, int localOrder, int remoteOrder, int requested)
        => Assert.True(LogisticsStationStorageSlotPolicy.CanAssign(item, count, inc, localOrder, remoteOrder, requested));

    [Theory]
    [InlineData(1301, 1, 0, 0, 0, 1303)]
    [InlineData(1301, 0, 1, 0, 0, 1303)]
    [InlineData(1301, 0, 0, 1, 0, 1303)]
    [InlineData(1301, 0, 0, 0, -1, 1303)]
    [InlineData(0, 1, 0, 0, 0, 1303)]
    public void DoesNotReplaceInventoryOrAnOutstandingDelivery(
        int item, int count, int inc, int localOrder, int remoteOrder, int requested)
        => Assert.False(LogisticsStationStorageSlotPolicy.CanAssign(item, count, inc, localOrder, remoteOrder, requested));
}
