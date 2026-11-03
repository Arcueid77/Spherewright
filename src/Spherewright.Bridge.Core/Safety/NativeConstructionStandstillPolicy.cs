namespace Spherewright.Bridge.Core.Safety;

public static class NativeConstructionStandstillPolicy
{
    public static bool SupportProved(bool own,double? height,double? normal)=>own
        && height is >=-.04d and <=.04d && normal is >=.5d and <=1.001d;
    public static bool StableProved(int consecutiveFrames,long age,double displacement)=>consecutiveFrames>=4
        && age>=0 && age<=1 && !double.IsNaN(displacement) && !double.IsInfinity(displacement)
        && displacement>=0 && displacement<=.001d;
}
