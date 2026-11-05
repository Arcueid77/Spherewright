using Newtonsoft.Json.Linq;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Logistics;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class StationCargoWithdrawalPolicyTests
{
    private static LogisticsStationSnapshot Cut(string name)=>JObject.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory,"Fixtures","native-station-cargo-delivery.json")))[name]!
        .ToObject<LogisticsStationSnapshot>()!;
    private static string? Check(LogisticsStationSnapshot? station,int item=1802,int count=1,
        bool binding=true,bool hand=true,bool range=true,bool capacity=true,int inc=0)
        => StationCargoWithdrawalPolicy.Blocker(station,77,item,count,binding,hand,range,capacity,inc);

    [Fact]
    public void ActualOrdersAreNotCargoAndRealPassiveArrivalsRequireNoInventedPower()
    {
        var pending=Cut("inflight");var boards=Cut("boardsArrived");var delivered=Cut("allArrived");
        Assert.Equal(0,delivered.PowerNetworkId);Assert.Equal(0,delivered.Energy);
        Assert.NotNull(Check(pending,1301,3));Assert.NotNull(Check(pending,1802,1));
        Assert.Null(Check(boards,1301,3));Assert.NotNull(Check(boards,1802,1));
        Assert.Null(Check(delivered,1301,3));Assert.Null(Check(delivered,1802,1));
        Assert.Equal(100,delivered.StorageSlots.Single(s=>s.ItemId==1802).Count);
    }

    [Theory]
    [InlineData("missing")][InlineData("binding")][InlineData("entity")][InlineData("collector")]
    [InlineData("hand")][InlineData("range")][InlineData("capacity")][InlineData("package_inc")]
    [InlineData("station_inc")][InlineData("count0")][InlineData("count101")][InlineData("shortage")]
    [InlineData("order")][InlineData("negative_order")][InlineData("duplicate_item")][InlineData("duplicate_slot")]
    public void NativeAndPhysicalFailuresNeverBecomePermissionToRemoveOrCreateStock(string fault)
    {
        var s=Cut("allArrived");var slot=s.StorageSlots.Single(x=>x.ItemId==1802);
        switch(fault)
        {
            case "missing":Assert.NotNull(Check(null));return;
            case "binding":Assert.NotNull(Check(s,binding:false));return;
            case "entity":s.EntityId++;break;
            case "collector":s.IsCollector=true;break;
            case "hand":Assert.NotNull(Check(s,hand:false));return;
            case "range":Assert.NotNull(Check(s,range:false));return;
            case "capacity":Assert.NotNull(Check(s,capacity:false));return;
            case "package_inc":Assert.NotNull(Check(s,inc:1));return;
            case "station_inc":slot.Inc=1;break;
            case "count0":Assert.NotNull(Check(s,count:0));return;
            case "count101":Assert.NotNull(Check(s,count:101));return;
            case "shortage":slot.Count=0;slot.RemoteOrder=100;break;
            case "order":slot.RemoteOrder=1;break;
            case "negative_order":slot.LocalOrder=-1;break;
            case "duplicate_item":s.StorageSlots[3].ItemId=1802;break;
            case "duplicate_slot":s.StorageSlots[3].Index=slot.Index;break;
        }
        Assert.NotNull(Check(s));
    }

    [Fact]
    public void PhysicalUIStockAndOrdersRemainUnmodifiedDuringPrepareAdmission()
    {
        var s=Cut("allArrived");var before=Newtonsoft.Json.JsonConvert.SerializeObject(s);
        Assert.Null(Check(s,1802,100));Assert.NotNull(Check(s,1802,101));
        Assert.Equal(before,Newtonsoft.Json.JsonConvert.SerializeObject(s));
    }
}
