using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Logistics;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class LogisticsStationDeliveryPolicyTests
{
    private static (PrepareConfigureBuildingRequest,LogisticsStationSnapshot) Fixture()
    {
        var s=new LogisticsStationSnapshot {EntityId=7,IsInterstellar=true,VesselDeliverySetting=100,
            WorkingVesselCount=0,WorkingDroneCount=0,ConfigurationStateHash="captured-config"};
        return (new() {EntityId=7,Mode=BuildingConfigurationModes.LogisticsStationDelivery,
            StationVesselDeliveryMinimumPercent=1,ExpectedStationConfigurationStateHash=s.ConfigurationStateHash},s);
    }

    [Theory][InlineData(1)][InlineData(25)][InlineData(99)]
    public void IdleNativePercentageRequiresTheExactDistinctConfigurationAndEcho(int percent)
    {
        var (r,s)=Fixture();r.StationVesselDeliveryMinimumPercent=percent;
        Assert.True(LogisticsStationDeliveryPolicy.CanApply(r,s));
        Assert.False(LogisticsStationDeliveryPolicy.ConfirmsEcho(r,new()));
        Assert.True(LogisticsStationDeliveryPolicy.ConfirmsEcho(r,new(){PlannedStationVesselDeliveryMinimumPercent=percent}));
        s.VesselDeliverySetting=percent;Assert.False(LogisticsStationDeliveryPolicy.CanApply(r,s));
    }

    [Theory][InlineData("zero")][InlineData("over")][InlineData("hash")][InlineData("foreign")]
    [InlineData("active_ship")][InlineData("active_drone")][InlineData("collector")][InlineData("item")][InlineData("recipe")]
    public void NoCargoFleetOrOtherSettingCanBeSmuggledThroughThePercentage(string fault)
    {
        var(r,s)=Fixture();
        switch(fault)
        {
            case "zero":r.StationVesselDeliveryMinimumPercent=0;break;
            case "over":r.StationVesselDeliveryMinimumPercent=101;break;
            case "hash":r.ExpectedStationConfigurationStateHash="stale";break;
            case "foreign":s.EntityId=8;break;case "active_ship":s.WorkingVesselCount=1;break;
            case "active_drone":s.WorkingDroneCount=1;break;case "collector":s.IsCollector=true;break;
            case "item":r.StationItemId=1301;break;case "recipe":r.RecipeId=1;break;
        }
        Assert.False(LogisticsStationDeliveryPolicy.CanApply(r,s));
    }

    [Fact]
    public void NativeDispatchThresholdUsesTheRealPercentWithoutFillingOrInventingCargo()
    {
        // Native29151 computes(shipCarries-1)*deliveryShips/100. A finite
        // packet below a full vessel must lower the UI setting, not fake load.
        const int carries=200,packet=100;
        Assert.True(packet<=(carries-1)*100/100);
        Assert.True(packet>(carries-1)*1/100);
        var(r,s)=Fixture();var before=CanonicalStateHash.LogisticsStationConfiguration(s);
        Assert.True(LogisticsStationDeliveryPolicy.CanApply(r,s));Assert.Equal(before,CanonicalStateHash.LogisticsStationConfiguration(s));
    }
}
