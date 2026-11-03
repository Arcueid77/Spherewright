using Spherewright.Contracts.Actions;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Theory][InlineData(1,true)][InlineData(2,false)][InlineData(null,false)]
    public async Task VesselDeliverySettingRequiresTheExactNewPreparedEcho(int? echo,bool valid)
    {
        var bridge=new FakeBridgeClient(SuccessResult()) {ConfigurePlan=new PreparedNormalAction
        {Prepared=true,CommitAllowedNow=true,PlanToken="synthetic-delivery-token",PlannedStationVesselDeliveryMinimumPercent=echo}};
        var result=await SpherewrightTools.PrepareConfigureBuildingAsync(bridge,"synthetic-session",7,9,0,"synthetic-factory-hash",
            mode:BuildingConfigurationModes.LogisticsStationDelivery,stationVesselDeliveryMinimumPercent:1,
            expectedStationConfigurationStateHash:"synthetic-station-hash");
        Assert.Equal(!valid,result.IsError);
        if(!valid)Assert.DoesNotContain("synthetic-delivery-token",result.StructuredContent!.Value.GetRawText());
    }
}
