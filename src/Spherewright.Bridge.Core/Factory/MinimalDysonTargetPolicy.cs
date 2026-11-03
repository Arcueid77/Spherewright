using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Factory;

public static class MinimalDysonTargetPolicy
{
    public const string Mode = "dyson-minimal-receiving-node";
    public static bool ConfirmsPreparedEcho(PrepareConfigureBuildingRequest r,PreparedNormalAction? p)
        =>r.Mode==Mode && r.RecipeId==0 && p is {Prepared:true,CommitAllowedNow:true,PlannedMinimalDysonTarget:{ } t}
            && !string.IsNullOrEmpty(p.PlanToken) && p.ActionKind==NormalActionKinds.ConfigureBuilding
            && t.EvidenceVersion==1 && t.LayerId==1 && t.NodePrototypeId==0 && t.StarId>0
            && t.Radius>0 && !float.IsInfinity(t.Radius) && !float.IsNaN(t.Radius)
            && t.ExactExistingLayerBinding && t.CompleteEmptyStructureCoverage && t.NativePrototypeAlreadyLoaded
            && t.NativeLatitudeAllowsEquator && !string.IsNullOrEmpty(r.ExpectedFactoryStateHash)
            && t.ConfigurationStateHash==r.ExpectedFactoryStateHash;
    public static string Hash(FactoryEntitySnapshot entity, MinimalDysonTargetSnapshot s) =>
        CanonicalStateHash.Combine("minimal-native-dyson-target-v1",entity.PlanetId,entity.ObjectId,entity.ItemId,
            entity.Silo?.SiloComponentId,entity.Position.X,entity.Position.Y,entity.Position.Z,
            entity.Rotation.X,entity.Rotation.Y,entity.Rotation.Z,entity.Rotation.W,
            s.StarId,s.LayerId,s.Radius,s.NodePrototypeId,s.ExactExistingLayerBinding,
            s.CompleteEmptyStructureCoverage,s.NativePrototypeAlreadyLoaded,s.NativeLatitudeAllowsEquator);

    public static string? Validate(PrepareConfigureBuildingRequest r, FactoryEntitySnapshot e,
        MinimalDysonTargetSnapshot? target)
    {
        if (r.Mode!=Mode || r.RecipeId!=0 || r.TechId!=0 || r.FilterItemId!=0 || r.EjectorOrbitId!=-1
            || !string.IsNullOrEmpty(r.StorageOperation) || r.StorageBannedGridCount!=-1
            || r.StationStorageIndex!=-1 || r.StationBeltSlotIndex!=-1 || r.StationBeltStorageIndex!=-1
            || r.StationItemId!=0 || r.StationMaximumCount!=0 || r.StationMaximumChargePowerWatts!=0
            || r.StationLocalLogic!=LogisticsStorageLogics.None || r.StationRemoteLogic!=LogisticsStorageLogics.None
            || !string.IsNullOrEmpty(r.ExpectedStationConfigurationStateHash)) return "minimal_target_exclusive_parameters_required";
        if(e.ObjectKind!=FactoryObjectKinds.Entity || e.ItemId!=2312 || e.ComponentKind!="silo"
            || e.ObjectId!=r.EntityId || e.PlanetId!=r.PlanetId || e.Silo is not { NativeBindingVerified:true,
                SphereAvailable:true,NodeCoverageComplete:true,RocketCoverageComplete:true,InFlightRocketCount:0,
                RocketCount:0,RocketIncPoints:0,StarSiloCoverageComplete:true,StarSiloCount:1 } silo
            || silo.Nodes is null || silo.Nodes.Count!=0 || silo.Rockets is null || silo.Rockets.Count!=0)
            return "minimal_target_exact_empty_silo_and_star_required";
        if(target is null || target.EvidenceVersion!=1 || target.StarId!=silo.StarId || target.StarId<=0
            || target.LayerId!=1 || target.NodePrototypeId!=0 || !target.ExactExistingLayerBinding
            || !target.CompleteEmptyStructureCoverage || !target.NativePrototypeAlreadyLoaded || !target.NativeLatitudeAllowsEquator
            || float.IsNaN(target.Radius) || float.IsInfinity(target.Radius) || target.Radius<=0
            || target.ConfigurationStateHash!=Hash(e,target) || r.ExpectedFactoryStateHash!=target.ConfigurationStateHash)
            return "minimal_target_existing_native_layer_or_hash_unproved";
        return null;
    }
}
