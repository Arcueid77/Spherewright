namespace Spherewright.Bridge.Core.Safety;

// Fresh copied native facts only. Missing values cannot prove spare energy. The
// owner supplies loaded energy and the original journey's protected return floor;
// this policy never replenishes, spends, lowers or re-locks either quantity.
public sealed class VoyagerControlReserveFacts
{
    public double? AvailableLoadedEnergyJoules { get; set; }
    public double? ProtectedReturnFloorJoules { get; set; }
    public double? ThrustPowerPerAcceleration { get; set; }
    public double? EnergyConsumptionCoefficient { get; set; }
    public double? NativeEnergyScale { get; set; }
    public double? OrdinarySpeedUpperBound { get; set; }
    public double? WarpStartPowerPerSpeed { get; set; }
    public double? WarpKeepingPowerPerSpeed { get; set; }
    public double? MaxWarpSpeed { get; set; }
    public bool? HasNativeWarpTail { get; set; }
    // Includes copied walk/mining/replication/research power and its coefficient.
    public double? ExtraMechaPowerUpperBoundWatts { get; set; }
}

public sealed class VoyagerControlReserveDecision
{
    public bool Proved { get; internal set; }
    // All three remain null unless the complete finite reserve was calculated.
    public double? RequiredMarginJoules { get; internal set; }
    public double? NativeExitEnergyUpperJoules { get; internal set; }
    public double? NextControlAndBrakingEnergyUpperJoules { get; internal set; }
    public string? BlockerCode { get; internal set; }
}

public static class VoyagerControlReservePolicy
{
    public const double NativeSailEnergyFactor = 3d;
    public const int ExtraMechaWorkReserveSeconds = VoyagerControlDeadlinePolicy.StopCleanupSeconds;
    public const int NativeExitReserveGameTicks = VoyagerControlDeadlinePolicy.WarpExitLeadGameTicks;

    // This authorizes neither native control nor a declaration of stopped/landed.
    // The owner must evaluate fresh facts before each control and gate each actual
    // paid brake against the same protected floor and remaining native exit tail.
    public static VoyagerControlReserveDecision Evaluate(VoyagerControlReserveFacts facts)
    {
        if (facts is null) throw new ArgumentNullException(nameof(facts));
        if (!facts.AvailableLoadedEnergyJoules.HasValue || !facts.ProtectedReturnFloorJoules.HasValue
            || !facts.ThrustPowerPerAcceleration.HasValue || !facts.EnergyConsumptionCoefficient.HasValue
            || !facts.NativeEnergyScale.HasValue || !facts.OrdinarySpeedUpperBound.HasValue
            || !facts.WarpStartPowerPerSpeed.HasValue || !facts.WarpKeepingPowerPerSpeed.HasValue
            || !facts.MaxWarpSpeed.HasValue || !facts.HasNativeWarpTail.HasValue
            || !facts.ExtraMechaPowerUpperBoundWatts.HasValue)
            return Blocked("voyager_control_reserve_facts_unavailable");

        var loaded = facts.AvailableLoadedEnergyJoules.Value;
        var floor = facts.ProtectedReturnFloorJoules.Value;
        var thrust = facts.ThrustPowerPerAcceleration.Value;
        var consumption = facts.EnergyConsumptionCoefficient.Value;
        var nativeScale = facts.NativeEnergyScale.Value;
        var speed = facts.OrdinarySpeedUpperBound.Value;
        var warpStart = facts.WarpStartPowerPerSpeed.Value;
        var warpKeeping = facts.WarpKeepingPowerPerSpeed.Value;
        var warpSpeed = facts.MaxWarpSpeed.Value;
        var extraPower = facts.ExtraMechaPowerUpperBoundWatts.Value;
        if (!FiniteNonnegative(loaded) || !FiniteNonnegative(floor) || !FiniteNonnegative(thrust)
            || !FiniteNonnegative(consumption) || !FiniteNonnegative(nativeScale) || !FiniteNonnegative(speed)
            || !FiniteNonnegative(warpStart) || !FiniteNonnegative(warpKeeping) || !FiniteNonnegative(warpSpeed)
            || !FiniteNonnegative(extraPower))
            return Blocked("voyager_control_reserve_facts_invalid");

        var scale = NativeSailEnergyFactor * thrust * consumption * Math.Max(1d, nativeScale);
        var nextAndBrake = scale * (2d * speed * 0.36d + 20d + speed * 1.5d);
        var nativeExit = facts.HasNativeWarpTail.Value
            ? (warpStart + warpKeeping) * warpSpeed * consumption * nativeScale * NativeExitReserveGameTicks / 60d
            : 0d;
        var extraWork = extraPower * ExtraMechaWorkReserveSeconds;
        var controlAndWork = nextAndBrake + extraWork;
        var margin = controlAndWork + nativeExit;
        var required = floor + margin;
        if (!FiniteNonnegative(scale) || !FiniteNonnegative(nextAndBrake) || !FiniteNonnegative(nativeExit)
            || !FiniteNonnegative(extraWork) || !FiniteNonnegative(controlAndWork)
            || !FiniteNonnegative(margin) || !FiniteNonnegative(required))
            return Blocked("voyager_control_reserve_overflow");

        var proved = CanSpendBeforeFloor(loaded, floor, controlAndWork, nativeExit);
        return new VoyagerControlReserveDecision
        {
            Proved = proved,
            RequiredMarginJoules = margin,
            NativeExitEnergyUpperJoules = nativeExit,
            NextControlAndBrakingEnergyUpperJoules = nextAndBrake,
            BlockerCode = proved ? null : "voyager_control_return_floor_insufficient",
        };
    }

    public static bool CanSpendBeforeFloor(double loaded, double floor, double exactPaidCost, double remainingNativeExitEnergy)
    {
        if (!FiniteNonnegative(loaded) || !FiniteNonnegative(floor)
            || !FiniteNonnegative(exactPaidCost) || !FiniteNonnegative(remainingNativeExitEnergy)) return false;
        var margin = exactPaidCost + remainingNativeExitEnergy;
        var required = floor + margin;
        if (!FiniteNonnegative(margin) || !FiniteNonnegative(required) || loaded < floor) return false;
        // The residual comparison also rejects a positive cost swallowed by a
        // large floor's floating-point addition: loaded == floor is not spare.
        return loaded >= required && loaded - floor >= margin;
    }

    private static VoyagerControlReserveDecision Blocked(string code) => new VoyagerControlReserveDecision { BlockerCode = code };
    private static bool FiniteNonnegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
}
