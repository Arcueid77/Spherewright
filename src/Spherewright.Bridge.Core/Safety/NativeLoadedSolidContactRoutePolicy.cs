using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Safety;

// Routing permission for an independent solid-support cast, never ground or
// idle. A known own ocean trigger is not a foreign solid and never supports.
public static class NativeLoadedSolidContactRoutePolicy
{
    public static bool MayProbe(NativePlanetTerrainPhysicsSnapshot s) => s.State=="observed" && s.Complete && !s.Truncated
        && s.NativeGroundSphereHit==false && s.RootLayer==31 && s.BodyLayer==31
        && s.FootOverlapComplete==true && s.FootHitDetailsComplete==true && s.FootOverlapCount is >0 and <=256
        && s.FootHits is {} hits && hits.Count==s.FootOverlapCount
        && hits.Any(Solid) && hits.All(h=>Solid(h) || Ocean(h));
    private static bool Common(NativeTerrainFootHitPhysicsSnapshot h)=>h.State=="observed" && h.Enabled==true
        && h.ActiveInHierarchy==true && h.Layer==31 && h.SameDefaultPhysicsScene==true && h.UnknownOwner==false;
    private static bool Solid(NativeTerrainFootHitPhysicsSnapshot h)=>Common(h) && h.Type=="mesh" && h.IsTrigger==false
        && h.IsCurrentSurface==true && h.CurrentSurfaceUnityMatchCount==1 && h.UniqueSurfaceIndex>=0 && h.SharedMeshPresent==true;
    private static bool Ocean(NativeTerrainFootHitPhysicsSnapshot h)=>Common(h) && h.Type=="sphere" && h.IsTrigger==true
        && h.SameCurrentOcean==true && h.SameCurrentOceanUnity==true && h.IsCurrentSurface==false;
}
