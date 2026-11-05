namespace Spherewright.Bridge.Core.Safety;

/// <summary>One paid approach controller invocation plus its following Native Sail601.
/// This is not the loaded-fuel full-braking/30-second cleanup reserve.</summary>
public sealed class NativeSailTickEnergyFacts
{
    public bool NativeSailBranchBound {get;set;}
    public bool ManualInputAndNavigationExcluded {get;set;}
    public bool NativeInputAffinitiesBound {get;set;}
    public double? ThrustPowerPerAcceleration {get;set;}
    public double? ConsumptionRatio {get;set;}
    public double? NativeEnergyScale {get;set;}
    public double? RelativeHomeSpeed {get;set;}
    public double? HomeToLocalBodyVelocity {get;set;}
    public double? MaximumSailSpeed {get;set;}
    public string BodyKind {get;set;}=string.Empty; // none, planet, star
    public double? BodyDistance {get;set;}
    public double? BodyRadius {get;set;}
    public double? GravityUpper {get;set;}
    public double? IndependentEnergyPerTick {get;set;}
}
public sealed class NativeSailTickEnergyDecision
{
    public bool Proved {get;internal set;}
    public string? Blocker {get;internal set;}
    public double? PerNativeTickJoules {get;internal set;}
    public double? ApproachControlJoules {get;internal set;}
    public double? NativeSailJoules {get;internal set;}
}
public static class NativeSailTickEnergyPolicy
{
    public static NativeSailTickEnergyDecision Evaluate(NativeSailTickEnergyFacts f)
    {
        NativeSailTickEnergyDecision Fail(string code)=>new(){Blocker=code};
        bool Known(double? v)=>v.HasValue && v>=0 && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value);
        if(!f.NativeSailBranchBound || !f.ManualInputAndNavigationExcluded || !f.NativeInputAffinitiesBound)
            return Fail("native_sail_tick_branch_unproved");
        if(new[]{f.ThrustPowerPerAcceleration,f.ConsumptionRatio,f.NativeEnergyScale,f.RelativeHomeSpeed,
            f.HomeToLocalBodyVelocity,f.MaximumSailSpeed,f.GravityUpper,f.IndependentEnergyPerTick}.Any(v=>!Known(v))
            || f.ConsumptionRatio<=0 || f.MaximumSailSpeed<=0 || f.MaximumSailSpeed>1e7 || f.RelativeHomeSpeed>1e7
            || f.HomeToLocalBodyVelocity>1e7)return Fail("native_sail_tick_inputs_unproved");
        if(f.BodyKind is not ("none" or "planet" or "star"))return Fail("native_sail_tick_body_unproved");
        var v=f.RelativeHomeSpeed!.Value;var scale=3*f.ThrustPowerPerAcceleration!.Value*f.ConsumptionRatio!.Value*Math.Max(1,f.NativeEnergyScale!.Value);
        // Published approach uses Slerp then either .008 braking or acceleration
        // max(7,min(max_acc,max(1,v)*.02)). Charge both mutually exclusive costs.
        var acceleration=Math.Max(7,Math.Max(1,v)*.02);
        var approach=scale*(2*v*.36+v*.008*1.5+acceleration);
        // Float Slerp endpoints have equal relative norm. Relative1e-5+1m/s
        // widens their conversion/interpolation before Native601 adds body motion.
        var nextSpeed=v*(1+1e-5)+1+acceleration+f.HomeToLocalBodyVelocity!.Value;
        double sail=0;
        if(f.BodyKind!="none")
        {
            if(!Known(f.BodyDistance) || !Known(f.BodyRadius) || f.BodyDistance<=0 || f.BodyRadius<=0)
                return Fail("native_sail_tick_geometry_unproved");
            var r=f.BodyDistance!.Value;var radius=f.BodyRadius!.Value;var g=f.GravityUpper!.Value;
            var height=r-radius;var target=f.BodyKind=="planet"?60d:300d;
            var altitudeGain=f.BodyKind=="planet"?.5d:.1d;
            var orbitalFactor=f.BodyKind=="planet"?.7d:2d;
            // Native input_aff values are clamped before this branch; a<=.08,
            // b<=1. Widen radial/tangent projections and the complete centrifugal
            // term. Star branch may be inactive; charging it still bounds601.
            var atmosphereDelta=.08*(2*nextSpeed+Math.Abs(height-target)*altitudeGain+Math.Sqrt(g*r*orbitalFactor))
                +nextSpeed*nextSpeed/r/60d;
            sail=scale*(atmosphereDelta*1.5+g/60d);
        }
        var total=approach+sail+f.IndependentEnergyPerTick!.Value;
        if(double.IsInfinity(total) || double.IsNaN(total) || total<=0)return Fail("native_sail_tick_arithmetic_unproved");
        // Explicit arithmetic widening, not a zero-cost or average-power estimate.
        total=total*(1+1e-8)+1;
        return new(){Proved=true,PerNativeTickJoules=total,ApproachControlJoules=approach,NativeSailJoules=sail};
    }
}
