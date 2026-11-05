namespace Spherewright.Bridge.Core.Safety;

public sealed class VoyagerStoredWarperReserveDecision
{
    public bool Proved { get; internal set; }
    public int? RequiredWarpers { get; internal set; }
    public string? Code { get; internal set; }
    public string? Message { get; internal set; }
}

/// <summary>The same physical chamber requirement before launch, commit and warp entry.</summary>
public static class VoyagerStoredWarperReservePolicy
{
    public static VoyagerStoredWarperReserveDecision Evaluate(int nativeCount,int currentEntries,int returnEntries,int extra)
    {
        var required=(long)currentEntries+returnEntries+extra;
        if(nativeCount<0 || currentEntries<1 || returnEntries<0 || extra<0
            || extra>VoyagerRouteBudgetPolicy.MaximumExtraWarperReserve || required>int.MaxValue)
            return new() { Code="invalid_remaining_warper_reserve",
                Message="Real native counts and all remaining entry reservations must be valid; the current entry is included." };
        return new() { Proved=nativeCount>=required,RequiredWarpers=(int)required,
            Code=nativeCount>=required?null:"warper_return_reserve_insufficient",
            Message=nativeCount>=required?null:"Real native warpers must cover this leg, remaining return entries and declared extras; package items are not reserved here." };
    }
}
