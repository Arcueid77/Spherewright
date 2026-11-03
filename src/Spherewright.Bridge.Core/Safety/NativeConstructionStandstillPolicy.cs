namespace Spherewright.Bridge.Core.Safety;

public static class NativeConstructionStandstillPolicy
{
    public static bool SupportProved(bool own,double? height,double? normal)=>own
        // Native Walk queries a radius.35 sphere at root+.15: a sleeping
        // grounded root may sit.20m above its independently proved solid.
        // Stable real501 frames and zero motion remain separate requirements.
        && height is >=-.21d and <=.04d && normal is >=.5d and <=1.001d;
    public static bool StableProved(int consecutiveFrames,long age,double displacement)=>consecutiveFrames>=4
        && age>=0 && age<=1 && !double.IsNaN(displacement) && !double.IsInfinity(displacement)
        && displacement>=0 && displacement<=.001d;
}
