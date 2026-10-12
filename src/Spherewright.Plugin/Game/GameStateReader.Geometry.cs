using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    private FactoryGeometryCutSnapshot CaptureFactoryGeometryCut(PlanetFactory factory,FactoryGeometryReadRequest request)
    {
        using var tool=NativeFactoryGeometry.Open(factory);
        var hash=NativeFactoryGeometry.Fingerprint(factory,_sessions.SessionId!,tool,out var built,out var prebuild);
        if(request.ExpectedFactoryGeometryHash is { } expected && expected!=hash)
            throw new InvalidOperationException("factory_geometry_layout_changed");
        var result=new FactoryGeometryCutSnapshot{SessionId=_sessions.SessionId!,PlanetId=factory.planetId,
            Revision=_sessions.Revision,CapturedAtGameTick=GameMain.gameTick,FactoryGeometryHash=hash,BuiltObjectCount=built,PrebuildObjectCount=prebuild};
        foreach(var id in request.ObjectIds)
        {
            var row=NativeFactoryGeometry.CaptureObject(factory,tool,id); result.Objects.Add(row);
            if(!row.Observed) result.Blockers.Add(row.ReasonCode+":"+id);
        }
        if(result.Objects.Sum(o=>o.Colliders.Count)>FactoryGeometryReadPolicy.MaximumColliders)
            throw new InvalidOperationException("factory_geometry_collider_cut_limit");
        for(var i=0;i<request.TerrainPoints.Count;i++)
        {
            var row=NativeFactoryGeometry.CaptureTerrain(factory.planet,request.TerrainPoints[i],i);result.Terrain.Add(row);
            if(!row.Observed) result.Blockers.Add(row.ReasonCode+":"+i);
        }
        if(request.NativeBeltSegments is { } segments)
            for(var i=0;i<segments.Count;i++)
            {
                var row=NormalGameActionCoordinator.ReadOnlyNativeBeltSegment(factory,segments[i],i);
                result.NativeBeltSegments.Add(row);
                if(!row.Observed) result.Blockers.Add(row.ReasonCode+":"+i);
            }
        if(NativeFactoryGeometry.Fingerprint(factory,_sessions.SessionId!,tool,out _,out _)!=hash)
            throw new InvalidOperationException("factory_geometry_layout_changed_during_read");
        result.State=result.Blockers.Count==0?"observed":"unavailable";
        return result;
    }
}
