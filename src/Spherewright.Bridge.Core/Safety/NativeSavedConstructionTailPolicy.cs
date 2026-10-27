using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Safety;

// Stop-only continuation after an exact owned normal save. This is not a new
// construction lease: every drone must already be in ordinary Native stage4,
// dispatch disabled, with no current/queued work targets. The consumer binds
// the complete original debit/entity receipts and every real factory pass.
public static class NativeSavedConstructionTailPolicy
{
    public const int MaximumGameTicks = 752; // ceil(40/(10/60/3)) +32
    public const double MaximumWallSeconds = 30d;
    public const double MaximumAnchorDisplacementMeters = .025d;
    public const double MaximumEjectDisplacementMeters = .026d;
    public const double MaximumDistanceMeters = 40d;
    public static bool StableReturnFitsRemaining(double distance,double speed,long remaining)
        => Known(distance) && distance<=MaximumDistanceMeters && Known(speed) && speed>=10d && speed<=12d
            && remaining>=0 && remaining<=MaximumGameTicks && Math.Ceiling(distance*180d/speed)+32d<=remaining;

    public static bool CopiedReturnStepProved(NativeConstructionDroneMotionFacts f,
        double anchorRadius, double anchorDisplacement, double fixedToLiveEjectDistance,
        Vector3Snapshot fixedEject)
    {
        if (f.Stage != 4 || !Known(anchorRadius) || anchorRadius < 160d || anchorRadius > 6000d
            || !Known(anchorDisplacement) || anchorDisplacement > MaximumAnchorDisplacementMeters
            || !Known(fixedToLiveEjectDistance) || fixedToLiveEjectDistance > MaximumEjectDisplacementMeters
            || f.SpeedMetersPerSecond < 10f || f.SpeedMetersPerSecond > 12f
            || MaximumDistanceMeters > (anchorRadius + 2.8d) / 4d) return false;
        var next = NativeConstructionDroneReservePolicy.PredictCopiedOrdinaryStep(f);
        if (next is null) return false;
        var distance = Distance(f.Position.X, f.Position.Y, f.Position.Z, fixedEject);
        var after = Distance(next.X, next.Y, next.Z, fixedEject);
        if (!Known(distance) || !Known(after) || distance > MaximumDistanceMeters || after > MaximumDistanceMeters) return false;
        if (NativeConstructionDroneReservePolicy.CopiedStepWouldNativeRecycle(f) || distance < .5d) return true;
        var step = f.SpeedMetersPerSecond / 60d;
        // Native ideal normalized-chord closure >=5s/6 for D<=q/4.
        // .01 arithmetic +2*.026 live/fixed +.001 distance +.01 guard
        // =.073m < (5/6-1/3)*(10/60)=.083333m. The smaller-D radial
        // branch retains the independently checked contracting recurrence.
        return distance >= 2d * step ? after + .01d <= distance - step / 3d
            : after + .01d <= .75d * distance + .04d;
    }

    private static double Distance(double x, double y, double z, Vector3Snapshot p)
        => Math.Sqrt((x-p.X)*(x-p.X)+(y-p.Y)*(y-p.Y)+(z-p.Z)*(z-p.Z));
    private static bool Known(double x) => !double.IsNaN(x) && !double.IsInfinity(x) && x >= 0d;
}
