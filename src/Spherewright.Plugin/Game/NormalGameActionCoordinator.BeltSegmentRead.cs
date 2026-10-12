using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    // Unity-thread geometry observation only. Reuses the ordinary free-path
    // generator, without occupancy checks, build tools, tokens or native writes.
    internal static FactoryNativeBeltSegmentSnapshot ReadOnlyNativeBeltSegment(
        PlanetFactory factory, BeltRouteSegment segment, int index)
    {
        var result=new FactoryNativeBeltSegmentSnapshot
        {
            Index=index, NativeGroundRadius=factory.planet.radius+.2f,
            Segment=new BeltRouteSegment{RoutingMode=segment.RoutingMode,
                Start=Snapshot(ToVector(segment.Start)),End=Snapshot(ToVector(segment.End)),
                StartAltitudeLevel=segment.StartAltitudeLevel,EndAltitudeLevel=segment.EndAltitudeLevel},
            ReasonCode="native_belt_segment_generation_unavailable",
        };
        if(factory.planet.aux?.customGrids is null || factory.planet.aux.activeGrid is null)
        {result.ReasonCode="native_belt_segment_grid_unavailable";return result;}
        EndpointPoint start; EndpointPoint end;
        if(segment.RoutingMode==BeltPathModes.NativeElevatedGrid)
        {
            if(!TryResolveElevatedBeltEndpoint(factory,segment.Start,segment.StartAltitudeLevel,out start)
                || !TryResolveElevatedBeltEndpoint(factory,segment.End,segment.EndAltitudeLevel,out end))
            { result.ReasonCode="native_belt_segment_layer_unavailable";return result; }
        }
        else
        {
            var a=factory.planet.aux.Snap(ToVector(segment.Start),onTerrain:true);
            var b=factory.planet.aux.Snap(ToVector(segment.End),onTerrain:true);
            start=new EndpointPoint(0,-1,new Pose(a,Maths.SphericalRotation(a,0f)));
            end=new EndpointPoint(0,-1,new Pose(b,Maths.SphericalRotation(b,0f)));
        }
        if(!FactoryGeometryReadPolicy.SamePoint(Snapshot(start.Pose.position),segment.Start)
            || !FactoryGeometryReadPolicy.SamePoint(Snapshot(end.Pose.position),segment.End))
        { result.ReasonCode="native_belt_segment_requires_exact_snapped_endpoints";return result; }
        var item=LDB.items.Select(2001);
        if(item?.prefabDesc is null)
        {result.ReasonCode="native_belt_segment_prototype_unavailable";return result;}
        Vector3Snapshot[]? nativePoints=null;
        var generated=TryCreateBeltSteps(factory,item,start,end,segment.RoutingMode,out var points,out var rejection,
            value=>result.NativeMaximumSlope=FactoryGeometryReadPolicy.Finite(value)&&value>=0?value:(float?)null,
            values=>nativePoints=values.Select(Snapshot).ToArray());
        if(!generated) {result.ReasonCode="native_belt_segment_generation_rejected:"+rejection;return result;}
        if(!result.NativeMaximumSlope.HasValue || nativePoints is null)
        {result.ReasonCode="native_belt_segment_complete_output_unavailable";return result;}
        if(points.Count<2 || points.Count>FactoryGeometryReadPolicy.MaximumNativePointsPerSegment
            || points.Any(p=>p.InputObjectId!=0 || p.OutputObjectId!=0 || p.SourceBeltAnchor is not null || p.DestinationBeltAnchor is not null))
        {result.ReasonCode="native_belt_segment_complete_point_limit";return result;}
        if(nativePoints.Length!=points.Count
            || !FactoryGeometryReadPolicy.SamePoint(nativePoints[0],segment.Start)
            || !FactoryGeometryReadPolicy.SamePoint(nativePoints[nativePoints.Length-1],segment.End)
            || points.Where((p,i)=>!FactoryGeometryReadPolicy.SamePoint(Snapshot(p.Position),nativePoints[i])).Any())
        {result.ReasonCode="native_belt_segment_generation_changed_points";return result;}
        result.Points=nativePoints.ToList();
        result.Observed=true;result.ReasonCode=null;
        return result;
    }
}
