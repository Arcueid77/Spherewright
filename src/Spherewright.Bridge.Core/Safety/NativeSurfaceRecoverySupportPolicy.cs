namespace Spherewright.Bridge.Core.Safety;

// Complete current-owned foot contacts (terrain or exactly registered current-world
// objects) plus an independent current-owned downward plane. A positive
// sphere against a vein's side is not proof of support or strict idle.
public static class NativeSurfaceRecoverySupportPolicy
{
    public static bool CutProved(bool? nativeGroundSphereHit, int? footCount, bool complete,
        int terrainCount, int provedOwnedObjectCount, bool castOwnerProved, double? normalDotUp, double? heightFromActor)
        => terrainCount >= 0 && provedOwnedObjectCount >= 0 && footCount.HasValue
            && (long)terrainCount + provedOwnedObjectCount == footCount
            && normalDotUp is >= .5d and <= 1.001d && heightFromActor is >= -2d and <= .2d
            && Proved(nativeGroundSphereHit, footCount, complete, true, castOwnerProved);

    public static bool Proved(bool? nativeGroundSphereHit, int? footCount, bool footQueryComplete,
        bool allFootHitsCurrentOwnedSupport, bool downwardSupportingCastProved)
        => nativeGroundSphereHit.HasValue && footQueryComplete && footCount is >= 0 and <= 256
            && allFootHitsCurrentOwnedSupport && downwardSupportingCastProved
            && (!nativeGroundSphereHit.Value || footCount > 0);
}
