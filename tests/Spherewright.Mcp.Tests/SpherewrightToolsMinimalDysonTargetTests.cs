using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Spherewright.Mcp.Tools;
using Xunit;

namespace Spherewright.Mcp.Tests;

public sealed partial class SpherewrightToolsTests
{
    [Theory]
    [InlineData("valid")][InlineData("missing")][InlineData("hash")][InlineData("extra_layer")][InlineData("unproved")]
    public async Task MinimalReceivingTargetRequiresExactPreparedEchoBeforeExposingAToken(string fault)
    {
        const string hash="sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var target=new MinimalDysonTargetSnapshot {StarId=11,LayerId=1,Radius=10000,ExactExistingLayerBinding=true,
            CompleteEmptyStructureCoverage=true,NativePrototypeAlreadyLoaded=true,NativeLatitudeAllowsEquator=true,ConfigurationStateHash=hash};
        var plan=new PreparedNormalAction {Prepared=true,CommitAllowedNow=true,ActionKind=NormalActionKinds.ConfigureBuilding,
            PlanToken="synthetic-minimal-node-token",PlannedMinimalDysonTarget=target};
        if(fault=="missing")plan.PlannedMinimalDysonTarget=null;
        if(fault=="hash")target.ConfigurationStateHash="changed";
        if(fault=="extra_layer")target.LayerId=2;
        if(fault=="unproved")target.CompleteEmptyStructureCoverage=false;
        var bridge=new FakeBridgeClient(SuccessResult()) {ConfigurePlan=plan};
        var result=await SpherewrightTools.PrepareConfigureBuildingAsync(bridge,"synthetic-session",7,5,0,hash,
            mode:BuildingConfigurationModes.MinimalDysonReceivingNode);
        Assert.Equal(fault!="valid",result.IsError);
        if(fault!="valid")Assert.DoesNotContain("synthetic-minimal-node-token",result.StructuredContent!.Value.GetRawText());
    }
}
