namespace Spherewright.Bridge.Core.Safety;

public static class VoyagerWallWindowPolicy
{
    public const int MinimumExplicitSeconds = 600;
    public const int MaximumExplicitSeconds = 86400;
    public static bool ValidExplicit(int? seconds)=>seconds is null
        || seconds>=MinimumExplicitSeconds && seconds<=MaximumExplicitSeconds;

    // Preserve the legacy default when no surface work is requested.  A new
    // finite work allowance gets a matching default wall window, still capped
    // by the existing explicit-window bound.
    public static int? EffectiveOutboundSeconds(long flightTicks,long surfaceWorkTicks,int? explicitSeconds)
    {
        if(flightTicks<7200 || flightTicks>108000 || surfaceWorkTicks<0
            || surfaceWorkTicks>VoyagerRecoveryWindowPolicy.MaximumAdditionalVoyagerGameTicks
            || !ValidExplicit(explicitSeconds)) throw new ArgumentOutOfRangeException(nameof(surfaceWorkTicks));
        if(explicitSeconds.HasValue || surfaceWorkTicks==0) return explicitSeconds;
        var estimated=Math.Ceiling((flightTicks+(double)surfaceWorkTicks)/60d*2d+120d);
        return (int)Math.Min(MaximumExplicitSeconds,Math.Max(MinimumExplicitSeconds,estimated));
    }

    public static DateTimeOffset OutboundDeadline(DateTimeOffset issued,long maximumGameTicks,int? explicitSeconds)
    {
        if(issued==default || maximumGameTicks<7200 || maximumGameTicks>108000 || !ValidExplicit(explicitSeconds))
            throw new ArgumentOutOfRangeException(nameof(explicitSeconds));
        return issued.AddSeconds(explicitSeconds??(maximumGameTicks/60d*2d+120d));
    }

    public static bool ValidRecord(DateTimeOffset issued,DateTimeOffset deadline,int? explicitSeconds)
        => issued!=default && deadline>issued && (explicitSeconds is null
            ? deadline-issued<=TimeSpan.FromHours(2)
            : ValidExplicit(explicitSeconds) && deadline==issued.AddSeconds(explicitSeconds.Value));
}
