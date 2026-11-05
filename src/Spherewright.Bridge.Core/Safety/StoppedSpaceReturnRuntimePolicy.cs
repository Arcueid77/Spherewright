using Spherewright.Contracts.Players;
using Spherewright.Contracts.Errors;

namespace Spherewright.Bridge.Core.Safety;

public sealed class StoppedSpaceReturnRuntimeFacts
{
    public bool ExactNativeBinding {get;set;}
    public bool ExactSourceQuarantine {get;set;}
    public bool ExactOriginalLedger {get;set;}
    public bool NativeObserverBound {get;set;}
    public bool NativeControlAvailable {get;set;}
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
    public double LoadedControlMargin {get;set;}
    public double WithdrawalFloor {get;set;}
}
public static class StoppedSpaceReturnRuntimePolicy
{
    public static NativeResumeFailureSnapshot? CaptureFailure(StoppedSpaceReturnRuntimeFacts f)
    {
        var blocker=FirstBlocker(f);if(blocker is null)return null;
        string Flag(bool v)=>v?"true":"false";
        return new NativeResumeFailureSnapshot {
            Code=BridgeErrorCodes.ActionRejected,Stage="stopped_space_home_return_guard",FirstFailedPredicate=blocker,
            CapturedAtGameTick=f.CurrentTick,CapturedAtUtc=f.Now,FactsState="observed",
            Predicates=new() {{"exact_native_binding",Flag(f.ExactNativeBinding)},
                {"exact_source_quarantine",Flag(f.ExactSourceQuarantine)},{"exact_original_ledger",Flag(f.ExactOriginalLedger)},
                {"native_observer_bound",Flag(f.NativeObserverBound)},{"native_control_available",Flag(f.NativeControlAvailable)},
                {"player_order_absent_or_owned",Flag(f.PlayerOrderAbsentOrOwned)}},
            Observations=new() {{"movement_state",f.MovementState}},
            Values=new() {{"core_j",FiniteOrNull(f.Core)},{"one_native_tick_upper_j",FiniteOrNull(f.NextTickUpper)},
                {"required_two_native_ticks_j",FiniteOrNull(2*f.NextTickUpper)},
                {"loaded_energy_j",FiniteOrNull(f.LoadedEnergy)},{"loaded_control_margin_j",FiniteOrNull(f.LoadedControlMargin)},
                {"withdrawal_floor_j",FiniteOrNull(f.WithdrawalFloor)},
                {"elapsed_game_ticks",f.CurrentTick-f.SourceTick},{"maximum_game_ticks",f.MaximumTicks},
                {"stored_warpers",f.StoredWarpers},{"starting_warpers",f.StartingWarpers},
                {"current_star",f.CurrentStar},{"home_star",f.HomeStar}} };
    }
    private static double? FiniteOrNull(double v)=>double.IsNaN(v)||double.IsInfinity(v)?null:v;
    public static bool AllowsNextNativeTick(StoppedSpaceReturnRuntimeFacts f)=>FirstBlocker(f) is null;
    public static string? FirstBlocker(StoppedSpaceReturnRuntimeFacts f)
    {
        bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        if(!f.ExactNativeBinding)return "space_return_native_binding";
        if(!f.ExactSourceQuarantine)return "space_return_source_quarantine";
        if(!f.ExactOriginalLedger)return "space_return_original_ledger";
        if(!f.NativeObserverBound)return "space_return_native_observer";
        if(!f.NativeControlAvailable)return "space_return_native_control";
        if(!f.PlayerOrderAbsentOrOwned)return "space_return_order_owner";
        if(!new[]{"Sail","Fly","Drift","Walk"}.Contains(f.MovementState))return "space_return_movement_mode";
        if(f.HomeStar<=0 || f.CurrentStar!=f.HomeStar)return "space_return_original_star";
        if(f.SourceTick<0 || f.CurrentTick<f.SourceTick || f.MaximumTicks<=0 || f.CurrentTick-f.SourceTick>=f.MaximumTicks)
            return "space_return_tick_budget";
        if(!f.Deadline.HasValue || f.Now>=f.Deadline.Value)return "space_return_wall_budget";
        if(f.Now>=f.ObservationLease)return "space_return_observation_lease";
        if(f.WarpCommand is not false || f.Warping is not false || f.WarpState!=0)return "space_return_warp_exit";
        if(f.StartingWarpers<0 || f.StoredWarpers!=f.StartingWarpers)return "space_return_warper_conservation";
        if(!Finite(f.NextTickUpper) || f.NextTickUpper<=0 || !Finite(f.WithdrawalFloor) || f.WithdrawalFloor<=0
            || !Finite(f.LoadedControlMargin) || f.LoadedControlMargin<0)return "space_return_energy_facts";
        if(!Finite(f.Core) || f.Core<2*f.NextTickUpper)return "space_return_core_two_native_ticks";
        if(!Finite(f.LoadedEnergy) || !VoyagerControlReservePolicy.CanSpendBeforeFloor(f.LoadedEnergy,
            f.WithdrawalFloor,f.LoadedControlMargin+2*f.NextTickUpper,0))return "space_return_loaded_control_reserve";
        return null;
    }
}
