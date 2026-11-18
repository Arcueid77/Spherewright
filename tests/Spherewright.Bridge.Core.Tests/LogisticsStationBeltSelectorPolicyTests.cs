using Spherewright.Bridge.Core.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class LogisticsStationBeltSelectorPolicyTests
{
    [Theory]
    [InlineData(0, 0, 4)]
    [InlineData(3, 1, 4)]
    [InlineData(2, 3, 4)]
    public void AllowsSelectingAndChangingConnectedOutput(int current, int requested, int slots)
        => Assert.True(LogisticsStationBeltSelectorPolicy.CanChange(current, requested, slots));

    [Theory]
    [InlineData(2, 1, 4)]
    [InlineData(-1, 1, 4)]
    [InlineData(5, 1, 4)]
    [InlineData(0, -1, 4)]
    [InlineData(0, 4, 4)]
    [InlineData(0, 0, 0)]
    public void RejectsUnchangedOrInvalidSelectors(int current, int requested, int slots)
        => Assert.False(LogisticsStationBeltSelectorPolicy.CanChange(current, requested, slots));
}
