using BepInEx.Logging;
using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

// Real shared environment/cache/site/Store boundaries. Unity contact, motion
// and Native pool transitions remain explicit substitutes, not live proof.
[Collection("Owned resume native substitutes")]
public sealed class SavedPaidSearchCacheRecoveryTests
{
    [Fact]
    public void RetainedSearchCacheTraversesPaidSiteThenIndependentStopWithoutNewSubmissionOrAnIdleClaim()
    {
        using var f=new Fixture();var original=f.Build.ConstructionControl!.EvidenceHash();var submission=SavedPaidConstructionStop.SubmissionHash(f.Build);
        var before=File.ReadAllBytes(f.StorePath);
        Assert.Equal("remote_resume_charge_native_construction_not_empty",f.Environment());
        Assert.True(f.Actions.BindSavedSearchForTest(f.Player,f.Factory,f.Build));
        // The common environment used by load, initialization, first-frame,
        // contact and charging admits only this bound paid candidate cache.
        Assert.Null(f.Environment());Assert.False(f.Player.mecha.constructionModule.TestPhysicalIdle);
        Assert.Null(SavedPaidConstructionStop.Current(f.Build,120));
        Assert.False(PaidFactoryCompletionPolicy.HasCompletedIdleProof(f.Build,120));
        Assert.Equal(before,File.ReadAllBytes(f.StorePath));
        var stop=new SavedPaidConstructionStop {OriginalActionId=f.Build.ActionId!,StoppedActionId=f.Build.ActionId!,
            OriginalControlHash=original,BuildPlanHash=f.Build.PlanHash,PaidSubmissionHash=submission,
            NativeSessionId=f.Sessions.SessionId!,SourceFingerprint="sha256:"+new string('b',64),SourceGameTick=120,
            NativeFactoryPassGameTick=120,PhysicalIdleGameTick=120,CompletedAtStop=[new(){Index=5,EntityId=2}]};
        Assert.False(stop.Matches(f.Build,120)); // no later Native factory pass
        // Substitute the separately observed disabled factory pass and actual
        // drone return, not clearing/changing the retained Native search array.
        GameMain.gameTick=122;f.Player.mecha.constructionModule.TestPhysicalIdle=true;
        stop.NativeFactoryPassGameTick=121;stop.PhysicalIdleGameTick=122;
        f.Build.SavedPaidStops.Add(stop);f.Build.LastEvidenceGameTick=122;f.Build.Generation++;
        Assert.True(f.Store.TryPut(f.Build));Assert.NotNull(SavedPaidConstructionStop.Current(f.Build,122));
        Assert.Null(f.Environment());Assert.True(f.Actions.SavedSearchForTest(f.Player,f.Build));
        Assert.Equal(8,f.Player.mecha.constructionModule.buildTargetTotalCount);
        Assert.Equal(original,f.Build.ConstructionControl.EvidenceHash());Assert.Equal(submission,SavedPaidConstructionStop.SubmissionHash(f.Build));
        Assert.Equal(10,f.Build.Objects.Count(o=>o.State==BlueprintObjectStates.PendingConstruction));
        Assert.False(PaidFactoryCompletionPolicy.HasCompletedIdleProof(f.Build,122));
        Assert.True(f.Store.TryRead(out var reopened));var saved=reopened.Single(b=>b.BuildId==f.Build.BuildId);
        Assert.Equal("outcome_unknown",saved.Phase);Assert.Equal(original,saved.ConstructionControl!.EvidenceHash());
        Assert.All(saved.Objects,o=>{Assert.Equal(20,o.InventoryBefore);Assert.Equal(9,o.InventoryAfter);});
        // A shrink caused by ordinary Native removal is safe; an additional
        // paid target that was not in this source cache cannot be borrowed.
        f.Player.mecha.constructionModule.buildTargets[0]=new();f.Player.mecha.constructionModule.buildTargetTotalCount--;
        Assert.Null(f.Environment());
        f.Player.mecha.constructionModule.buildTargets[0]=new(){objectId=11,value=1};f.Player.mecha.constructionModule.buildTargetTotalCount++;
        Assert.Equal("remote_resume_charge_native_construction_not_empty",f.Environment());
    }

    [Theory]
    [InlineData("prelaunch_reserved")][InlineData("foreign_target")][InlineData("duplicate")]
    [InlineData("foreign_builder")]
    [InlineData("counter")][InlineData("repair")][InlineData("auto_reconstruct")]
    [InlineData("dispatch_enabled")][InlineData("destroyed")][InlineData("nonfinite")]
    [InlineData("unpaid_site")][InlineData("extra_prebuild")][InlineData("one_sided")]
    [InlineData("different_session")][InlineData("different_world")]
    public void SearchCandidatesCannotHideAReservedLaunchForeignWorkOrLostRecoveryCohort(string fault)
    {
        using var f=new Fixture();Assert.True(f.Actions.BindSavedSearchForTest(f.Player,f.Factory,f.Build));
        var m=f.Player.mecha.constructionModule;
        switch(fault)
        {
            case "prelaunch_reserved":f.Factory.prebuildPool[1].builderModuleId=-1;break;
            case "foreign_builder":f.Factory.prebuildPool[1].builderModuleId=1;break;
            case "foreign_target":m.buildTargets[0].objectId=63;break;
            case "duplicate":m.buildTargets[1]=m.buildTargets[0];break;
            case "counter":m.buildTargetTotalCount++;break;
            case "repair":m.repairTargets=[new(){objectId=1}];break;
            case "auto_reconstruct":m.autoReconstructTargetTotalCount=1;break;
            case "dispatch_enabled":m.droneEnabled=true;break;
            case "destroyed":f.Factory.prebuildPool[1].isDestroyed=true;break;
            case "nonfinite":m.buildTargets[0].value=float.NaN;break;
            case "unpaid_site":f.Factory.entityPool[3]=new(){id=3,protoId=2101};f.Factory.entityCount++;f.Factory.entityCursor++;break;
            case "extra_prebuild":f.Factory.prebuildPool[12]=new(){id=12};f.Factory.prebuildCount++;f.Factory.prebuildCursor++;break;
            case "one_sided":f.Factory.TestPorts.Remove((-1,0));break;
            case "different_session":f.Sessions.SessionId=Guid.NewGuid().ToString();break;
            case "different_world":GameMain.data=new(){localLoadedPlanetFactory=f.Factory};break;
        }
        Assert.NotNull(f.Environment());
    }

    private sealed class Fixture:IDisposable
    {
        private readonly string root=Path.Combine(Path.GetTempPath(),"Spherewright","paid-search-cache",Guid.NewGuid().ToString("N"));
        internal readonly GameSessionTracker Sessions=new(){IsCurrentSessionOwned=true,OwnedSaveName="synthetic-paid-cache",SessionId=Guid.NewGuid().ToString()};
        internal readonly PlanetFactory Factory=new(){planetId=402,entityCount=2,entityCursor=3,prebuildCount=10,prebuildCursor=12};
        internal readonly Player Player=new(){planetId=402};
        internal readonly string Journey=Guid.NewGuid().ToString();
        internal readonly BlueprintBuildStore Store;internal readonly BlueprintBuildState Build;internal readonly NormalGameActionCoordinator Actions;
        internal string StorePath=>Directory.GetFiles(Path.Combine(root,"foundry"),"blueprints-*.json").Single();
        internal string? Environment()=>Actions.SavedLandingRecoveryEnvironmentForTest(Player,Journey);
        internal Fixture()
        {
            GameMain.gameTick=120;GameMain.history=new();Player.factory=Factory;GameMain.SavedFactoryTestPlayer=Player;
            GameMain.data=new(){localLoadedPlanetFactory=Factory};GameMain.localPlanet=new(){id=402,factory=Factory};
            Factory.entityPool[1]=new(){id=1,protoId=2101,rot=new(0,0,0,1)};
            Factory.entityPool[2]=new(){id=2,protoId=2001,beltId=1,rot=new(0,0,0,1)};
            for(var id=1;id<=11;id++)if(id!=6)Factory.prebuildPool[id]=new(){id=id};
            var m=Player.mecha.constructionModule;m.droneEnabled=false;m.TestPhysicalIdle=false;m.buildTargets=new TestTarget[120];
            var ids=Enumerable.Range(1,11).Where(id=>id!=6).Take(8).ToArray();
            for(var i=0;i<ids.Length;i++)m.buildTargets[i]=new(){objectId=ids[i],value=1};m.buildTargetTotalCount=8;
            Store=new(root,"0.10.35.29151",Sessions,new ManualLogSource());
            Assert.True(Store.TryPut(Make(1,false)));Build=Make(11,true);Assert.True(Store.TryPut(Build));
            for(var i=0;i<10;i++)Connect(i==5?2:-(i+1),0,i+1==5?2:-(i+2),1);
            Actions=new(Store,Sessions);
        }
        private BlueprintBuildState Make(int count,bool unknown)
        {
            var site=new BlueprintSiteSnapshot(){SessionId=Sessions.SessionId!,PlanetId=402,CapturedAtGameTick=100,BlueprintHash="synthetic-paid-cache-site",
                NativeCheckPerformed=true,NativeCheckPassed=true,InventorySufficient=true,TechnologySatisfied=true};
            var binding=new StructuredConstructionBinding(){Kind=unknown?"belt":"core",JourneyId=Journey,NativePlanHash="sha256:"+new string('a',64)};
            for(var i=0;i<count;i++)
            {site.Objects.Add(new(){Index=i,ItemId=unknown?2001:2101,Rotation=new(){W=1}});binding.Steps.Add(new(){InputStepIndex=i==0?-1:i-1,
                InputFromSlot=0,InputToSlot=1,OutputStepIndex=i<count-1?i+1:-1,OutputFromSlot=0,OutputToSlot=1});}
            var b=BlueprintBuildState.Create(site,structuredConstruction:binding);var c=b.ConstructionControl=StructuredConstructionControlTests.Control();
            c.NativeCreateActionId=Guid.NewGuid().ToString();b.Begin(Sessions.SessionId!,c.NativeCreateActionId,count,100);
            b.BeforeStructuredSubmission(101,unknown?20:1,"before");b.ConfirmStructuredSubmission(Enumerable.Range(1,count).ToArray(),unknown?9:0,"after");
            b.ConfirmCompletion(unknown?5:0,unknown?2:1,110);
            c.RequestStop(unknown?"construction_pre_player_stationary_binding_lost":"construction_complete_draining",110,c.StartedAtUtc.AddSeconds(1),unknown);
            c.DispatchDisabled=true;
            if(unknown){c.Phase="quarantined";b.LastEvidenceGameTick=112;b.Stop("outcome_unknown");}
            else{c.RecordDisabledNativeFactoryPass(111);Assert.True(c.TryMarkPhysicalIdle(112,true,true));c.Phase="completed_idle";b.LastEvidenceGameTick=112;b.Stop("completed");}
            return b;
        }
        private void Connect(int from,int fs,int to,int ts)
        {Factory.TestPorts[(from,fs)]=new(){ObjectId=from,Slot=fs,IsOutput=true,OtherObjectId=to,OtherSlot=ts};
            Factory.TestPorts[(to,ts)]=new(){ObjectId=to,Slot=ts,IsOutput=false,OtherObjectId=from,OtherSlot=fs};}
        public void Dispose()
        {var parent=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"Spherewright","paid-search-cache"))+Path.DirectorySeparatorChar;
            if(!Path.GetFullPath(root).StartsWith(parent,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException();if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
