using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Logistics;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Transport;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private sealed class StationCargoBinding
    {
        public GameData World = null!;
        public Player Player = null!;
        public PlanetFactory Factory = null!;
        public StorageComponent Package = null!;
        public EntityData[] Entities = null!;
        public StationComponent Station = null!;
        public StationStore[] Storage = null!;
        public StationCargoWithdrawalPlan Plan = null!;
    }

    private sealed partial class NormalActionPlanPayload
    {
        public StationCargoBinding? StationCargo { get; set; }
    }

    private static int StationCargoPackageInc(StorageComponent package,int itemId)
        => package.grids.Take(package.size).Where(g=>g.itemId==itemId).Sum(g=>g.inc);

    private static string? CaptureStationCargo(Player p,PlanetFactory f,FactoryEntitySnapshot entity,
        int itemId,int count,out StationCargoBinding? binding)
    {
        binding=null;
        if(!p.isAlive || p.package?.grids is null || p.mecha is null
            || p.planetId!=f.planetId || !ReferenceEquals(p.factory,f) || ReferenceEquals(p.package,p.mecha.reactorStorage)
            || p.package.size<1 || p.package.size>p.package.grids.Length
            || !TryGetFleetStation(f,entity.ObjectId,out var station,out _,out _) || station is null)
            return "station_cargo_native_component_unavailable";
        var snapshot=entity.LogisticsStation;
        if(station!.storage is null || snapshot is null || snapshot.StorageSlots.Count!=station.storage.Length)
            return "station_cargo_complete_native_storage_unavailable";
        var reason=StationCargoWithdrawalPolicy.Blocker(snapshot,entity.ObjectId,itemId,count,true,
            p.inhandItemId==0 && p.inhandItemCount==0 && p.inhandItemInc==0,
            Vector3.Distance(p.position,f.entityPool[entity.ObjectId].pos)<=p.mecha.buildArea,
            CanPlayerPackageAcceptExactly(p.package,itemId,count),StationCargoPackageInc(p.package,itemId));
        if(reason is not null)return reason;
        var index=snapshot.StorageSlots.Single(s=>s.ItemId==itemId).Index;
        if(index>=station.storage.Length || station.storage[index].itemId!=itemId
            || station.storage[index].count!=snapshot.StorageSlots.Single(s=>s.Index==index).Count)
            return "station_cargo_native_slot_identity_changed";
        binding=new(){World=GameMain.data,Player=p,Factory=f,Package=p.package,Entities=f.entityPool,
            Station=station,Storage=station.storage,Plan=new(){EntityId=entity.ObjectId,StorageIndex=index,
            ItemId=itemId,Count=count,NativeOperation=StationCargoWithdrawalPolicy.NativeOperation}};
        return null;
    }

    private GameCallResult<PreparedNormalAction> PrepareStationCargoWithdrawalOnMainThread(string? sessionId,PrepareTransferRequest r)
    {
        var common=ValidatePrepareCommon(sessionId,r.PlanetId,r.StateHashVersion);
        if(common.Error is not null)return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        if(r.Direction!=TransferDirections.StationStorageToPlayer || r.ItemId<1 || r.Count<1 || r.Count>100)
            return InvalidPlan("Station cargo withdrawal requires station-storage-to-player and exact physical count1..100.");
        var player=_reader.GetPlayerStateOnMainThread(sessionId,new LocalPlanetRequest{PlanetId=r.PlanetId});
        var entity=_reader.InspectFactoryEntityOnMainThread(sessionId,new InspectFactoryEntityRequest{PlanetId=r.PlanetId,ObjectId=r.StorageEntityId});
        if(!player.Success || player.Value is null)return GameCallResult<PreparedNormalAction>.Failed(player.Error!);
        if(!entity.Success || entity.Value is null)return GameCallResult<PreparedNormalAction>.Failed(entity.Error!);
        if(player.Value.StateHash!=r.ExpectedPlayerStateHash || entity.Value.StateHash!=r.ExpectedStorageStateHash)
            return StalePlan("Player or exact physical station stock changed after inspection.");
        if(GameMain.localPlanet?.factory is not { } f || GameMain.mainPlayer is not { } p)
            return NotReadyPlan("The exact local owned station/player is unavailable.");
        var reason=CaptureStationCargo(p,f,entity.Value,r.ItemId,r.Count,out var binding);
        if(reason is not null)return InvalidPlan(reason);
        var playerHash=CanonicalStateHash.PlayerAction(player.Value);
        var hash=CanonicalStateHash.Combine("station-cargo-withdrawal-v1",sessionId,r.PlanetId,playerHash,
            entity.Value.StateHash,r.StorageEntityId,r.ItemId,r.Count,binding!.Plan.StorageIndex);
        var payload=NormalActionPlanPayload.Transfer(sessionId!,r.PlanetId,hash,playerHash,entity.Value.StateHash,
            r.StorageEntityId,r.Direction,r.ItemId,r.Count);
        payload.StationCargo=binding;
        var prepared=AddPreparedPlan(payload,common.Session!,1,
            "One Native TakeItem/AddItemStacked withdrawal conserves exact physical station/package stock; orders are not stock.");
        if(prepared.Success && prepared.Value is { } plan)
        {
            plan.StationCargoWithdrawal=binding.Plan;plan.SourceObjectId=r.StorageEntityId;
            plan.ItemBudget.Add(new(){ItemId=r.ItemId,Count=r.Count,Direction=r.Direction});
        }
        return prepared;
    }

    private BridgeError? RevalidateStationCargoWithdrawalOnMainThread(NormalActionPlanPayload plan)
    {
        var b=plan.StationCargo;var f=GameMain.localPlanet?.factory;var p=GameMain.mainPlayer;
        if(b is null || f is null || p is null || !ReferenceEquals(GameMain.data,b.World)
            || !ReferenceEquals(p,b.Player) || !ReferenceEquals(f,b.Factory) || !ReferenceEquals(p.package,b.Package)
            || !ReferenceEquals(f.entityPool,b.Entities) || !TryGetFleetStation(f,plan.TransferStorageEntityId,out var station,out _,out _)
            || !ReferenceEquals(station,b.Station) || !ReferenceEquals(station!.storage,b.Storage))
            return Stale("station_cargo_original_native_binding_lost");
        var player=_reader.GetPlayerStateOnMainThread(plan.SessionId,new LocalPlanetRequest{PlanetId=plan.PlanetId});
        var entity=_reader.InspectFactoryEntityOnMainThread(plan.SessionId,new InspectFactoryEntityRequest{PlanetId=plan.PlanetId,ObjectId=plan.TransferStorageEntityId});
        if(!player.Success || player.Value is null)return player.Error;
        if(!entity.Success || entity.Value is null)return entity.Error;
        if(CanonicalStateHash.PlayerAction(player.Value)!=plan.PlayerStateHash || entity.Value.StateHash!=plan.TransferStorageStateHash)
            return Stale("station_cargo_original_stock_or_package_changed");
        var reason=CaptureStationCargo(p,f,entity.Value,plan.TransferItemId,plan.Count,out var fresh);
        return reason is null && fresh!.Plan.StorageIndex==b.Plan.StorageIndex ? null : Stale(reason??"station_cargo_original_slot_changed");
    }

    private void ExecuteStationCargoWithdrawalOnMainThread(ActionRecord action)
    {
        var plan=action.Plan;var b=plan.StationCargo!;var index=b.Plan.StorageIndex;var itemId=plan.TransferItemId;
        var before=_reader.InspectFactoryEntityOnMainThread(plan.SessionId,new InspectFactoryEntityRequest{PlanetId=plan.PlanetId,ObjectId=plan.TransferStorageEntityId});
        if(!before.Success || before.Value?.LogisticsStation is not { } beforeStation)
            throw new InvalidOperationException("station_cargo_same_tick_before_unavailable");
        var slots=b.Storage.ToArray();var packageBefore=b.Package.GetItemCount(itemId);var packageIncBefore=StationCargoPackageInc(b.Package,itemId);
        var otherPackage=CapturePlayerPackageStateExcludingItem(b.Package,itemId);
        var takenItem=itemId;var takenCount=plan.Count;
        // Native TakeItem uses the same split_inc as the station UI's exact
        // split pickup. Native AddItemStacked performs the ordinary package
        // insertion. No station count, grid, energy or simulation field is set.
        b.Station.TakeItem(ref takenItem,ref takenCount,out var takenInc);
        if(takenItem!=itemId || takenCount!=plan.Count || takenInc!=0)
            throw new InvalidOperationException("station_cargo_native_exact_take_unproved");
        var added=b.Package.AddItemStacked(itemId,takenCount,takenInc,out var remainingInc);
        if(added!=takenCount || remainingInc!=0)throw new InvalidOperationException("station_cargo_native_package_add_unproved");
        b.Player.NotifyPackageAddItem(itemId,added,0);
        var after=_reader.InspectFactoryEntityOnMainThread(plan.SessionId,new InspectFactoryEntityRequest{PlanetId=plan.PlanetId,ObjectId=plan.TransferStorageEntityId});
        var slotsPreserved=b.Storage.Length==slots.Length;
        for(var i=0;slotsPreserved && i<slots.Length;i++)
        {
            var unaffected=b.Storage[i];
            // Detached Native value copy only: every other StationStore field
            // must remain identical, including orders, limits and logic.
            if(i==index){unaffected.count=slots[i].count;unaffected.inc=slots[i].inc;}
            slotsPreserved=unaffected.Equals(slots[i]);
        }
        var packageAfter=b.Package.GetItemCount(itemId);var packageIncAfter=StationCargoPackageInc(b.Package,itemId);
        var otherPackagePreserved=otherPackage==CapturePlayerPackageStateExcludingItem(b.Package,itemId);
        var otherStationPreserved=false;
        if(after.Success && after.Value?.LogisticsStation is { } afterStation)
        {
            var normalized=PluginJson.Deserialize<LogisticsStationSnapshot>(PluginJson.Serialize(afterStation))!;
            normalized.StorageSlots[index]=beforeStation.StorageSlots.Single(s=>s.Index==index);
            otherStationPreserved=CanonicalStateHash.LogisticsStation(normalized)==CanonicalStateHash.LogisticsStation(beforeStation)
                && PluginJson.Serialize(afterStation.ActiveVessels)==PluginJson.Serialize(beforeStation.ActiveVessels)
                && PluginJson.Serialize(after.Value.Connections)==PluginJson.Serialize(before.Value!.Connections);
        }
        var conserved=slotsPreserved && otherStationPreserved && otherPackagePreserved
            && b.Storage[index].count==slots[index].count-plan.Count && b.Storage[index].inc==0
            && packageAfter==packageBefore+plan.Count && packageIncBefore==0 && packageIncAfter==0
            && b.Player.inhandItemId==0 && b.Player.inhandItemCount==0 && b.Player.inhandItemInc==0;
        action.StationCargoWithdrawalReadback=new(){EntityId=plan.TransferStorageEntityId,StorageIndex=index,ItemId=itemId,
            RequestedCount=plan.Count,StationCountBefore=slots[index].count,StationCountAfter=b.Storage[index].count,
            StationIncBefore=slots[index].inc,StationIncAfter=b.Storage[index].inc,PackageCountBefore=packageBefore,
            PackageCountAfter=packageAfter,PackageIncBefore=packageIncBefore,PackageIncAfter=packageIncAfter,
            ExactNativeBindingVerified=true,StationOtherStatePreserved=slotsPreserved && otherStationPreserved,
            PackageOtherStockPreserved=otherPackagePreserved,ConservationVerified=conserved,CapturedAtGameTick=GameMain.gameTick};
        if(!conserved)throw new InvalidOperationException("station_cargo_complete_bilateral_conservation_unproved");
        action.TargetObjectId=plan.TransferStorageEntityId;action.TargetItemId=itemId;
        action.BeforeTargetAmount=slots[index].count;action.AfterTargetAmount=b.Storage[index].count;
        action.AfterInventory=CaptureInventory(b.Player);action.CompletedAtGameTick=GameMain.gameTick;
        action.AfterStateHash=CanonicalStateHash.Combine("station-cargo-withdrawal-completed-v1",after.Value!.StateHash,packageAfter,packageIncAfter);
        action.Message="Native station withdrawal conserved exact physical stock and inc with all unrelated station/package state retained.";
        action.State=NormalActionStates.Completed;action.Terminal=true;action.Succeeded=true;
    }
}
