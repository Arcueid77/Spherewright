using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameCallResult<PreparedNormalAction> PrepareBoundedBeltRoutePreviewOnMainThread(
        PrepareBuildRequest request,PlayerStateSnapshot playerBefore)
    {
        var factory=GameMain.localPlanet!.factory; var player=GameMain.mainPlayer;
        var specification=request.BeltRoutePreview!;
        using var geometryTool=NativeFactoryGeometry.Open(factory);
        var hash=NativeFactoryGeometry.Fingerprint(factory,_sessions.SessionId!,geometryTool,out _,out _);
        if(hash!=specification.ExpectedFactoryGeometryHash) return StalePlan("The frozen factory layout, configuration, collision prototypes or terrain changed.");
        var item=LDB.items.Select(2001);
        var steps=new List<BuildStepPlan>();
        var assessment=new BeltRoutePreviewSnapshot{FactoryGeometryHash=hash};
        for(var i=0;i<specification.Segments.Count;i++)
        {
            var segment=specification.Segments[i]; EndpointPoint start; EndpointPoint end;
            if(segment.RoutingMode==BeltPathModes.NativeElevatedGrid)
            {
                if(!TryResolveElevatedBeltEndpoint(factory,segment.Start,segment.StartAltitudeLevel,out start)
                    || !TryResolveElevatedBeltEndpoint(factory,segment.End,segment.EndAltitudeLevel,out end))
                    return InvalidPlan("belt_route_preview_native_layer_unavailable:"+i);
            }
            else
            {
                var a=factory.planet.aux.Snap(ToVector(segment.Start),onTerrain:true);
                var b=factory.planet.aux.Snap(ToVector(segment.End),onTerrain:true);
                start=new EndpointPoint(0,-1,new Pose(a,Maths.SphericalRotation(a,0f)));
                end=new EndpointPoint(0,-1,new Pose(b,Maths.SphericalRotation(b,0f)));
            }
            // Points MUST first have been read from native grid geometry. Native
            // resnapping is checked for identity; no silently relocated waypoint.
            if(!FactoryGeometryReadPolicy.SamePoint(Snapshot(start.Pose.position),segment.Start)
                || !FactoryGeometryReadPolicy.SamePoint(Snapshot(end.Pose.position),segment.End))
                return InvalidPlan("belt_route_preview_requires_exact_native_snapped_waypoints:"+i);
            if(!TryCreateBeltSteps(factory,item,start,end,segment.RoutingMode,out var part,out var rejection))
                return InvalidPlan("belt_route_preview_native_generation_rejected:segment:"+i+":"+rejection);
            if(part.Count<2 || part.Count>64 || part.Any(p=>p.InputObjectId!=0 || p.OutputObjectId!=0
                || p.SourceBeltAnchor is not null || p.DestinationBeltAnchor is not null))
                return InvalidPlan("belt_route_preview_generated_segment_limit:"+i);
            var first=steps.Count;
            if(i>0)
            {
                if(!FactoryGeometryReadPolicy.SamePoint(Snapshot(steps[steps.Count-1].Position),Snapshot(part[0].Position)))
                    return InvalidPlan("belt_route_preview_native_seam_mismatch:"+i);
                first--; // Only the identical shared endpoint is represented once.
            }
            steps.AddRange(part.Skip(i==0?0:1));
            if(steps.Count>BeltRoutePreviewPolicy.MaximumPoints) return InvalidPlan("belt_route_preview_complete_point_limit");
            assessment.Segments.Add(new NativeBeltRouteSegment{Index=i,RoutingMode=segment.RoutingMode,
                StartAltitudeLevel=segment.StartAltitudeLevel,EndAltitudeLevel=segment.EndAltitudeLevel,
                FirstBeltIndex=first,LastBeltIndex=steps.Count-1,NativePointCount=part.Count});
        }
        for(var i=0;i<steps.Count;i++)
        {
            steps[i].BeltPathMode=BeltPathModes.NativeRoutePreview;
            steps[i].InputStepIndex=i==0?-1:i-1; steps[i].OutputStepIndex=i+1<steps.Count?i+1:-1;
        }
        assessment.Points=steps.Select((p,i)=>new NativeBeltRoutePoint{Index=i,PlannedPosition=Snapshot(p.Position),Position=Snapshot(p.Position),
            Rotation=NativeFactoryGeometry.Rotation(p.Rotation),Tilt=p.Tilt}).ToList();
        var passed=TryValidateBeltBuild(factory,player,item,steps,out var accepted,out var failure,out _,
            assessment);
        if (passed && (accepted.Count != steps.Count || accepted.Where((p, i) =>
            !FactoryGeometryReadPolicy.SamePoint(Snapshot(p.Position), Snapshot(steps[i].Position))).Any()))
        {
            // Retain all actual native point results on an integrity rejection;
            // do not silently flatten the route or withhold its failed geometry.
            passed = false;
            failure = "belt_route_preview_native_changed_complete_points";
        }
        if(!passed)
        {
            assessment.FailureStage=assessment.NativePathCheckPerformed?"native_full_path":"pre_native_path_guard";
            assessment.FailureCondition ??= failure;
            assessment.Endpoints=new BeltEndpointPreviewSnapshot{SessionId=_sessions.SessionId!,PlanetId=request.PlanetId,
                Revision=_sessions.Revision,CapturedAtGameTick=GameMain.gameTick,AssessmentHash=CanonicalStateHash.Combine(
                    BeltRoutePreviewPolicy.Phase,hash,playerBefore.StateHash,failure)};
            assessment.Endpoints.Blockers.Add(failure);
            var negative=CreateReadOnlyRouteResponse(request,assessment,steps);
            if(NativeFactoryGeometry.Fingerprint(factory,_sessions.SessionId!,geometryTool,out _,out _)!=hash)
                return StalePlan("Factory geometry changed during the failed read-only native path check.");
            var after=_reader.GetPlayerStateOnMainThread(_sessions.SessionId,new LocalPlanetRequest{PlanetId=request.PlanetId});
            if(!after.Success || after.Value!.StateHash!=playerBefore.StateHash) return StalePlan("Player changed during the read-only native path check.");
            return GameCallResult<PreparedNormalAction>.Succeeded(negative);
        }
        assessment.NativePathCheckPassed=true;
        var preparation=BuildPreparation.Succeeded(NormalBuildKinds.Belt,accepted);
        var result=PrepareBeltEndpointPreviewOnMainThread(request,preparation,playerBefore,assessment);
        if(NativeFactoryGeometry.Fingerprint(factory,_sessions.SessionId!,geometryTool,out _,out _)!=hash)
            return StalePlan("Factory geometry changed during the complete linked route qualification.");
        return result;
    }

    private static PreparedNormalAction CreateReadOnlyRouteResponse(PrepareBuildRequest request,
        BeltRoutePreviewSnapshot assessment,IReadOnlyList<BuildStepPlan> steps)
    {
        var result=new PreparedNormalAction{Prepared=false,CommitAllowedNow=false,PlanToken=string.Empty,
            ActionKind=NormalActionKinds.Build,BuildKind="belt_route_preview",ExpectedStateHash=request.ExpectedPlayerStateHash,
            BeltRoutePreview=assessment,PlannedPath=assessment.Points.Select(p=>p.PlannedPosition).ToList(),
            PlannedBeltPath=new BeltPathPlanSnapshot{NativeValidationMode="full_path_stage1_and_joint_all_objects",
                RoutingMode=BeltPathModes.NativeRoutePreview,SourceBindingMode="none",DestinationBindingMode="none",NewObjectCount=steps.Count},
            CompletionCondition="Complete read-only route assessment; no token, action, prebuild or supply credit."};
        result.ItemBudget.Add(new ActionItemBudget{ItemId=2001,Count=steps.Count,Direction="preview-construction-consumption",Name=LDB.items.Select(2001).name});
        foreach(var group in new[]{request.BeltRoutePreview!.SourceSorterItemId,request.BeltRoutePreview.DestinationSorterItemId}.GroupBy(i=>i))
            result.ItemBudget.Add(new ActionItemBudget{ItemId=group.Key,Count=group.Count(),Direction="preview-construction-consumption",Name=LDB.items.Select(group.Key)?.name??string.Empty});
        return result;
    }
}
