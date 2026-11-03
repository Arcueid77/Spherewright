using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Transport;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private sealed class ProductionResetBinding
    {
        internal PlanetFactory Factory=null!;
        internal StorageComponent Package=null!;
        internal EntityData[] Entities=null!;
        internal AssemblerComponent[]? Assemblers;
        internal LabComponent[]? Labs;
        internal int ComponentId;
        internal ProductionResetPlan Plan=null!;
    }
    private sealed partial class NormalActionPlanPayload
    {
        public ProductionResetBinding? ProductionReset {get;set;}
    }

    private static string? CaptureProductionReset(Player p,PlanetFactory f,FactoryEntitySnapshot snapshot,out ProductionResetBinding? binding)
    {
        binding=null;var id=snapshot.ObjectId;var fs=f.factorySystem;
        if(p.mecha is null || p.package?.grids is null || p.package.size<1 || p.package.size>4096 || p.package.size>p.package.grids.Length
            || p.package.bans<0 || p.package.bans>p.package.size || !ReferenceEquals(p.factory,f)
            || ReferenceEquals(p.package,p.mecha.reactorStorage) || f.entityPool is null || id<1 || id>=f.entityCursor || id>=f.entityPool.Length || fs is null)
            return "production_reset_native_pools_unavailable";
        ref var e=ref f.entityPool[id];if(e.id!=id || e.protoId!=snapshot.ItemId)return "production_reset_native_entity_changed";
        var b=new ProductionResetBinding{Factory=f,Package=p.package,Entities=f.entityPool};
        var facts=new ProductionResetFacts{EntityId=id,BuildingItemId=e.protoId,RecipeId=snapshot.RecipeId,
            WithinBuildArea=Vector3.Distance(p.position,e.pos)<=p.mecha.buildArea,
            EmptyPlayerHand=p.inhandItemId==0 && p.inhandItemCount==0 && p.inhandItemInc==0};
        bool CopyBuffers(int[]? ids,int[]? counts,int[]? incs,int units,bool output=false)
        {
            if(ids is null || counts is null || ids.Length!=counts.Length || ids.Length>16
                || !output && (incs is null || incs.Length!=ids.Length))return false;
            for(var i=0;i<ids.Length;i++)
            {
                var item=LDB.items.Select(ids[i]);if(item is null)return false;
                facts.Buffers.Add(new(){ItemId=ids[i],RawCount=counts[i],RawInc=output?0:incs![i],UnitsPerItem=units,NativeStackSize=item.StackSize});
            }
            return true;
        }
        if(e.assemblerId>0 && e.labId==0 && fs.assemblerPool is not null && e.assemblerId<fs.assemblerCursor && e.assemblerId<fs.assemblerPool.Length)
        {
            var a=fs.assemblerPool[e.assemblerId];b.Assemblers=fs.assemblerPool;b.ComponentId=e.assemblerId;
            facts.Kind="assembler";facts.ExactNativeComponent=a.id==e.assemblerId && a.entityId==id && a.pcId==e.powerConId
                && LDB.items.Select(e.protoId)?.prefabDesc?.isAssembler==true && a.recipeId==snapshot.RecipeId;
            facts.Working=a.replicating;facts.ProductionProgress=a.time;facts.ExtraProductionProgress=a.extraTime;
            if(a.recipeExecuteData is null || !CopyBuffers(a.recipeExecuteData.requires,a.served,a.incServed,1)
                || !CopyBuffers(a.recipeExecuteData.products,a.produced,null,1,true))return "production_reset_complete_native_buffers_required";
        }
        else if(e.labId>0 && e.assemblerId==0 && fs.labPool is not null && fs.labCursor>0 && fs.labCursor<=65536
            && fs.labCursor<=fs.labPool.Length && e.labId<fs.labCursor)
        {
            var lab=fs.labPool[e.labId];b.Labs=fs.labPool;b.ComponentId=e.labId;
            facts.Kind=lab.researchMode ? "lab-research-whole" : "lab-production";
            facts.ExactNativeComponent=lab.id==e.labId && lab.entityId==id && lab.pcId==e.powerConId
                && LDB.items.Select(e.protoId)?.prefabDesc?.isLab==true && lab.recipeId==snapshot.RecipeId;
            facts.Working=lab.replicating;facts.ProductionProgress=lab.time!=0?lab.time:lab.hashBytes;facts.ExtraProductionProgress=lab.extraTime;
            facts.UnstackedLab=lab.nextLabId==0;
            for(var i=1;i<fs.labCursor;i++)if(fs.labPool[i].id!=0 && fs.labPool[i].nextLabId==e.labId)facts.UnstackedLab=false;
            if(lab.researchMode)
            {if(!CopyBuffers(LabComponent.matrixIds,lab.matrixServed,lab.matrixIncServed,3600))return "production_reset_complete_native_matrices_required";}
            else if(lab.recipeExecuteData is null || !CopyBuffers(lab.recipeExecuteData.requires,lab.served,lab.incServed,1)
                || !CopyBuffers(lab.recipeExecuteData.products,lab.produced,null,1,true))return "production_reset_complete_native_buffers_required";
        }
        else return "production_reset_supported_component_required";
        for(var i=0;i<p.package.size-p.package.bans;i++)
        {
            var slot=p.package.grids[i];
            if(slot.itemId<0 || slot.count<0 || slot.inc<0 || (long)slot.inc>(long)slot.count*10 || slot.itemId==0 && slot.count!=0)
                return "production_reset_native_package_cells_unproved";
            if(slot.itemId==0 && slot.count==0 && slot.inc==0 && slot.filter==0)facts.EmptyUnfilteredPackageSlots++;
        }
        var reason=ProductionResetPolicy.Validate(facts,out var planned);if(reason is not null)return reason;
        b.Plan=planned!;binding=b;return null;
    }

    private GameCallResult<PreparedNormalAction> PrepareProductionResetOnMainThread(string? sessionId,PrepareTransferRequest r)
    {
        var common=ValidatePrepareCommon(sessionId,r.PlanetId,r.StateHashVersion);if(common.Error is not null)return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        if(r.Direction!=TransferDirections.ProductionResetToPlayer || r.ItemId!=0 || r.Count!=0)
            return InvalidPlan("production-reset-to-player requires itemId=0/count=0 and discloses every exact Native return before confirmation.");
        var player=_reader.GetPlayerStateOnMainThread(sessionId,new LocalPlanetRequest{PlanetId=r.PlanetId});
        var entity=_reader.InspectFactoryEntityOnMainThread(sessionId,new InspectFactoryEntityRequest{PlanetId=r.PlanetId,ObjectId=r.StorageEntityId});
        if(!player.Success || player.Value is null)return GameCallResult<PreparedNormalAction>.Failed(player.Error!);
        if(!entity.Success || entity.Value is null)return GameCallResult<PreparedNormalAction>.Failed(entity.Error!);
        if(player.Value.StateHash!=r.ExpectedPlayerStateHash || entity.Value.StateHash!=r.ExpectedStorageStateHash)return StalePlan("production_reset_player_or_device_changed");
        var f=GameMain.localPlanet?.factory;var p=GameMain.mainPlayer;if(f is null || p is null)return NotReadyPlan("production_reset_world_unavailable");
        var blocker=CaptureProductionReset(p,f,entity.Value,out var binding);if(blocker is not null)return StalePlan(blocker);
        var journey=_flightCheckpoints.VoyagerJourney;
        if(journey is not null && !IsVoyagerJourneyClosed(journey) && binding!.Plan.Returns.Any(x=>x.ItemId>=6001 && x.ItemId<=6006))
            return StalePlan("production_reset_remote_mecha_research_transition_unproved");
        var ph=CanonicalStateHash.PlayerAction(player.Value);
        var hash=CanonicalStateHash.Combine("production-reset-native-ui-v1",_sessions.SessionId,r.PlanetId,ph,entity.Value.StateHash,PluginJson.Serialize(binding!.Plan));
        var payload=NormalActionPlanPayload.Transfer(_sessions.SessionId!,r.PlanetId,hash,ph,entity.Value.StateHash,r.StorageEntityId,r.Direction,0,0);payload.ProductionReset=binding;
        var result=AddPreparedPlan(payload,common.Session!,1,"Normal Native TakeBackItems returns every disclosed whole item/inc to proven package capacity and resets the exact idle device. No work, items, points or ticks are created.");
        if(result.Success && result.Value is {} prepared)
        {
            prepared.DestinationObjectId=r.StorageEntityId;prepared.ProductionReset=binding.Plan;
            foreach(var row in binding.Plan.Returns)prepared.ItemBudget.Add(new(){ItemId=row.ItemId,Count=row.Count,Direction=r.Direction});
        }
        return result;
    }

    private BridgeError? RevalidateProductionResetOnMainThread(NormalActionPlanPayload plan)
    {
        var b=plan.ProductionReset;var f=GameMain.localPlanet?.factory;var p=GameMain.mainPlayer;
        if(b is null || f is null || p is null || !ReferenceEquals(f,b.Factory) || !ReferenceEquals(p.package,b.Package)
            || !ReferenceEquals(f.entityPool,b.Entities) || b.Assemblers is not null && !ReferenceEquals(b.Assemblers,f.factorySystem?.assemblerPool)
            || b.Labs is not null && !ReferenceEquals(b.Labs,f.factorySystem?.labPool))return Stale("production_reset_original_native_binding_lost");
        var player=_reader.GetPlayerStateOnMainThread(plan.SessionId,new LocalPlanetRequest{PlanetId=plan.PlanetId});
        var entity=_reader.InspectFactoryEntityOnMainThread(plan.SessionId,new InspectFactoryEntityRequest{PlanetId=plan.PlanetId,ObjectId=plan.TransferStorageEntityId});
        if(!player.Success || player.Value is null)return player.Error;
        if(!entity.Success || entity.Value is null)return entity.Error;
        if(CanonicalStateHash.PlayerAction(player.Value)!=plan.PlayerStateHash || entity.Value.StateHash!=plan.TransferStorageStateHash)return Stale("production_reset_original_inventory_or_buffers_changed");
        var reason=CaptureProductionReset(p,f,entity.Value,out var fresh);
        return reason is null && fresh!.ComponentId==b.ComponentId && PluginJson.Serialize(fresh.Plan)==PluginJson.Serialize(b.Plan)
            ? null : Stale(reason??"production_reset_original_return_plan_changed");
    }

    private static Dictionary<int,(int Count,int Inc)> ProductionResetPackage(StorageComponent p)=>p.grids.Take(p.size)
        .Where(x=>x.itemId>0 && x.count>0).GroupBy(x=>x.itemId).ToDictionary(g=>g.Key,g=>(g.Sum(x=>x.count),g.Sum(x=>x.inc)));

    private void ExecuteProductionResetOnMainThread(ActionRecord action)
    {
        var plan=action.Plan;var b=plan.ProductionReset!;var p=GameMain.mainPlayer;
        if(RevalidateProductionResetOnMainThread(plan) is {} changed)
            throw new InvalidOperationException("production_reset_pre_mutation_binding_changed:"+changed.Code);
        var before=ProductionResetPackage(b.Package);var expected=before.ToDictionary(x=>x.Key,x=>x.Value);
        foreach(var row in b.Plan.Returns){expected.TryGetValue(row.ItemId,out var old);expected[row.ItemId]=(old.Count+row.Count,old.Inc+row.Inc);}
        var prior=_reader.GetProgressionStateOnMainThread(plan.SessionId,new LocalPlanetRequest{PlanetId=plan.PlanetId});
        var original=_reader.InspectFactoryEntityOnMainThread(plan.SessionId,new InspectFactoryEntityRequest{PlanetId=plan.PlanetId,ObjectId=plan.TransferStorageEntityId});
        if(!prior.Success || prior.Value is null || !original.Success || original.Value is null)throw new InvalidOperationException("production_reset_same_tick_before_unavailable");
        // Exact normal UI business methods. Never assign served/produced,
        // matrix points, package grids, technology, simulation time or energy.
        if(b.Assemblers is not null)b.Factory.factorySystem.TakeBackItems_Assembler(p,b.ComponentId);
        else b.Factory.factorySystem.TakeBackItems_Lab(p,b.ComponentId);
        var after=ProductionResetPackage(b.Package);
        var entity=_reader.InspectFactoryEntityOnMainThread(plan.SessionId,new InspectFactoryEntityRequest{PlanetId=plan.PlanetId,ObjectId=plan.TransferStorageEntityId});
        var progression=_reader.GetProgressionStateOnMainThread(plan.SessionId,new LocalPlanetRequest{PlanetId=plan.PlanetId});
        if(expected.Count!=after.Count || expected.Any(x=>!after.TryGetValue(x.Key,out var y) || x.Value!=y)
            || !entity.Success || entity.Value is null || entity.Value.RecipeId!=0 || entity.Value.Progress!=0 || entity.Value.Buffers.Any(x=>x.Count!=0)
            || entity.Value.ObjectId!=original.Value.ObjectId || entity.Value.ItemId!=original.Value.ItemId
            || PluginJson.Serialize(entity.Value.Connections)!=PluginJson.Serialize(original.Value.Connections)
            || !progression.Success || progression.Value is null || progression.Value.StateHash!=prior.Value.StateHash)
            throw new InvalidOperationException("production_reset_complete_native_conservation_unproved");
        action.TargetObjectId=plan.TransferStorageEntityId;action.BeforeTargetAmount=b.Plan.Returns.Sum(x=>x.Count);action.AfterTargetAmount=0;
        action.AfterInventory=CaptureInventory(p);action.CompletedAtGameTick=GameMain.gameTick;
        action.AfterStateHash=CanonicalStateHash.Combine("production-reset-completed-v1",entity.Value.StateHash,PluginJson.Serialize(action.AfterInventory),progression.Value.StateHash);
        action.Message="Normal Native TakeBackItems returned every disclosed material/inc and reset one exact idle device; full package totals, connections and technology were conserved at this tick.";
        action.State=NormalActionStates.Completed;action.Terminal=true;action.Succeeded=true;
    }
}
