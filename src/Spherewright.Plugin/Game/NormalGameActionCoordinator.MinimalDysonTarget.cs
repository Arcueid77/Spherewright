using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameCallResult<PreparedNormalAction> PrepareMinimalDysonTargetOnMainThread(string? sessionId,
        PrepareConfigureBuildingRequest request, SessionState session)
    {
        var inspected=_reader.InspectFactoryEntityOnMainThread(sessionId,
            new InspectFactoryEntityRequest { PlanetId=request.PlanetId,ObjectId=request.EntityId });
        if(!inspected.Success || inspected.Value is null)return GameCallResult<PreparedNormalAction>.Failed(inspected.Error!);
        var target=inspected.Value.Silo?.MinimalReceivingTarget;
        var reason=MinimalDysonTargetPolicy.Validate(request,inspected.Value,target);
        if(reason is not null || target is null)return InvalidPlan(reason??"minimal_target_unavailable");
        var layer=FindExistingMinimalTargetLayer(target);
        if(layer is null || !EjectorWithinNativeBuildReach(request.EntityId))return StalePlan("Existing target layer or local silo reach changed.");
        var payload=NormalActionPlanPayload.ForMinimalDysonTarget(sessionId!,request.PlanetId,request.EntityId,target,layer);
        var result=AddPreparedPlan(payload,session,1,"Creates exactly one planned equatorial node on an existing empty Native layer through NewDysonNode. No structure points, items or rockets are injected; no simulation is advanced.");
        if(result.Success && result.Value is not null)result.Value.PlannedMinimalDysonTarget=target.Copy();
        return result;
    }

    private static DysonSphereLayer? FindExistingMinimalTargetLayer(MinimalDysonTargetSnapshot target)
    {
        var data=GameMain.data;var star=GameMain.localPlanet?.star;
        if(data?.dysonSpheres is null || star is null || star.id!=target.StarId || star.index<0
            || star.index>=data.dysonSpheres.Length || data.dysonSpheres[star.index] is not { } sphere
            || !ReferenceEquals(sphere.gameData,data) || !ReferenceEquals(sphere.starData,star)
            || sphere.layersIdBased is not { Length:11 } layers || layers[1] is not { } layer
            || layer.id!=1 || !ReferenceEquals(layer.dysonSphere,sphere))return null;
        return layer;
    }

    private Spherewright.Contracts.Errors.BridgeError? RevalidateMinimalDysonTargetOnMainThread(NormalActionPlanPayload plan)
    {
        var inspected=_reader.InspectFactoryEntityOnMainThread(plan.SessionId,
            new InspectFactoryEntityRequest {PlanetId=plan.PlanetId,ObjectId=plan.EntityId});
        if(!inspected.Success || inspected.Value is null)return inspected.Error??Stale("Silo disappeared.");
        var target=inspected.Value.Silo?.MinimalReceivingTarget;
        var request=new PrepareConfigureBuildingRequest {Mode=BuildingConfigurationModes.MinimalDysonReceivingNode,
            PlanetId=plan.PlanetId,EntityId=plan.EntityId,ExpectedFactoryStateHash=plan.FactoryStateHash};
        var blocker=MinimalDysonTargetPolicy.Validate(request,inspected.Value,target);
        return blocker is null && target is not null && plan.MinimalTarget is not null
            && ReferenceEquals(GameMain.data,plan.MinimalTargetWorld)
            && ReferenceEquals(FindExistingMinimalTargetLayer(target),plan.MinimalTargetLayer)
            && ReferenceEquals(DysonSphereSegmentRenderer.GetNodeProtoMesh(0),plan.MinimalTargetMesh)
            && ReferenceEquals(DysonSphereSegmentRenderer.GetNodeProtoMaterial(0),plan.MinimalTargetMaterial)
            && EjectorWithinNativeBuildReach(plan.EntityId) ? null : Stale(blocker??"minimal_target_native_binding_changed");
    }

    private void ApplyMinimalDysonTargetOnMainThread(NormalActionPlanPayload plan)
    {
        if(RevalidateMinimalDysonTargetOnMainThread(plan) is { } error)throw new InvalidOperationException(error.Message);
        var layer=plan.MinimalTargetLayer!;var target=plan.MinimalTarget!;
        var pos=new Vector3(target.Radius,0f,0f);
        // This is the same ordinary business operation as the Native node
        // brush after its empty-layer/collision/stress premises are proved.
        var id=layer.NewDysonNode(target.NodePrototypeId,pos);
        if(id<=0 || id>=layer.nodeCursor || id>=layer.nodePool.Length || layer.nodePool[id] is not { } node
            || node.id!=id || node.layerId!=target.LayerId || node.protoId!=target.NodePrototypeId
            || !node.pos.Equals(pos) || node.sp!=0 || node.spMax!=30 || node.spOrdered!=0
            || layer.nodeCount!=1 || layer.frameCount!=0 || layer.shellCount!=0)
            throw new InvalidOperationException("The original native node operation did not prove its exact zero-delivery plan.");
        plan.MinimalTargetNodeId=id;
    }

    private static bool IsMinimalDysonTargetApplied(FactoryEntitySnapshot entity,NormalActionPlanPayload plan)
        => plan.MinimalTargetNodeId>0 && entity.Silo is {NodeCoverageComplete:true} silo && silo.Nodes.Count==1
            && silo.Nodes[0].LayerId==plan.MinimalTarget!.LayerId && silo.Nodes[0].NodeId==plan.MinimalTargetNodeId
            && silo.Nodes[0].StructurePoints==0 && silo.Nodes[0].OrderedStructurePoints==0
            && silo.Nodes[0].MaximumStructurePoints==30;

    private sealed partial class NormalActionPlanPayload
    {
        public static NormalActionPlanPayload ForMinimalDysonTarget(string sessionId,int planetId,int entityId,
            MinimalDysonTargetSnapshot target,DysonSphereLayer layer) => new()
        {
            ActionKind=NormalActionKinds.ConfigureBuilding,SessionId=sessionId,PlanetId=planetId,
            EntityId=entityId,ConfigureMode=BuildingConfigurationModes.MinimalDysonReceivingNode,
            FactoryStateHash=target.ConfigurationStateHash,
            ExpectedStateHash=CanonicalStateHash.Combine("minimal-native-node-plan-v1",sessionId,planetId,entityId,target.ConfigurationStateHash),
            MinimalTarget=target.Copy(),MinimalTargetLayer=layer,MinimalTargetWorld=GameMain.data,
            MinimalTargetMesh=DysonSphereSegmentRenderer.GetNodeProtoMesh(0),
            MinimalTargetMaterial=DysonSphereSegmentRenderer.GetNodeProtoMaterial(0)
        };
        public MinimalDysonTargetSnapshot? MinimalTarget {get;set;}
        public DysonSphereLayer? MinimalTargetLayer {get;set;}
        public GameData? MinimalTargetWorld {get;set;}
        public Mesh? MinimalTargetMesh {get;set;}
        public Material? MinimalTargetMaterial {get;set;}
        public int MinimalTargetNodeId {get;set;}
    }
}
