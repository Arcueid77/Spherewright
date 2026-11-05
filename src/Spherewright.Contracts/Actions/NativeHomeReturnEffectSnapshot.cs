namespace Spherewright.Contracts.Actions;

/// <summary>First paid Native home-return control or Player601 effect, not terminal landing.</summary>
public sealed class NativeHomeReturnEffectSnapshot
{
    public string EvidenceScope {get;set;}="synchronous_paid_native_home_return_control";
    public bool NativeBindingVerified {get;set;}
    public bool PaidControlObserved {get;set;}
    public long CapturedAtGameTick {get;set;}
    public double CoreBefore {get;set;}
    public double CoreAfter {get;set;}
    public double NativeThrustDebitJoules {get;set;}
    public double PositionBeforeX {get;set;}
    public double PositionBeforeY {get;set;}
    public double PositionBeforeZ {get;set;}
    public double PositionAfterX {get;set;}
    public double PositionAfterY {get;set;}
    public double PositionAfterZ {get;set;}
    public double VelocityBeforeX {get;set;}
    public double VelocityBeforeY {get;set;}
    public double VelocityBeforeZ {get;set;}
    public double VelocityAfterX {get;set;}
    public double VelocityAfterY {get;set;}
    public double VelocityAfterZ {get;set;}
}
