namespace Spherewright.Bridge.Core.Safety;

public sealed class StoppedSpaceReturnRuntimeFacts
{
    public bool ExactNativeBinding {get;set;}
    public bool ExactSourceQuarantine {get;set;}
    public bool ExactOriginalLedger {get;set;}
    public bool NativeObserverBound {get;set;}
    public bool PlayerOrderAbsentOrOwned {get;set;}
    public string MovementState {get;set;}=string.Empty;
    public int? CurrentStar {get;set;}
    public int HomeStar {get;set;}
    public long SourceTick {get;set;}
    public long CurrentTick {get;set;}
    public long MaximumTicks {get;set;}
    public DateTimeOffset Now {get;set;}
    public DateTimeOffset? Deadline {get;set;}
    public DateTimeOffset ObservationLease {get;set;}
    public bool? WarpCommand {get;set;}
    public bool? Warping {get;set;}
    public float? WarpState {get;set;}
    public int StoredWarpers {get;set;}
    public int StartingWarpers {get;set;}
    public double Core {get;set;}
    public double LoadedEnergy {get;set;}
    public double NextTickUpper {get;set;}
    public double WithdrawalFloor {get;set;}
}
public static class StoppedSpaceReturnRuntimePolicy
{
    public static bool AllowsNextNativeTick(StoppedSpaceReturnRuntimeFacts f)
    {
        bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        return f.ExactNativeBinding && f.ExactSourceQuarantine && f.ExactOriginalLedger && f.NativeObserverBound
            && f.PlayerOrderAbsentOrOwned && new[]{"Sail","Fly","Drift","Walk"}.Contains(f.MovementState)
            && f.HomeStar>0 && f.CurrentStar==f.HomeStar && f.CurrentTick>=f.SourceTick && f.SourceTick>=0
            && f.MaximumTicks>0 && f.CurrentTick-f.SourceTick<f.MaximumTicks
            && f.Deadline.HasValue && f.Now<f.Deadline.Value && f.Now<f.ObservationLease
            && f.WarpCommand is false && f.Warping is false && f.WarpState==0
            && f.StartingWarpers>=0 && f.StoredWarpers==f.StartingWarpers
            && Finite(f.NextTickUpper) && f.NextTickUpper>0 && Finite(f.WithdrawalFloor) && f.WithdrawalFloor>0
            && Finite(f.Core) && f.Core>=2*f.NextTickUpper && Finite(f.LoadedEnergy)
            && f.LoadedEnergy>=f.WithdrawalFloor+2*f.NextTickUpper;
    }
}
