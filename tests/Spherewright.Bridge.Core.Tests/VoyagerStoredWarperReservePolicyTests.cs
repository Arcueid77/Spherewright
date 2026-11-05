using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerStoredWarperReservePolicyTests
{
    [Theory]
    [InlineData(2,1,1,1,false,3)][InlineData(3,1,1,1,true,3)]
    [InlineData(20,1,1,1,true,3)][InlineData(1,1,0,1,false,2)][InlineData(2,1,0,1,true,2)]
    public void EveryStageUsesActualChamberCounts(int stored,int current,int returning,int extra,bool allowed,int required)
    {
        var result=VoyagerStoredWarperReservePolicy.Evaluate(stored,current,returning,extra);
        Assert.Equal(allowed,result.Proved);Assert.Equal(required,result.RequiredWarpers);
        if(!allowed)Assert.Equal("warper_return_reserve_insufficient",result.Code);
    }
    [Theory]
    [InlineData(-1,1,1,1)][InlineData(20,0,1,1)][InlineData(20,1,-1,1)][InlineData(20,1,1,-1)]
    [InlineData(20,int.MaxValue,int.MaxValue,1)]
    public void InvalidOrOverflowedReservationsNeverAdmit(int stored,int current,int returning,int extra)
        =>Assert.False(VoyagerStoredWarperReservePolicy.Evaluate(stored,current,returning,extra).Proved);
}
