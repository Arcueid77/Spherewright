using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeConstructionRecoveryStateTests
{
    [Fact]
    public void CapturedOwnMeshPlusOwnOceanTriggerRoutesOnlyAnIndependentSolidProbe()
    {
        // Sanitized actual loaded cut; Native ground remains false and the
        // ocean trigger contributes no solid support or idle permission.
        var solid=new NativeTerrainFootHitPhysicsSnapshot {State="observed",Type="mesh",Enabled=true,ActiveInHierarchy=true,
            Layer=31,SameDefaultPhysicsScene=true,UnknownOwner=false,IsTrigger=false,IsCurrentSurface=true,
            CurrentSurfaceUnityMatchCount=1,UniqueSurfaceIndex=3,SharedMeshPresent=true};
        var ocean=new NativeTerrainFootHitPhysicsSnapshot {State="observed",Type="sphere",Enabled=true,ActiveInHierarchy=true,
            Layer=31,SameDefaultPhysicsScene=true,UnknownOwner=false,IsTrigger=true,SameCurrentOcean=true,SameCurrentOceanUnity=true,IsCurrentSurface=false};
        var s=new NativePlanetTerrainPhysicsSnapshot {State="observed",Complete=true,RootLayer=31,BodyLayer=31,
            NativeGroundSphereHit=false,FootOverlapComplete=true,FootHitDetailsComplete=true,FootOverlapCount=2,FootHits=[solid,ocean]};
        Assert.True(NativeLoadedSolidContactRoutePolicy.MayProbe(s));Assert.False(s.NativeGroundSphereHit);
        ocean.SameCurrentOcean=false;Assert.False(NativeLoadedSolidContactRoutePolicy.MayProbe(s));
        ocean.SameCurrentOcean=true;solid.IsCurrentSurface=false;Assert.False(NativeLoadedSolidContactRoutePolicy.MayProbe(s));
        solid.IsCurrentSurface=true;s.FootHitDetailsComplete=false;Assert.False(NativeLoadedSolidContactRoutePolicy.MayProbe(s));
    }

    [Fact]
    public void BroadSphereAndZeroSpeedDoNotCertifyAnUnsettledQuarterMetreGap()
    {
        Assert.False(NativeConstructionStandstillPolicy.SupportProved(true,-.23366366,.99));
        Assert.True(NativeConstructionStandstillPolicy.SupportProved(true,-.02,.99));
        Assert.True(NativeConstructionStandstillPolicy.SupportProved(true,-.14,.99));
        Assert.False(NativeConstructionStandstillPolicy.SupportProved(true,-.211,.99));
        Assert.False(NativeConstructionStandstillPolicy.StableProved(3,0,0));
        Assert.True(NativeConstructionStandstillPolicy.StableProved(4,0,0));
        Assert.False(NativeConstructionStandstillPolicy.StableProved(4,0,.2585055));
        Assert.False(NativeConstructionStandstillPolicy.SupportProved(false,0,1));
        Assert.False(NativeConstructionStandstillPolicy.SupportProved(true,double.NaN,1));
    }

    [Theory]
    [InlineData(1)][InlineData(2)][InlineData(3)]
    public void ExistingWorkTargetsMustBeOnlyTheRetainedPaidPrebuilds(int stage)
    {
        Assert.True(NativeSavedPaidDroneTargetPolicy.Proved(stage,-1,[0,0,0],[-1]));
        Assert.False(NativeSavedPaidDroneTargetPolicy.Proved(stage,1,[0,0,0],[-1]));
        Assert.False(NativeSavedPaidDroneTargetPolicy.Proved(stage,-2,[0,0,0],[-1]));
        Assert.False(NativeSavedPaidDroneTargetPolicy.Proved(stage,-1,[0,-2,0],[-1]));
        Assert.True(NativeSavedPaidDroneTargetPolicy.Proved(4,0,[0,0,0],[-1]));
        Assert.False(NativeSavedPaidDroneTargetPolicy.Proved(4,-1,[0,0,0],[-1]));
    }
}
