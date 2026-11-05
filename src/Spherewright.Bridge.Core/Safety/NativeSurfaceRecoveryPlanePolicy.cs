namespace Spherewright.Bridge.Core.Safety;

public sealed class NativeSurfaceRecoveryPlane
{
    public bool Hit { get; set; }
    public bool CurrentOwnerProved { get; set; }
    public bool CurrentTerrainProved { get; set; }
    public double? NormalDotUp { get; set; }
    public double? HeightFromActor { get; set; }
}

// A wide sphere may encounter an owned object's side before the terrain under
// an EMPTY foot cut. Its normal is retained; it is never called support. Only
// a separate first-hit current-terrain ray satisfying the SAME plane bounds
// may replace it during the explicitly bounded, non-idle surface role.
public static class NativeSurfaceRecoveryPlanePolicy
{
    public static bool Supporting(NativeSurfaceRecoveryPlane? p) => p is {Hit:true,CurrentOwnerProved:true}
        && p.NormalDotUp is >= .5d and <= 1.001d && p.HeightFromActor is >= -2d and <= .2d;

    public static NativeSurfaceRecoveryPlane Select(int footCount,bool complete,bool allFootOwnersProved,
        NativeSurfaceRecoveryPlane first,Func<NativeSurfaceRecoveryPlane?> independentTerrainRay,out bool usedTerrainRay)
    {
        usedTerrainRay=false;
        if(Supporting(first) || footCount!=0 || !complete || !allFootOwnersProved
            || !first.Hit || !first.CurrentOwnerProved || first.NormalDotUp is not (>= -1.001d and <= 1.001d)
            || !Known(first.HeightFromActor))return first;
        var terrain=independentTerrainRay();
        if(terrain?.CurrentTerrainProved==true && Supporting(terrain))
        {usedTerrainRay=true;return terrain;}
        return first;
    }
    private static bool Known(double? n)=>n.HasValue && !double.IsNaN(n.Value) && !double.IsInfinity(n.Value);
}
