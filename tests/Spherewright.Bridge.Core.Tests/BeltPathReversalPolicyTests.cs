using Spherewright.Bridge.Core.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class BeltPathReversalPolicyTests
{
    [Theory]
    [InlineData(1, 2, true, false, 1)]
    [InlineData(1, 2, false, true, 2)]
    [InlineData(1, 2, true, true, 0)]
    [InlineData(1, 2, false, false, 0)]
    [InlineData(0, 2, true, false, 0)]
    public void OnlyOppositeConnectedPortsEnablePilerWork(int first, int second, bool firstOut, bool secondOut, int expected)
        => Assert.Equal(expected, BeltPathReversalPolicy.PilerMode(first, second, firstOut, secondOut));
}
