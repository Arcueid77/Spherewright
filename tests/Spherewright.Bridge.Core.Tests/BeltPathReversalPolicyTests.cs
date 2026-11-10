using Spherewright.Bridge.Core.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class BeltPathReversalPolicyTests
{
    [Theory]
    [InlineData(7, 0, 0, true)]
    [InlineData(0, 7, 0, true)]
    [InlineData(0, 0, 7, true)]
    [InlineData(7, 7, 0, false)]
    [InlineData(0, 8, 0, false)]
    public void OnePredecessorMayUseAnyNativeInputSide(int back, int left, int right, bool expected)
        => Assert.Equal(expected, BeltPathReversalPolicy.MatchesLinearMember(7, 9, new[] { back, left, right }, 7, 9));

    [Theory]
    [InlineData(1, 2, true, false, 1)]
    [InlineData(1, 2, false, true, 2)]
    [InlineData(1, 2, true, true, 0)]
    [InlineData(1, 2, false, false, 0)]
    [InlineData(0, 2, true, false, 0)]
    public void OnlyOppositeConnectedPortsEnablePilerWork(int first, int second, bool firstOut, bool secondOut, int expected)
        => Assert.Equal(expected, BeltPathReversalPolicy.PilerMode(first, second, firstOut, secondOut));
}
