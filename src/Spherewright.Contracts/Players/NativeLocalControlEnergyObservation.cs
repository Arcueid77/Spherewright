namespace Spherewright.Contracts.Players;

// Conditional numeric cost, never permission to unpause, load or execute.
public sealed class NativeLocalControlEnergyObservation
{
    public string Basis { get; set; } = "conditional_native_walk_drift_tick_upper_v1";
    public double? PerNativeTickEnergyUpperJoules { get; set; }
    public bool CostInputsProved { get; set; }
    public bool IndependentDebitsExcluded { get; set; }
    public bool ExecutionPremisesProved => false;
    public List<string> Blockers { get; set; } = new();
}
