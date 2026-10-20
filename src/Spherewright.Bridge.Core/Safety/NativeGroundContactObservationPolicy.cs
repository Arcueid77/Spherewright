using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>Only copies bounded query outcomes. Never a contact or execution certificate.</summary>
public static class NativeGroundContactObservationPolicy
{
    public static NativePlayerGroundContactSnapshot Capture(long tick, string stage,
        Action<NativePlayerGroundContactSnapshot> captureContext, Func<bool> checkSphere,
        Func<(bool Hit, float Distance, NativePlayerGroundColliderSnapshot? Collider)> sphereCast)
    {
        var result = new NativePlayerGroundContactSnapshot { CapturedAtGameTick = tick, CaptureStage = stage };
        try
        {
            captureContext(result);
            if (!result.AutoSyncTransforms.HasValue || !result.QueriesHitTriggers.HasValue)
            { result.ReasonCode = "native_ground_query_context_unavailable"; return result; }
            result.CheckSphereHit = checkSphere();
            if (result.CheckSphereHit == true)
            {
                result.SphereCastAttempted = false; result.GroundDistance = 0f;
            }
            else
            {
                result.SphereCastAttempted = true;
                var cast = sphereCast(); result.SphereCastHit = cast.Hit;
                if (cast.Hit)
                {
                    if (float.IsNaN(cast.Distance) || float.IsInfinity(cast.Distance) || cast.Distance < 0f || cast.Distance > 20f)
                    { result.ReasonCode = "native_ground_cast_distance_unproved"; return result; }
                    result.GroundDistance = cast.Distance; result.HitCollider = cast.Collider;
                    if (cast.Collider is null) { result.ReasonCode = "native_ground_cast_collider_unavailable"; return result; }
                }
                // A miss does not measure20m. Only the explicitly labelled Native cache may contain that sentinel.
            }
            result.State = "observed";
        }
        catch (Exception exception) { result.ReasonCode = "native_ground_query_failed:" + exception.GetType().Name; }
        return result;
    }
}
