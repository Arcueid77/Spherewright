namespace Spherewright.Bridge.Core.Safety;

// Positive current-owned support only. A complete owned foot overlap may
// coexist with a false Native sphere query at a contact boundary; it never
// substitutes for that query or publishes idle. The airborne branch still
// needs an independently proved downward supporting cast.
public static class NativeSurfaceRecoverySupportPolicy
{
    public static bool Proved(bool? nativeGroundSphereHit, int? footCount, bool footQueryComplete,
        bool allFootHitsCurrentTerrain, bool downwardSupportingCastProved)
        => nativeGroundSphereHit.HasValue && footQueryComplete && footCount is >= 0 and <= 256
            && allFootHitsCurrentTerrain && (nativeGroundSphereHit.Value
                ? footCount > 0 : downwardSupportingCastProved);
}
