using Spherewright.Contracts.Actions;

namespace Spherewright.Bridge.Core.Safety;

public sealed class ProductionReturnBuffer
{
    public int ItemId { get; set; }
    public int RawCount { get; set; }
    public int RawInc { get; set; }
    public int UnitsPerItem { get; set; } = 1;
    public int NativeStackSize { get; set; }
}

public sealed class ProductionResetFacts
{
    public int EntityId { get; set; }
    public int BuildingItemId { get; set; }
    public int RecipeId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public bool ExactNativeComponent { get; set; }
    public bool WithinBuildArea { get; set; }
    public bool EmptyPlayerHand { get; set; }
    public bool UnstackedLab { get; set; }
    public bool Working { get; set; }
    public int ProductionProgress { get; set; }
    public int ExtraProductionProgress { get; set; }
    public int EmptyUnfilteredPackageSlots { get; set; }
    public List<ProductionReturnBuffer> Buffers { get; set; } = new();
}

// A normal Native reset, not permission to clear buffers or refund work.
// Whole dedicated empty package slots avoid relying on competing partial stacks.
public static class ProductionResetPolicy
{
    public const int EvidenceVersion = 1;
    public static string? Validate(ProductionResetFacts f, out ProductionResetPlan? plan)
    {
        plan=null;
        if(f is null || !f.ExactNativeComponent || !f.WithinBuildArea || !f.EmptyPlayerHand || f.EntityId<1 || f.BuildingItemId<1)
            return "production_reset_exact_native_binding_required";
        if(f.Kind is not ("assembler" or "lab-production" or "lab-research-whole")
            || (f.Kind!="assembler" && !f.UnstackedLab) || (f.Kind=="lab-research-whole" ? f.RecipeId!=0 : f.RecipeId<=0))
            return "production_reset_supported_idle_device_required";
        if(f.Working || f.ProductionProgress!=0 || f.ExtraProductionProgress!=0)
            return "production_reset_in_progress_work_preserved";
        if(f.Buffers is null || f.Buffers.Count>32 || f.EmptyUnfilteredPackageSlots<0)
            return "production_reset_bounded_buffers_required";
        var items=new Dictionary<int,ProductionReturnItem>();var stacks=new Dictionary<int,int>();
        foreach(var b in f.Buffers)
        {
            var units=f.Kind=="lab-research-whole" ? 3600 : 1;
            if(b is null || b.ItemId<=0 || b.ItemId==1099 || b.RawCount<0 || b.RawInc<0 || (long)b.RawInc>(long)b.RawCount*10 || b.UnitsPerItem!=units
                || b.RawCount%units!=0 || b.RawInc%units!=0 || b.NativeStackSize<1 || b.NativeStackSize>10000)
                return "production_reset_fractional_or_unproved_material_preserved";
            if(b.RawCount==0)continue;
            if(!items.TryGetValue(b.ItemId,out var row))items[b.ItemId]=row=new(){ItemId=b.ItemId};
            var count=(long)row.Count+b.RawCount/units;var inc=(long)row.Inc+b.RawInc/units;
            if(count>10000 || inc>count*10 || (stacks.TryGetValue(b.ItemId,out var stack) && stack!=b.NativeStackSize))
                return "production_reset_bounded_material_required";
            row.Count=(int)count;row.Inc=(int)inc;stacks[b.ItemId]=b.NativeStackSize;
        }
        if(items.Count==0 || items.Count>16 || items.Values.Sum(x=>(long)x.Count)>10000)return "production_reset_nonempty_finite_return_required";
        var slots=items.Values.Sum(x=>(x.Count+stacks[x.ItemId]-1)/stacks[x.ItemId]);
        if(slots>f.EmptyUnfilteredPackageSlots)return "production_reset_no_drop_package_capacity_required";
        plan=new(){EvidenceVersion=EvidenceVersion,EntityId=f.EntityId,BuildingItemId=f.BuildingItemId,OriginalRecipeId=f.RecipeId,
            Kind=f.Kind,NativeOperation=f.Kind=="assembler" ? "FactorySystem.TakeBackItems_Assembler" : "FactorySystem.TakeBackItems_Lab",
            DedicatedEmptyPackageSlots=slots,Returns=items.Values.OrderBy(x=>x.ItemId).ToList()};
        return null;
    }

    public static bool HasMatchingEcho(int entityId,PreparedNormalAction? prepared)=>prepared is {Prepared:true,ActionKind:NormalActionKinds.Transfer,ProductionReset:{} p}
        && prepared.DestinationObjectId==entityId && prepared.ItemBudget is not null && p.Returns is not null
        && p.EvidenceVersion==EvidenceVersion && p.EntityId==entityId && p.BuildingItemId>0 && p.DedicatedEmptyPackageSlots>0
        && p.Returns.Count is >0 and <=16 && p.Returns.All(x=>x is not null && x.ItemId>0 && x.ItemId!=1099 && x.Count>0 && x.Count<=10000 && x.Inc>=0 && (long)x.Inc<=(long)x.Count*10)
        && p.Returns.Select(x=>x.ItemId).Distinct().Count()==p.Returns.Count && p.Returns.Sum(x=>(long)x.Count)<=10000
        && prepared.ItemBudget.Count==p.Returns.Count && p.Returns.All(x=>prepared.ItemBudget.Count(b=>b is not null && b.ItemId==x.ItemId
            && b.Count==x.Count && b.Direction==TransferDirections.ProductionResetToPlayer)==1)
        && (p.Kind=="assembler" && p.OriginalRecipeId>0 && p.NativeOperation=="FactorySystem.TakeBackItems_Assembler"
            || (p.Kind is "lab-production" or "lab-research-whole") && p.NativeOperation=="FactorySystem.TakeBackItems_Lab"
                && (p.Kind=="lab-research-whole" ? p.OriginalRecipeId==0 : p.OriginalRecipeId>0));
}
