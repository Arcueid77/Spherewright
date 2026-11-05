namespace Spherewright.Bridge.Core.Safety;

public sealed class StoppedSpaceReturnBudgetFacts
{
    public long SourceTick {get;set;}
    public long JourneyStartedTick {get;set;}
    public long RecordedSpentTicks {get;set;}
    public long JourneyMaximumTicks {get;set;}
    public long OriginalReturnTicks {get;set;}
    public long RequestedTicks {get;set;}
    public double RequestedWallSeconds {get;set;}
    public double Distance {get;set;}
    public double SailSpeed {get;set;}
    public double Core {get;set;}
    public double CoreCapacity {get;set;}
    public double MinimumCoreRatio {get;set;}
    public double OriginalReturnCoreMinimum {get;set;}
    public double LoadedEnergy {get;set;}
    public double WithdrawalFloor {get;set;}
    public double NextTickEnergyUpper {get;set;}
}
public sealed class StoppedSpaceReturnBudgetDecision
{
    public bool Proved {get;internal set;}
    public string? Blocker {get;internal set;}
    public long ConservedPriorSpent {get;internal set;}
    public double RequiredFlightEnergy {get;internal set;}
}
public static class StoppedSpaceReturnBudgetPolicy
{
    public static StoppedSpaceReturnBudgetDecision Evaluate(StoppedSpaceReturnBudgetFacts f)
    {
        var d=new StoppedSpaceReturnBudgetDecision();
        bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        if(f.SourceTick<f.JourneyStartedTick || f.JourneyStartedTick<0 || f.RecordedSpentTicks<0
            || f.OriginalReturnTicks<3600 || f.RequestedTicks<3600 || f.RequestedTicks>f.OriginalReturnTicks
            || !Finite(f.RequestedWallSeconds) || f.RequestedWallSeconds<=0 || f.RequestedWallSeconds>21600)
        {d.Blocker="space_return_finite_original_budget_required";return d;}
        d.ConservedPriorSpent=Math.Max(f.RecordedSpentTicks,f.SourceTick-f.JourneyStartedTick);
        if(d.ConservedPriorSpent>f.JourneyMaximumTicks || f.RequestedTicks>f.JourneyMaximumTicks-d.ConservedPriorSpent)
        {d.Blocker="space_return_original_ticks_exhausted";return d;}
        if(new[]{f.Distance,f.SailSpeed,f.Core,f.CoreCapacity,f.MinimumCoreRatio,f.OriginalReturnCoreMinimum,
            f.LoadedEnergy,f.WithdrawalFloor,f.NextTickEnergyUpper}.Any(v=>!Finite(v))
            || f.Distance<0 || f.SailSpeed<300 || f.CoreCapacity<=0 || f.MinimumCoreRatio<.8 || f.MinimumCoreRatio>1
            || f.OriginalReturnCoreMinimum<=0 || f.LoadedEnergy<=0 || f.WithdrawalFloor<=0 || f.NextTickEnergyUpper<=0)
        {d.Blocker="space_return_energy_or_distance_unknown";return d;}
        if(Math.Ceiling(f.Distance/f.SailSpeed*60d)+7200d>f.RequestedTicks)
        {d.Blocker="space_return_distance_exceeds_finite_cap";return d;}
        d.RequiredFlightEnergy=Math.Max(f.CoreCapacity*1.5d,f.Distance*1000d);
        if(f.Core<Math.Max(f.OriginalReturnCoreMinimum,Math.Max(f.CoreCapacity*f.MinimumCoreRatio,2*f.NextTickEnergyUpper))
            || f.LoadedEnergy<d.RequiredFlightEnergy+f.WithdrawalFloor+2*f.NextTickEnergyUpper)
        {d.Blocker="space_return_closed_funded_native_envelope_unproved";return d;}
        d.Proved=true;return d;
    }
}
