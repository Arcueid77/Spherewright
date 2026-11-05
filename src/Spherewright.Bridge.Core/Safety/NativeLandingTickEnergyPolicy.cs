namespace Spherewright.Bridge.Core.Safety;

public sealed class NativeLandingTickEnergyFacts
{
    public bool OwnedOrderAndNoManualInput {get;set;}
    public bool JumpAndGuideExcluded {get;set;}
    public double? WalkSpeed {get;set;}
    public double? ObservedRtsSpeed {get;set;}
    public double? WalkPower {get;set;}
    public double? OtherWorkPower {get;set;}
    public double? ThrustPowerPerAcceleration {get;set;}
    public double? ConsumptionRatio {get;set;}
    public double? GravityUpper {get;set;}
    public double? VerticalSpeed {get;set;}
    public double? IndependentEnergyPerTick {get;set;}
    public double? PotentialJumpEnergy {get;set;}
}
/// <summary>One Native Fly/Drift/Walk transition tick, not a strict-stop certificate.</summary>
public static class NativeLandingTickEnergyPolicy
{
    public static double? Evaluate(NativeLandingTickEnergyFacts f)
    {
        bool Known(double? v)=>v.HasValue && v>=0 && !double.IsNaN(v.Value)&&!double.IsInfinity(v.Value);
        if(!f.OwnedOrderAndNoManualInput || !f.JumpAndGuideExcluded
            || new[]{f.WalkSpeed,f.ObservedRtsSpeed,f.WalkPower,f.OtherWorkPower,f.ThrustPowerPerAcceleration,
                f.ConsumptionRatio,f.GravityUpper,f.IndependentEnergyPerTick,f.PotentialJumpEnergy}.Any(v=>!Known(v))
            || !f.VerticalSpeed.HasValue || double.IsNaN(f.VerticalSpeed.Value)||double.IsInfinity(f.VerticalSpeed.Value)
            || f.WalkSpeed is <1 or >60 || f.ObservedRtsSpeed>1e7 || f.ConsumptionRatio<=0)return null;
        var ratio=f.ConsumptionRatio!.Value;var walk=f.WalkSpeed!.Value;
        // Fly RTS target<=2.5*walk; incoming Walk/Drift tails may be larger.
        // Retain their observed norm and widen float interpolation, not truncate
        // them to a grounded walk-only premise.
        var speed=Math.Max(f.ObservedRtsSpeed!.Value,2.5*walk)*(1+1e-5)+.01;
        var walkCost=f.WalkPower!.Value/60*(speed/walk)*Math.Max(1,speed/6)*ratio;
        // Native UseFlyEnergy applies num4 twice, with num3<=1 and recovery<=1.
        var flyFactor=speed/(2.5*walk)*1.25;
        var flyCost=f.WalkPower.Value/60*flyFactor*flyFactor*Math.Max(1,speed/6)*ratio;
        // Fly altitude error clamps to10; cadence factor clamps to6 before
        // optional inverse-sqrt. Thus force<=6000+global gravity in any allowed
        // target-altitude branch. Drift's separate Native bound is also charged.
        var flyThrust=f.ThrustPowerPerAcceleration!.Value*ratio/60*(6000+f.GravityUpper!.Value)
            *Math.Max(1,Math.Abs(f.VerticalSpeed!.Value)/12);
        var drift=VoyagerLocalWalkReservePolicy.CalculatePotentialDriftPowerUpperBound(
            f.ThrustPowerPerAcceleration,f.GravityUpper,f.ConsumptionRatio);
        if(!drift.HasValue)return null;
        var total=walkCost+flyCost+flyThrust+drift.Value/60+f.OtherWorkPower!.Value*ratio/60
            +f.IndependentEnergyPerTick!.Value+f.PotentialJumpEnergy!.Value*ratio;
        return double.IsNaN(total)||double.IsInfinity(total)||total<=0?null:total*(1+1e-8)+1;
    }
}
