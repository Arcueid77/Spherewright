namespace Spherewright.Bridge.Core.Safety;

public static class VoyagerWallWindowPolicy
{
    public const int MinimumExplicitSeconds = 600;
    public const int MaximumExplicitSeconds = 86400;
    public static bool ValidExplicit(int? seconds)=>seconds is null
        || seconds>=MinimumExplicitSeconds && seconds<=MaximumExplicitSeconds;

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
