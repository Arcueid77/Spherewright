using Newtonsoft.Json.Linq;
using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class SurfaceRecoveryCapturedCutReplayTests
{
    [Fact]
    public void CapturedLayer11FailureRemainsUnprovedUntilNewOwnerAndPlaneAreObserved()
    {
        var cut = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "surface-recovery-contact-cut.json")));
        var count = cut["values"]!.Value<int>("surface_support_foot_count");
        var ground = cut["observations"]!.Value<string>("surface_support_native_ground") == "true";
        Assert.Equal(2, count);
        Assert.Equal(11, cut["values"]!.Value<int>("surface_support_foreign_layer"));
        Assert.False(NativeSurfaceRecoverySupportPolicy.CutProved(ground, count, true, 1, 0, false, null, null));
        // A newly observed fully registered contact is admissible, but it
        // cannot retroactively supply the old receipt's unobserved cast.
        Assert.False(NativeSurfaceRecoverySupportPolicy.CutProved(ground, count, true, 1, 1, true, null, null));
        Assert.True(NativeSurfaceRecoverySupportPolicy.CutProved(ground, count, true, 1, 1, true, .85, -.1));
        Assert.False(cut["observations"]!.Value<string>("surface_strict_grounded_walk_zero_tail") == "true");
    }

    [Theory]
    [InlineData(false, 0, 0, .85, -.2, true)]
    [InlineData(false, 1, 0, .85, -.2, true)]
    [InlineData(true, 1, 1, .85, -.2, true)]
    [InlineData(true, 0, 0, .85, -.2, false)]
    [InlineData(true, 1, 0, .49, -.2, false)]
    [InlineData(true, 1, 0, .85, .21, false)]
    [InlineData(true, 1, 0, .85, -2.01, false)]
    [InlineData(true, 1, 0, double.NaN, -.2, false)]
    public void ContactStatesUseCountsAndGeometryRatherThanAnAllTrueGroundStub(bool ground, int terrain, int natural,
        double normal, double height, bool expected)
        => Assert.Equal(expected, NativeSurfaceRecoverySupportPolicy.CutProved(ground, terrain + natural, true,
            terrain, natural, true, normal, height));

    [Fact]
    public void ForeignIncompleteAndSideContactCannotBorrowThePositiveNativeSphere()
    {
        Assert.False(NativeSurfaceRecoverySupportPolicy.CutProved(true, 2, true, 1, 0, true, .9, 0));
        Assert.False(NativeSurfaceRecoverySupportPolicy.CutProved(true, 2, false, 1, 1, true, .9, 0));
        Assert.False(NativeSurfaceRecoverySupportPolicy.CutProved(true, 2, true, 1, 1, false, .9, 0));
    }
}
