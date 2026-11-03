using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class MinimalDysonTargetPolicyTests
{
    [Theory]
    [InlineData("valid")][InlineData("stock")][InlineData("flight")][InlineData("extra_node")]
    [InlineData("coverage")][InlineData("extra_silo")][InlineData("layer")][InlineData("prototype")]
    [InlineData("unloaded_asset")][InlineData("latitude")][InlineData("nan_radius")][InlineData("hash")]
    [InlineData("foreign_entity")][InlineData("mixed_parameters")]
    public void OnePristineNativeReceivingPlanRequiresTheExactCompleteEmptySiloAndLayer(string fault)
    {
        var e=new FactoryEntitySnapshot {PlanetId=7,ObjectId=5,ItemId=2312,ComponentKind="silo",ObjectKind=FactoryObjectKinds.Entity,
            Silo=new() {NativeBindingVerified=true,SiloComponentId=2,StarId=11,SphereAvailable=true,NodeCoverageComplete=true,
                RocketCoverageComplete=true,InFlightRocketCount=0,StarSiloCoverageComplete=true,StarSiloCount=1}};
        var t=new MinimalDysonTargetSnapshot {StarId=11,LayerId=1,Radius=10000,NodePrototypeId=0,
            ExactExistingLayerBinding=true,CompleteEmptyStructureCoverage=true,NativePrototypeAlreadyLoaded=true,NativeLatitudeAllowsEquator=true};
        t.ConfigurationStateHash=MinimalDysonTargetPolicy.Hash(e,t);
        var r=new PrepareConfigureBuildingRequest {PlanetId=7,EntityId=5,Mode=MinimalDysonTargetPolicy.Mode,ExpectedFactoryStateHash=t.ConfigurationStateHash};
        switch(fault)
        {
            case "stock":e.Silo.RocketCount=1;break;case "flight":e.Silo.InFlightRocketCount=1;break;
            case "extra_node":e.Silo.Nodes.Add(new(){NodeId=1,LayerId=1});break;
            case "coverage":e.Silo.NodeCoverageComplete=false;break;case "extra_silo":e.Silo.StarSiloCount=2;break;
            case "layer":t.LayerId=2;break;case "prototype":t.NodePrototypeId=1;break;
            case "unloaded_asset":t.NativePrototypeAlreadyLoaded=false;break;case "latitude":t.NativeLatitudeAllowsEquator=false;break;
            case "nan_radius":t.Radius=float.NaN;break;case "hash":r.ExpectedFactoryStateHash="stale";break;
            case "foreign_entity":e.ObjectId=6;break;case "mixed_parameters":r.StationItemId=1;break;
        }
        Assert.Equal(fault!="valid",MinimalDysonTargetPolicy.Validate(r,e,t) is not null);
        Assert.Equal(1,t.PlannedNodeCount);Assert.Equal(0,t.PlannedDeliveredStructurePoints);Assert.Equal(30,t.PlannedRequiredStructurePoints);
    }
}
