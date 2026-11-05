using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerJourneyStoreIntegrationTests
{
    [Fact]
    public void NewFiniteWallRecordReopensExactlyAndCannotBeChangedDuringProgress()
    {
        using var f=new Fixture(maximumRoundTripWallSeconds:43200);
        var reopened=f.ReopenStore();Assert.Null(reopened.VoyagerJourneyLoadError);
        var restored=reopened.VoyagerJourney!;
        Assert.Equal(43200,restored.MaximumRoundTripWallSeconds);
        Assert.Equal(f.Record.DeadlineUtc,restored.DeadlineUtc);
        AssertOriginalReservations(f.Record,restored);
        var originalBytes=File.ReadAllBytes(f.JourneyPath);
        var changed=Copy(restored,r=>{r.MaximumRoundTripWallSeconds=86400;r.DeadlineUtc=r.IssuedAtUtc.AddHours(24);});
        Assert.False(reopened.TryPersistVoyagerJourney(changed,out _));
        Assert.Equal(originalBytes,File.ReadAllBytes(f.JourneyPath));
        changed=Copy(restored,r=>r.MaximumRoundTripWallSeconds=null);
        Assert.False(reopened.TryPersistVoyagerJourney(changed,out _));
        Assert.Equal(originalBytes,File.ReadAllBytes(f.JourneyPath));
    }

    [Fact]
    public void LegacyJourneyBytesDoNotAcquireTheNewOptionalWallField()
    {
        using var f=new Fixture();
        Assert.Null(f.Record.MaximumRoundTripWallSeconds);
        Assert.DoesNotContain("maximumRoundTripWallSeconds",PluginJson.Serialize(f.Record));
        Assert.DoesNotContain("maximumRoundTripWallSeconds",File.ReadAllText(f.JourneyPath));
    }
}
