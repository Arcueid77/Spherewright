using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerNativeIdleHandoffPolicyTests
{
    [Fact]
    public void GroundedMoveWaitsForTheSamePhysicalStopRequiredByItsIdleOwner()
    {
        // A zero RTS tail and reported player speed can precede the rigid-body
        // stop. The original Move must keep its bounded stop for that frame.
        Assert.False(VoyagerNativeIdleHandoffPolicy.PhysicallyAtRest(true, true, 0, 0, .001, 0, 0, 0, 0));
        Assert.True(VoyagerNativeIdleHandoffPolicy.PhysicallyAtRest(true, true, 0, 0, 0, 0, 0, 0, 0));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void AnyRemainingControllerOrBodyMotionPreventsIdleHandoff(int component)
    {
        var values = new double[6];
        values[component] = .001;
        Assert.False(VoyagerNativeIdleHandoffPolicy.PhysicallyAtRest(true, true, 0,
            values[0], values[1], values[2], values[3], values[4], values[5]));
    }

    [Fact]
    public void ModeOrNonfiniteSpeedCannotPretendToBeStopped()
    {
        Assert.False(VoyagerNativeIdleHandoffPolicy.PhysicallyAtRest(false, true, 0, 0, 0, 0, 0, 0, 0));
        Assert.False(VoyagerNativeIdleHandoffPolicy.PhysicallyAtRest(true, false, 0, 0, 0, 0, 0, 0, 0));
        Assert.False(VoyagerNativeIdleHandoffPolicy.PhysicallyAtRest(true, true, double.NaN, 0, 0, 0, 0, 0, 0));
        Assert.False(VoyagerNativeIdleHandoffPolicy.PhysicallyAtRest(true, true, .11, 0, 0, 0, 0, 0, 0));
    }
}
