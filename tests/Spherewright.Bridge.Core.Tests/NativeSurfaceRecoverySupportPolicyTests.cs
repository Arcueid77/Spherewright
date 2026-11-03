using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeSurfaceRecoverySupportPolicyTests
{
    [Fact]
    public void ContactBoundaryMayUsePositiveOwnedOverlapAndSupportingRayWithoutInventingGround()
    {
        bool? nativeGround = false;
        Assert.True(NativeSurfaceRecoverySupportPolicy.Proved(nativeGround, 1, true, true, true));
        Assert.False(nativeGround.Value);
        Assert.False(NativeSurfaceRecoverySupportPolicy.Proved(nativeGround, 1, true, true, false));
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(256)]
    public void AirborneStepRequiresACompleteForeignFreeCutAndPositiveDownwardSupport(int count)
    {
        Assert.True(NativeSurfaceRecoverySupportPolicy.Proved(false, count, true, true, true));
        Assert.False(NativeSurfaceRecoverySupportPolicy.Proved(false, count, false, true, true));
        Assert.False(NativeSurfaceRecoverySupportPolicy.Proved(false, count, true, false, true));
    }

    [Theory]
    [InlineData(-1)][InlineData(257)][InlineData(1000)]
    public void SaturationOrInvalidCountCannotBorrowAPositiveCast(int count)
        => Assert.False(NativeSurfaceRecoverySupportPolicy.Proved(false, count, true, true, true));

    [Fact]
    public void MissingNativeQueryAndGroundedEmptyCutRefuse()
    {
        Assert.False(NativeSurfaceRecoverySupportPolicy.Proved(null, 1, true, true, true));
        Assert.False(NativeSurfaceRecoverySupportPolicy.Proved(true, null, true, true, true));
        Assert.False(NativeSurfaceRecoverySupportPolicy.Proved(true, 0, true, true, true));
        Assert.True(NativeSurfaceRecoverySupportPolicy.Proved(true, 1, true, true, false));
    }
}
