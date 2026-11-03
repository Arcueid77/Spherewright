using Newtonsoft.Json.Linq;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeTerrainSettlingReplayTests
{
    internal static JObject Cut()=>JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","native-terrain-settling-cut.json")));

    [Fact]
    public void CapturedLoadedTerrainSelectsOneFiniteCadenceEnvelopeAcrossItsNativeLayerTransition()
    {
        var cut=Cut();var source=cut["sourceSupport"]!.ToObject<NativeNearGroundSupportSnapshot>()!;
        var after=cut["afterFirst151"]!.ToObject<NativeNearGroundSupportSnapshot>()!;
        var now=DateTimeOffset.UtcNow;
        var policy=NativeResumeContactPolicy.ForNativeSupport(100,now,source,cut.Value<double>("fixedDeltaTimeSeconds"));
        Assert.False(source.ActualGroundSphereHit);Assert.Equal(31,source.Layer);Assert.Equal(9,after.Layer);
        Assert.True(policy.UsesTerrainSettling);Assert.Equal(32,policy.WorkTickLimit);Assert.Equal(5,policy.WallSecondsLimit);
        var observedSpeed=cut["failure"]!.Value<double>("speedMetersPerSecond");
        Assert.True(observedSpeed>NativeResumeContactPolicy.MaximumSpeedMetersPerSecond);
        Assert.True(observedSpeed<policy.SpeedLimit);
        // The old terminal's missing exact foot cut is still missing. No
        // paused readback retroactively proves ownership or successful idle.
        Assert.Equal(JTokenType.Null,cut["failure"]!["footOwnershipAtFailure"]!.Type);
        source.CurrentPoolReference=null;
        Assert.False(NativeResumeContactPolicy.ForNativeSupport(100,now,source,.125).UsesTerrainSettling);
        source.CurrentPoolReference=true;source.IsTrigger=true;
        Assert.False(NativeResumeContactPolicy.ForNativeSupport(100,now,source,.125).UsesTerrainSettling);
    }
}
