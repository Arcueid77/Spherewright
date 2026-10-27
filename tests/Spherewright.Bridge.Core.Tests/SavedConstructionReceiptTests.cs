using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class SavedConstructionReceiptTests
{
    private static BlueprintBuildState Receipt()
    {
        var site=new BlueprintSiteSnapshot { SessionId="synthetic-session",PlanetId=201,CapturedAtGameTick=100,
            BlueprintHash="synthetic-site",NativeCheckPerformed=true,NativeCheckPassed=true,InventorySufficient=true,TechnologySatisfied=true,
            Objects=[new() { Index=0,ItemId=2104,NativeCondition="Ok" }] };
        var provenance=new StructuredConstructionBinding { Kind="core",JourneyId=Guid.NewGuid().ToString(),
            NativePlanHash="sha256:"+new string('b',64),Steps=[new()] };
        var b=BlueprintBuildState.Create(site,structuredConstruction:provenance);
        b.ConstructionControl=StructuredConstructionControlTests.Control();
        var action=Guid.NewGuid().ToString();b.ConstructionControl.NativeCreateActionId=action;
        b.Begin("synthetic-session",action,1,100);b.BeforeStructuredSubmission(101,1,"before");b.ConfirmStructuredSubmission([1],0,"after");
        b.ConstructionControl.RequestStop("original_binding_failed",110,b.ConstructionControl.StartedAtUtc.AddSeconds(2),true);
        b.ConstructionControl.DispatchDisabled=true;b.Stop("outcome_unknown");return b;
    }

    [Fact]
    public void OriginalUnknownStoppedTowerReceiptIsEligibleForLaterNativeProofWithoutRenewingItsWindow()
    {
        var b=Receipt();var c=b.ConstructionControl!;var hash=c.EvidenceHash();
        Assert.True(NormalGameActionCoordinator.SavedTowerReceiptForTest(b,b.StructuredConstruction!.JourneyId,120));
        Assert.Equal("outcome_unknown",b.Phase);Assert.True(c.BindingLost);Assert.False(c.PhysicalIdleVerified);
        Assert.Equal(hash,c.EvidenceHash());Assert.Null(c.RemainingWorkGameTicks(120,c.WorkDeadlineUtc.AddSeconds(1)));
    }

    [Theory]
    [InlineData("debit")] [InlineData("create")] [InlineData("dispatch")] [InlineData("target")]
    [InlineData("unknown_submission")] [InlineData("original_switch")] [InlineData("future")]
    [InlineData("other_journey")] [InlineData("running")] [InlineData("drain")]
    public void MissingOrAmbiguousOriginalReceiptCannotAuthorizeAnAlreadyReturningTail(string fault)
    {
        var b=Receipt();var journey=b.StructuredConstruction!.JourneyId;
        switch(fault)
        {
            case "debit":b.Objects[0].InventoryAfter=1;break;case "create":b.ConstructionControl!.NativeCreateActionId=null;break;
            case "dispatch":b.ConstructionControl!.DispatchDisabled=false;break;case "target":b.Site.Objects[0].ItemId=2201;break;
            case "unknown_submission":b.Objects[0].State=BlueprintObjectStates.OutcomeUnknown;break;
            case "original_switch":b.ConstructionControl!.OriginalDroneEnabled=false;break;
            case "future":b.LastEvidenceGameTick=121;break;case "other_journey":journey=Guid.NewGuid().ToString();break;
            case "running":b.Phase="running";break;case "drain":b.ConstructionControl!.DrainStartedAtGameTick=null;break;
        }
        Assert.False(NormalGameActionCoordinator.SavedTowerReceiptForTest(b,journey,120));
    }
}
