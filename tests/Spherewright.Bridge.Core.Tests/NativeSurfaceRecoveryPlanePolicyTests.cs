using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeSurfaceRecoveryPlanePolicyTests
{
    [Fact]
    public void OwnedSideHitIsRetainedWhileIndependentCurrentTerrainMustProveTheUnchangedPlaneBounds()
    {
        // The copied broad-hit pattern is sanitized; the terrain ray remains
        // a synthetic premise here, not an invented live ray observation.
        var first=Side();var ray=Terrain();var selected=NativeSurfaceRecoveryPlanePolicy.Select(0,true,true,first,()=>ray,out var used);
        Assert.True(used);Assert.Same(ray,selected);Assert.Equal(.42,first.NormalDotUp);
        Assert.False(NativeSurfaceRecoveryPlanePolicy.Supporting(first));
        Assert.True(NativeSurfaceRecoverySupportPolicy.CutProved(false,0,true,0,0,selected.CurrentOwnerProved,selected.NormalDotUp,selected.HeightFromActor));
        // A supporting plane still cannot invent native ground or idle.
        Assert.False(NativeSurfaceRecoverySupportPolicy.CutProved(true,0,true,0,0,selected.CurrentOwnerProved,selected.NormalDotUp,selected.HeightFromActor));
    }

    [Theory]
    [InlineData("foreign_first")][InlineData("missing_first")][InlineData("unknown_first")]
    [InlineData("invalid_first")]
    [InlineData("positive_foot")][InlineData("incomplete_foot")][InlineData("foreign_foot")]
    public void IndependentQueryCannotBypassAnIncompleteForeignOrAlreadyOverlappedCut(string fault)
    {
        var first=Side();var count=0;var complete=true;var owned=true;
        switch(fault){case "foreign_first":first.CurrentOwnerProved=false;break;case "missing_first":first.Hit=false;break;
            case "unknown_first":first.NormalDotUp=null;break;case "invalid_first":first.NormalDotUp=2;break;case "positive_foot":count=1;break;
            case "incomplete_foot":complete=false;break;case "foreign_foot":owned=false;break;}
        var calls=0;var selected=NativeSurfaceRecoveryPlanePolicy.Select(count,complete,owned,first,()=>{calls++;return Terrain();},out var used);
        Assert.False(used);Assert.Equal(0,calls);Assert.Same(first,selected);
    }

    [Theory]
    [InlineData("missing")][InlineData("foreign")][InlineData("object_only")][InlineData("steep")]
    [InlineData("normal_unknown")][InlineData("normal_nan")][InlineData("too_low")][InlineData("too_high")]
    public void AlternativeRayDoesNotLowerNormalHeightOrCurrentTerrainRequirements(string fault)
    {
        var first=Side();var ray=Terrain();switch(fault){case "missing":ray.Hit=false;break;case "foreign":ray.CurrentOwnerProved=false;break;
            case "object_only":ray.CurrentTerrainProved=false;break;case "steep":ray.NormalDotUp=.499;break;
            case "normal_unknown":ray.NormalDotUp=null;break;case "normal_nan":ray.NormalDotUp=double.NaN;break;
            case "too_low":ray.HeightFromActor=-2.001;break;case "too_high":ray.HeightFromActor=.201;break;}
        Assert.Same(first,NativeSurfaceRecoveryPlanePolicy.Select(0,true,true,first,()=>ray,out var used));Assert.False(used);
    }

    [Fact]
    public void ExistingSupportingSphereKeepsItsOriginalQueryAndDoesNotAcquireASecondSample()
    {
        var first=Terrain();Assert.Same(first,NativeSurfaceRecoveryPlanePolicy.Select(0,true,true,first,
            ()=>throw new InvalidOperationException("Unexpected second query"),out var used));Assert.False(used);
    }
    private static NativeSurfaceRecoveryPlane Side()=>new(){Hit=true,CurrentOwnerProved=true,NormalDotUp=.42,HeightFromActor=-.02};
    private static NativeSurfaceRecoveryPlane Terrain()=>new(){Hit=true,CurrentOwnerProved=true,CurrentTerrainProved=true,NormalDotUp=.99,HeightFromActor=-.24};
}
