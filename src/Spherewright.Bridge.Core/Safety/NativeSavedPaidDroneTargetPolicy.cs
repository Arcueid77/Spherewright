namespace Spherewright.Bridge.Core.Safety;

// A new exact-source observation may continue existing Native work only for
// its retained paid prebuild IDs. It never issues another Create or dispatch.
public static class NativeSavedPaidDroneTargetPolicy
{
    public static bool Proved(int stage,int target,IReadOnlyList<int>? queued,IReadOnlyCollection<int>? paid)
    {
        if(stage<1 || stage>4 || queued is null || queued.Count!=3 || paid is null || paid.Count<1 || paid.Count>512
            || paid.Any(n=>n>=0))return false;
        if(stage==4)return target==0 && queued.All(n=>n==0);
        return target<0 && paid.Contains(target) && queued.All(n=>n==0 || n<0 && paid.Contains(n));
    }
}
