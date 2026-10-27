using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeTerrainInitializationFrameTests
{
    [Fact]
    public void OnePaidInitializationFrameStillRejectsMissingActualGroundAtTheRealPhysicsBoundary()
    {
        var p=new NativeFirstFrameAdmission();
        Assert.Null(p.ArmTerrainInitialization(100,true,true,true,true,()=>"actual_ground_unproved"));
        Assert.False(p.UniverseObserved);Assert.False(p.PhysicsObserved);
        Assert.Null(p.ObserveUniverse(101,true,true,true));
        Assert.Equal("actual_ground_unproved",p.ObservePhysics(101,true));
        Assert.False(p.PhysicsObserved);Assert.Equal("actual_ground_unproved",p.Failure);
    }
    [Theory]
    [InlineData(false,true,true)] [InlineData(true,false,true)] [InlineData(true,true,false)]
    public void MissingLoaderTerrainOrFundingCannotArmAnyNativeFrame(bool loader,bool terrain,bool funded)
    {
        var p=new NativeFirstFrameAdmission();Assert.NotNull(p.ArmTerrainInitialization(100,loader,true,terrain,funded,()=>null));
        Assert.False(p.Armed);Assert.False(p.UniverseObserved);Assert.False(p.PhysicsObserved);
    }
    [Theory]
    [InlineData(200d,199.5d,true)] [InlineData(200d,200.5d,true)]
    [InlineData(200.379d,200d,true)]
    [InlineData(200d,199.4999d,false)] [InlineData(double.NaN,200d,false)] [InlineData(null,200d,false)]
    public void NativeHeightIsOnlyABoundedConditionalEntryAndNeverAnUnknownOrUnlimitedDrop(double? player,double? surface,bool expected)
        => Assert.Equal(expected,NativeFirstFrameAdmission.TerrainInitializationHeightIsBounded(player,surface));
}
