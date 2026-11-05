using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Celestial;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerWallWindowPolicyTests
{
    private static readonly DateTimeOffset Issued=new(2032,1,2,3,4,5,TimeSpan.Zero);
    [Fact]
    public void LegacyWindowsKeepTheirOldBoundsAndExplicitWindowsBindOneExactDeadline()
    {
        Assert.Equal(Issued.AddSeconds(3720),VoyagerWallWindowPolicy.OutboundDeadline(Issued,108000,null));
        Assert.True(VoyagerWallWindowPolicy.ValidRecord(Issued,Issued.AddHours(2),null));
        Assert.False(VoyagerWallWindowPolicy.ValidRecord(Issued,Issued.AddHours(2).AddTicks(1),null));
        Assert.Equal(Issued.AddHours(12),VoyagerWallWindowPolicy.OutboundDeadline(Issued,108000,43200));
        Assert.True(VoyagerWallWindowPolicy.ValidRecord(Issued,Issued.AddHours(12),43200));
        Assert.False(VoyagerWallWindowPolicy.ValidRecord(Issued,Issued.AddHours(12).AddTicks(1),43200));
        Assert.False(VoyagerWallWindowPolicy.ValidRecord(Issued,Issued.AddHours(12),null));
    }

    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(599)][InlineData(86401)][InlineData(int.MaxValue)]
    public void UnboundedOrTooShortExplicitWindowsAreRejected(int seconds)
    {
        Assert.False(VoyagerWallWindowPolicy.ValidExplicit(seconds));
        Assert.Throws<ArgumentOutOfRangeException>(()=>VoyagerWallWindowPolicy.OutboundDeadline(Issued,108000,seconds));
    }

    [Fact]
    public void AReturnCannotExtendItsOriginalWindowByChangingItsPreview()
    {
        var r=new PreviewVoyagerRouteRequest{PlanetId=901,DestinationPlanetId=902,
            ExpectedPlayerStateHash="player",ExpectedCatalogStateHash="catalog",
            JourneyId="00000000-0000-0000-0000-000000000001",MaximumRoundTripWallSeconds=43200};
        Assert.Contains("return inherits",VoyagerRouteBudgetPolicy.ValidateRequest(r));
        r.MaximumRoundTripWallSeconds=null;Assert.Null(VoyagerRouteBudgetPolicy.ValidateRequest(r));
    }
}
