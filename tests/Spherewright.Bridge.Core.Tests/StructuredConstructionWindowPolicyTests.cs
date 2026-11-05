using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class StructuredConstructionWindowPolicyTests
{
    [Fact]
    public void DistinctPaidWindowFundsItsNewAnchorWhileOrdinaryContinuationRetainsTheOriginal()
    {
        var b=Build(false);var original=b.ConstructionControl!.Anchor;
        var now=b.ConstructionControl.StartedAtUtc.AddSeconds(61);var current=new Vector3Snapshot(){X=199.94f,Y=5};
        Assert.True(StructuredConstructionWindowPolicy.StartsNewPaidWindow(b,true,false,4000,now));
        var fresh=StructuredConstructionWindowPolicy.ReserveAnchor(b,current,true);
        Assert.Equal(current.Y,fresh.Y);Assert.NotSame(current,fresh);Assert.Same(original,b.ConstructionControl.Anchor);
        fresh.Y=7;Assert.Equal(5,current.Y);Assert.Equal(0,original.Y);
        var ordinary=StructuredConstructionWindowPolicy.ReserveAnchor(b,current,false);
        Assert.Equal(original.Y,ordinary.Y);Assert.NotSame(original,ordinary);
        // The actual reserve/energy policy still runs unchanged. Reusing a
        // five-metre-old eject point selected the conservative return bound
        // and needlessly exceeded core capacity despite a stationary new site.
        var old=Reserve(5);var next=Reserve(0);
        Assert.True(old.Proved);Assert.True(next.Proved);Assert.True(old.TailGameTicks>next.TailGameTicks);
        Assert.False(NativeConstructionDroneReservePolicy.CanFundNativeTail(1_600_000_000,20_000_000_000,5_000_000_000,old.TailAndNextTickEnergyJoules));
        Assert.True(NativeConstructionDroneReservePolicy.CanFundNativeTail(1_600_000_000,20_000_000_000,5_000_000_000,next.TailAndNextTickEnergyJoules));
    }

    [Theory]
    [InlineData("not_explicit")][InlineData("unexpired")][InlineData("working")][InlineData("unknown_unbound")]
    [InlineData("unknown_no_certificate")][InlineData("history_full")][InlineData("all_complete")]
    public void ANewAnchorCannotRenewAnUnprovedActiveOrUnexpiredWindow(string fault)
    {
        var unknown=fault.StartsWith("unknown",StringComparison.Ordinal);var b=Build(unknown);
        var allow=true;var bound=unknown;var tick=4000L;var now=b.ConstructionControl!.StartedAtUtc.AddSeconds(61);
        switch(fault)
        {
            case "not_explicit":allow=false;break;
            case "unexpired":tick=130;now=b.ConstructionControl.StartedAtUtc.AddSeconds(10);break;
            case "working":b.Phase="running";break;
            case "unknown_unbound":bound=false;break;
            case "unknown_no_certificate":b.SavedPaidStops.Clear();break;
            case "history_full":for(var i=0;i<16;i++)b.PreviousConstructionWindows.Add(StructuredConstructionControlTests.Control());break;
            case "all_complete":b.ConfirmCompletion(0,10,130);b.ConfirmCompletion(1,11,130);break;
        }
        Assert.False(StructuredConstructionWindowPolicy.StartsNewPaidWindow(b,allow,bound,tick,now));
    }

    [Fact]
    public void IndependentlyStoppedUnknownKeepsItsControlAndDebitWhenAReplacementAnchorIsSelected()
    {
        var b=Build(true);var c=b.ConstructionControl!;var original=c.EvidenceHash();var submission=SavedPaidConstructionStop.SubmissionHash(b);
        Assert.True(StructuredConstructionWindowPolicy.StartsNewPaidWindow(b,true,true,4000,c.StartedAtUtc.AddSeconds(61)));
        var anchor=StructuredConstructionWindowPolicy.ReserveAnchor(b,new(){X=199.94f,Y=5},true);
        Assert.Equal(5,anchor.Y);Assert.Equal(original,c.EvidenceHash());Assert.Equal(submission,SavedPaidConstructionStop.SubmissionHash(b));
        Assert.Equal("outcome_unknown",b.Phase);Assert.All(b.Objects,o=>Assert.Equal("pending_construction",o.State));
        Assert.False(StructuredConstructionWindowPolicy.StartsNewPaidWindow(b,true,true,130,c.StartedAtUtc.AddSeconds(10)));
    }

    private static NativeConstructionDroneReserve Reserve(double displacement)=>NativeConstructionDroneReservePolicy.Evaluate(new(){
        MaximumMechaDrones=3,NativeSpeedMetersPerSecond=10,NativeEnergyPerMeter=9000,NativeEjectEnergyJoules=60000,
        NativeRepairEnergyPerTick=3600,OtherMechaPowerUpperWatts=200_000_000,AnchorRadiusMeters=200,MaximumTargetDistanceMeters=10,
        NativePendingJumpTicks=0,FixedToLiveEjectDistanceMeters=displacement,
        IndependentMechaCosts=new(){EnergyShieldCapacity=0,EnergyShieldRechargeEnabled=false,LaserActive=false,AmmoItemId=0,
            AmmoSelectSlot=0,AmmoRateOfFire=0,AmmoEnergyCost=0,AmmoStorageEmptyAndUnfiltered=true,GroundFleetCount=0,SpaceFleetCount=0}});

    private static BlueprintBuildState Build(bool unknown)
    {
        var site=new BlueprintSiteSnapshot(){SessionId="synthetic-window",PlanetId=201,CapturedAtGameTick=100,BlueprintHash="synthetic-approved",
            NativeCheckPerformed=true,NativeCheckPassed=true,InventorySufficient=true,TechnologySatisfied=true,
            Objects=[new(){Index=0,ItemId=2001},new(){Index=1,ItemId=2001}]};
        var b=BlueprintBuildState.Create(site,structuredConstruction:new(){Kind="belt",JourneyId=Guid.NewGuid().ToString(),NativePlanHash="sha256:"+new string('a',64),
            Steps=[new(){OutputStepIndex=1,OutputToSlot=1},new(){InputStepIndex=0,InputToSlot=1}]});
        var c=b.ConstructionControl=StructuredConstructionControlTests.Control();c.NativeCreateActionId=Guid.NewGuid().ToString();
        b.Begin("synthetic-window",c.NativeCreateActionId,2,100);b.BeforeStructuredSubmission(101,4,"before");b.ConfirmStructuredSubmission([1,2],2,"after");
        c.RequestStop(unknown?"construction_pre_player_stationary_binding_lost":"explicit_structured_cancellation",110,c.StartedAtUtc.AddSeconds(1),unknown);
        c.DispatchDisabled=true;
        if(unknown){c.Phase="quarantined";b.Stop("outcome_unknown");b.SavedPaidStops.Add(new(){OriginalActionId=b.ActionId!,StoppedActionId=b.ActionId!,
            OriginalControlHash=c.EvidenceHash(),BuildPlanHash=b.PlanHash,PaidSubmissionHash=SavedPaidConstructionStop.SubmissionHash(b),NativeSessionId=Guid.NewGuid().ToString(),
            SourceFingerprint="sha256:"+new string('b',64),SourceGameTick=120,NativeFactoryPassGameTick=121,PhysicalIdleGameTick=122});}
        else{c.RecordDisabledNativeFactoryPass(111);Assert.True(c.TryMarkPhysicalIdle(112,true,true));c.Phase="paused_idle";b.Stop("paused");}
        return b;
    }
}
