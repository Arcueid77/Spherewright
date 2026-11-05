using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;
using Spherewright.Plugin.Game;
using BepInEx.Logging;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

[Collection("Owned resume native substitutes")]
public sealed class SavedBeltSourceTopologyTests
{
    [Fact]
    public void ColdPoolRebindingRequiresFullPaidSiteAndNeverChangesLegacyReceiptOrHash()
    {
        using var f=new Fixture();
        var before=File.ReadAllBytes(f.Path);
        Assert.False(f.Actions.SavedSourceMatchesForTest(f.Factory,f.Pending,f.Factory.TestSourceTopology));
        Assert.True(f.Actions.BindSavedSourcesForTest(f.Player,f.Factory,f.Journey,f.Saved));
        Assert.True(f.Actions.SavedSourceMatchesForTest(f.Factory,f.Pending,f.Factory.TestSourceTopology));
        Assert.True(f.Actions.SavedSourceMatchesForTest(f.Factory,f.Pending,f.Pending.StructuredConstruction!.EmptyBeltSource!.CompletionTopologyHash));
        Assert.Equal(before,File.ReadAllBytes(f.Path));
        // The current hash still binds each Native handle. It may not change
        // again inside this world/session, even if every paid object remains.
        f.Factory.TestSourceTopology="sha256:"+new string('e',64);
        Assert.False(f.Actions.SavedSourceMatchesForTest(f.Factory,f.Pending,f.Factory.TestSourceTopology));
        Assert.False(f.Actions.BindSavedSourcesForTest(f.Player,f.Factory,f.Journey,f.Saved));
    }

    [Theory]
    [InlineData("one_sided")][InlineData("extra_entity")][InlineData("extra_prebuild")]
    [InlineData("geometry")][InlineData("unpaid")][InlineData("drone_busy")]
    [InlineData("different_world")][InlineData("different_session")][InlineData("different_binding")]
    [InlineData("lost_ownership")]
    public void RebindingNeverBorrowsMissingNativePaidTopologyOrAnotherCohort(string fault)
    {
        using var f=new Fixture();
        var cohortFault=fault is "different_world" or "different_session" or "different_binding" or "lost_ownership";
        if(cohortFault)Assert.True(f.Actions.BindSavedSourcesForTest(f.Player,f.Factory,f.Journey,f.Saved));
        switch(fault)
        {
            case "one_sided":f.Factory.TestPorts.Remove((-1,1));break;
            case "extra_entity":f.Factory.entityPool[2]=new(){id=2,protoId=2001,beltId=2};f.Factory.entityCount=2;f.Factory.entityCursor=3;break;
            case "extra_prebuild":f.Factory.prebuildPool[3]=new(){id=3};f.Factory.prebuildCount=3;f.Factory.prebuildCursor=4;break;
            case "geometry":f.Factory.TestGeometryMatches=false;break;
            case "unpaid":f.Saved.RemoveAt(0);break;
            case "drone_busy":f.Player.mecha.constructionModule.TestPhysicalIdle=false;break;
            case "different_world":GameMain.data=new(){localLoadedPlanetFactory=f.Factory};break;
            case "different_session":f.Sessions.SessionId="another-session";break;
            case "different_binding":f.Pending.StructuredConstruction!.NativePlanHash="sha256:"+new string('f',64);break;
            case "lost_ownership":f.Sessions.IsCurrentSessionOwned=false;break;
        }
        Assert.False(f.Actions.BindSavedSourcesForTest(f.Player,f.Factory,f.Journey,f.Saved));
        Assert.False(f.Actions.SavedSourceMatchesForTest(f.Factory,f.Pending,f.Factory.TestSourceTopology));
    }

    private sealed class Fixture:IDisposable
    {
        private readonly string root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Spherewright","source-topology",Guid.NewGuid().ToString("N"));
        internal readonly GameSessionTracker Sessions=new(){IsCurrentSessionOwned=true,OwnedSaveName="synthetic-topology-world",SessionId="synthetic-session"};
        internal readonly PlanetFactory Factory=new(){planetId=402,entityCount=1,entityCursor=2,prebuildCount=2,prebuildCursor=3};
        internal readonly Player Player=new(){planetId=402};
        internal readonly string Journey=Guid.NewGuid().ToString();
        internal readonly List<BlueprintBuildState> Saved=[];
        internal readonly BlueprintBuildState Pending;
        internal readonly NormalGameActionCoordinator Actions;
        internal readonly string Path;
        internal Fixture()
        {
            GameMain.gameTick=120;GameMain.history=new();Player.factory=Factory;Player.mecha.constructionModule.droneEnabled=false;
            GameMain.SavedFactoryTestPlayer=Player;GameMain.data=new(){localLoadedPlanetFactory=Factory};GameMain.localPlanet=new(){id=402,factory=Factory};
            Factory.entityPool[1]=new(){id=1,protoId=2001,beltId=1,rot=new(0,0,0,1)};
            Factory.prebuildPool[1]=new(){id=1};Factory.prebuildPool[2]=new(){id=2};
            Connect(1,0,-1,1);Connect(-1,0,-2,1);
            Saved.Add(Build(1,false));Pending=Build(2,true);Saved.Add(Pending);
            var store=new BlueprintBuildStore(root,"0.10.35.29151",Sessions,new ManualLogSource());
            foreach(var b in Saved)Assert.True(store.TryPut(b));
            Assert.True(store.TryRead(out var reopened));Saved.Clear();Saved.AddRange(reopened);Pending=Saved.Single(b=>b.Objects.Count==2);
            Path=Directory.GetFiles(System.IO.Path.Combine(root,"foundry"),"blueprints-*.json").Single();
            Actions=new(store,Sessions);
        }
        private BlueprintBuildState Build(int count,bool pending)
        {
            var site=new BlueprintSiteSnapshot(){SessionId="synthetic-session",PlanetId=402,CapturedAtGameTick=100,BlueprintHash="synthetic-paid-belt",
                NativeCheckPerformed=true,NativeCheckPassed=true,InventorySufficient=true,TechnologySatisfied=true};
            var binding=new StructuredConstructionBinding(){Kind="belt",JourneyId=Journey,NativePlanHash="sha256:"+new string('a',64)};
            for(var i=0;i<count;i++)
            {site.Objects.Add(new(){Index=i,ItemId=2001,Rotation=new(){W=1}});binding.Steps.Add(new(){InputStepIndex=i==0?-1:i-1,
                InputObjectId=i==0&&pending?1:0,InputFromSlot=0,InputToSlot=1,OutputStepIndex=i<count-1?i+1:-1,OutputFromSlot=0,OutputToSlot=1});}
            if(pending){binding.SourceObjectId=1;binding.Endpoints.Add(new(){ObjectId=1,ItemId=2001,Rotation=new(){W=1}});
                binding.EmptyBeltSource=new(){ObjectId=1,BindingHash="sha256:"+new string('b',64),CargoHash="sha256:"+new string('c',64),CompletionTopologyHash="sha256:"+new string('9',64)};}
            var b=BlueprintBuildState.Create(site,structuredConstruction:binding);var c=b.ConstructionControl=StructuredConstructionControlTests.Control();
            c.NativeCreateActionId=Guid.NewGuid().ToString();b.Begin("synthetic-session",c.NativeCreateActionId,count,100);
            b.BeforeStructuredSubmission(101,count,"before");b.ConfirmStructuredSubmission(Enumerable.Range(1,count).ToArray(),0,"after");
            if(!pending)b.ConfirmCompletion(0,1,110);
            c.RequestStop(pending?"explicit_structured_cancellation":"construction_complete_draining",110,c.StartedAtUtc.AddSeconds(1),false);
            c.DispatchDisabled=true;c.RecordDisabledNativeFactoryPass(111);Assert.True(c.TryMarkPhysicalIdle(112,true,true));c.Phase=pending?"paused_idle":"completed_idle";
            b.LastEvidenceGameTick=112;b.Stop(pending?"paused":"completed");return b;
        }
        private void Connect(int from,int fs,int to,int ts)
        {Factory.TestPorts[(from,fs)]=new(){ObjectId=from,Slot=fs,IsOutput=true,OtherObjectId=to,OtherSlot=ts};
            Factory.TestPorts[(to,ts)]=new(){ObjectId=to,Slot=ts,IsOutput=false,OtherObjectId=from,OtherSlot=fs};}
        public void Dispose()
        {var parent=System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Spherewright","source-topology"))+System.IO.Path.DirectorySeparatorChar;
            if(!System.IO.Path.GetFullPath(root).StartsWith(parent,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException();if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
