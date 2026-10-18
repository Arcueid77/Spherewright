using Spherewright.Contracts.Celestial;

namespace Spherewright.Bridge.Core.Safety;

public enum VoyagerWarpPathEnvelope
{
    Unknown,
    Clear,
    Blocked,
}

// One instance belongs to one planned entry. Install NextState before any native effect;
// never replace it with Ready to retry the same entry, including an unknown dispatch.
public sealed class VoyagerWarpControlState
{
    private VoyagerWarpControlState(bool entryAttempted, bool stopLatched)
    {
        EntryAttempted = entryAttempted;
        StopLatched = stopLatched;
    }

    public static VoyagerWarpControlState Ready { get; } = new VoyagerWarpControlState(false, false);
    public bool EntryAttempted { get; }
    public bool StopLatched { get; }
    internal VoyagerWarpControlState ClaimEntry() => new VoyagerWarpControlState(true, StopLatched);
    internal VoyagerWarpControlState LatchStop() => new VoyagerWarpControlState(EntryAttempted, true);
}

// Copied, fresh facts only. A clear envelope must be proved using lawful native
// observations; generated hidden planet/resource data cannot prove it clear.
public sealed class VoyagerWarpEntryFacts
{
    public bool OwnedBindingVerified { get; set; }
    public bool ExclusiveBindingVerified { get; set; }
    public bool FactsFresh { get; set; }
    public bool StopRequested { get; set; }
    public bool IsAlive { get; set; }
    public bool IsSailing { get; set; }
    public bool ControllerEnabled { get; set; }
    public bool NativeGuideRunning { get; set; }
    public int LocalPlanetId { get; set; }
    public int ThrusterLevel { get; set; }
    public bool WarpCommand { get; set; }
    public float WarpState { get; set; }
    public double CoreEnergyJoules { get; set; }
    public double NativeStartThresholdJoules { get; set; }
    public int NativeWarperCount { get; set; }
    // CurrentLegRemainingEntries includes the entry being evaluated. Return entries
    // and its energy floor are disjoint from the current leg, not double-counted.
    public int CurrentLegRemainingEntries { get; set; }
    public int ReturnRemainingEntries { get; set; }
    public int ExtraWarperReserve { get; set; }
    public bool RemainingBudgetLocked { get; set; }
    public double AvailableEnergyJoules { get; set; }
    public double LockedRemainingLegEnergyJoules { get; set; }
    public double ReturnEnergyFloorJoules { get; set; }
    public double HeadingErrorDegrees { get; set; }
    public bool TargetNativelyVisible { get; set; }
    public bool PathEnvelopeLegallyObservable { get; set; }
    public VoyagerWarpPathEnvelope PathEnvelope { get; set; }
    public double ClearPathDistanceMeters { get; set; }
    public double MaxWarpSpeed { get; set; }
    // Must bound ordinary speed throughout exit, not just sample a slower instant.
    public double OrdinarySpeedUpperBound { get; set; }
    public int ControlLatencyGameTicks { get; set; } = VoyagerWarpControlPolicy.DefaultControlLatencyGameTicks;
}

public sealed class VoyagerWarpExitDistanceBound
{
    public bool Computed { get; internal set; }
    public int ControlLatencyGameTicks { get; internal set; }
    public int NativeDecayGameTicks { get; internal set; }
    public double WarpDistanceUpperBoundMeters { get; internal set; }
    public double OrdinaryCoastUpperBoundMeters { get; internal set; }
    public double DistanceUpperBoundMeters { get; internal set; }
    public string? BlockerCode { get; internal set; }
}

public sealed class VoyagerWarpEntryDecision
{
    public bool Allowed { get; internal set; }
    public VoyagerWarpControlState NextState { get; internal set; } = VoyagerWarpControlState.Ready;
    public int? RequiredWarpers { get; internal set; }
    public double? RequiredEnergyJoules { get; internal set; }
    public VoyagerWarpExitDistanceBound ExitBound { get; internal set; } = new VoyagerWarpExitDistanceBound();
    public List<VoyagerRouteBlocker> Blockers { get; } = new List<VoyagerRouteBlocker>();
}

public sealed class VoyagerWarpStopDecision
{
    public VoyagerWarpControlState NextState { get; internal set; } = VoyagerWarpControlState.Ready;
    public bool ClearOwnedWarpCommand { get; internal set; }
    public bool NativeExitObserved { get; internal set; }
    public bool NormalBrakingRequired { get; internal set; }
    // This settles only the stop observation, not landing, checkpoint retirement or flight.
    public bool StopSettled { get; internal set; }
    public bool FlightSucceeded => false;
    public List<VoyagerRouteBlocker> Blockers { get; } = new List<VoyagerRouteBlocker>();
}

public static class VoyagerWarpControlPolicy
{
    // DSP29151 PlayerController.UpdateEnvironment: false command decays native float w before Sail.GameTick.
    public const float NativeWarpStateDecayPerTick = 0.06667667f;
    public const int MaximumNativeDecayGameTicks = 15;
    public const int DefaultControlLatencyGameTicks = 2;
    public const int MaximumControlLatencyGameTicks = 120;
    public const double MaximumHeadingErrorDegrees = 0.25d;
    public const double StoppedOrdinarySpeedMetersPerSecond = 0.1d;

    public static VoyagerWarpEntryDecision EvaluateEntry(VoyagerWarpControlState state, VoyagerWarpEntryFacts facts)
    {
        if (state is null) throw new ArgumentNullException(nameof(state));
        if (facts is null) throw new ArgumentNullException(nameof(facts));
        var next = facts.StopRequested || !facts.OwnedBindingVerified ? state.LatchStop() : state;
        var result = new VoyagerWarpEntryDecision { NextState = next };
        void Block(string code, string message) => result.Blockers.Add(new VoyagerRouteBlocker { Code = code, Message = message });
        if (!facts.OwnedBindingVerified) Block("owned_binding_required", "The original owned session, world, player and controller must still match.");
        if (!facts.ExclusiveBindingVerified) Block("exclusive_movement_binding_required", "The exact action must exclusively own its native movement/warp channel.");
        if (!facts.FactsFresh) Block("fresh_entry_facts_required", "Native facts and the proved path envelope must be fresh for this entry.");
        if (next.StopLatched) Block("warp_stop_latched", "Cancellation or binding loss is sticky for this planned entry.");
        if (state.EntryAttempted) Block("warp_entry_already_attempted", "Reconcile the original entry; do not consume another warper or retry an unknown dispatch.");
        if (!facts.IsAlive || !facts.IsSailing || !facts.ControllerEnabled || facts.NativeGuideRunning || facts.LocalPlanetId != 0)
            Block("native_sailing_entry_conditions_required", "Entry requires an alive sailing player, enabled controller, no guide and no local planet.");
        if (facts.ThrusterLevel < 3) Block("warp_technology_required", "Native thrusterLevel must be at least 3.");
        if (!IsWarpState(facts.WarpState) || facts.WarpCommand || facts.WarpState != 0f)
            Block("native_warp_exit_required_before_entry", "A prior warp command or nonzero/invalid warp state cannot be re-entered.");
        if (!FiniteNonnegative(facts.CoreEnergyJoules) || !FiniteNonnegative(facts.NativeStartThresholdJoules))
            Block("invalid_native_entry_energy", "Core energy and the native start threshold must be finite and nonnegative.");
        else if (facts.CoreEnergyJoules <= facts.NativeStartThresholdJoules)
            Block("native_warp_start_energy_insufficient", "Current core energy must be strictly greater than the unscaled native start threshold.");

        var warpers = (long)facts.CurrentLegRemainingEntries + facts.ReturnRemainingEntries + facts.ExtraWarperReserve;
        if (facts.NativeWarperCount < 0 || facts.CurrentLegRemainingEntries < 1 || facts.ReturnRemainingEntries < 0
            || facts.ExtraWarperReserve < 0 || facts.ExtraWarperReserve > VoyagerRouteBudgetPolicy.MaximumExtraWarperReserve || warpers > int.MaxValue)
            Block("invalid_remaining_warper_reserve", "Real native counts and all remaining entry reservations must be valid; the current entry is included.");
        else
        {
            result.RequiredWarpers = (int)warpers;
            if (facts.NativeWarperCount < warpers)
                Block("warper_return_reserve_insufficient", "Real native warpers must cover this leg, remaining return entries and declared extras; package items are not reserved here.");
        }
        var requiredEnergy = facts.LockedRemainingLegEnergyJoules + facts.ReturnEnergyFloorJoules;
        if (!facts.RemainingBudgetLocked || !FiniteNonnegative(facts.AvailableEnergyJoules)
            || !FinitePositive(facts.LockedRemainingLegEnergyJoules) || !FiniteNonnegative(facts.ReturnEnergyFloorJoules)
            || (facts.ReturnRemainingEntries > 0 && facts.ReturnEnergyFloorJoules <= 0d) || !IsFinite(requiredEnergy))
            Block("invalid_locked_remaining_energy", "A finite locked current-leg budget and protected return floor are required; no partial budget is usable.");
        else
        {
            result.RequiredEnergyJoules = requiredEnergy;
            if (facts.AvailableEnergyJoules < requiredEnergy)
                Block("fuel_return_reserve_insufficient", "Available loaded energy must still cover the locked remaining leg plus the protected return floor.");
        }
        if (!FiniteNonnegative(facts.HeadingErrorDegrees) || facts.HeadingErrorDegrees > MaximumHeadingErrorDegrees)
            Block("warp_heading_not_aligned", "Heading error must be finite and no greater than 0.25 degrees.");
        if (!facts.TargetNativelyVisible || !facts.PathEnvelopeLegallyObservable || facts.PathEnvelope != VoyagerWarpPathEnvelope.Clear)
            Block("lawful_clear_path_envelope_required", "A hidden, unknown or blocked path cannot be represented as a proved clear envelope.");
        // An entry can reach full warp later: clearance is never based on its initial zero state.
        result.ExitBound = CalculateExitDistanceUpperBound(1f, facts.MaxWarpSpeed, facts.OrdinarySpeedUpperBound, facts.ControlLatencyGameTicks);
        if (!result.ExitBound.Computed)
            Block(result.ExitBound.BlockerCode!, "No finite native warp-exit distance bound could be established.");
        if (!FinitePositive(facts.ClearPathDistanceMeters) || (result.ExitBound.Computed && facts.ClearPathDistanceMeters <= result.ExitBound.DistanceUpperBoundMeters))
            Block("warp_exit_clearance_insufficient", "The fresh proved clear distance must be strictly greater than the full-warp exit bound including declared latency.");
        result.Allowed = result.Blockers.Count == 0;
        if (result.Allowed) result.NextState = next.ClaimEntry();
        return result;
    }

    public static VoyagerWarpStopDecision EvaluateStop(VoyagerWarpControlState state, bool ownedBinding,
        bool stopRequested, bool warpCommand, float warpState, double ordinarySpeed)
    {
        if (state is null) throw new ArgumentNullException(nameof(state));
        var next = stopRequested || !ownedBinding || !IsWarpState(warpState) || !FiniteNonnegative(ordinarySpeed) ? state.LatchStop() : state;
        var result = new VoyagerWarpStopDecision { NextState = next };
        void Block(string code, string message) => result.Blockers.Add(new VoyagerRouteBlocker { Code = code, Message = message });
        if (!ownedBinding) Block("owned_binding_required", "Do not release another session, player, controller or action's command.");
        // EntryAttempted is a reservation, not proof of a successful native entry.
        // It is retained on unknown outcome and grants no new entry or item refund.
        result.ClearOwnedWarpCommand = next.StopLatched && ownedBinding && state.EntryAttempted && warpCommand;
        if (next.StopLatched && ownedBinding && warpCommand && !state.EntryAttempted)
            Block("warp_command_not_owned", "No command was reserved by this entry; do not clear an external warp command.");
        if (!IsWarpState(warpState)) Block("invalid_native_warp_state", "The copied native warp state must be finite and within zero through one.");
        if (!FiniteNonnegative(ordinarySpeed)) Block("invalid_ordinary_speed", "Ordinary speed must be finite and nonnegative; unknown is not stopped.");
        result.NativeExitObserved = ownedBinding && IsWarpState(warpState) && !warpCommand && warpState == 0f;
        result.NormalBrakingRequired = next.StopLatched && ownedBinding && FiniteNonnegative(ordinarySpeed) && ordinarySpeed > StoppedOrdinarySpeedMetersPerSecond;
        if (next.StopLatched && !result.NativeExitObserved)
            Block("native_warp_exit_not_observed", "Clearing a command or stopping the Host is not evidence of a completed native exit.");
        if (result.NormalBrakingRequired)
            Block("normal_braking_still_required", "Warp exit does not stop ordinary velocity; continue only the bounded normal braking/withdrawal path.");
        result.StopSettled = next.StopLatched && result.NativeExitObserved && FiniteNonnegative(ordinarySpeed) && ordinarySpeed <= StoppedOrdinarySpeedMetersPerSecond;
        return result;
    }

    public static VoyagerWarpExitDistanceBound CalculateExitDistanceUpperBound(float warpState,
        double maxWarpSpeed, double ordinarySpeedUpperBound, int controlLatencyGameTicks = DefaultControlLatencyGameTicks)
    {
        var result = new VoyagerWarpExitDistanceBound();
        if (!IsWarpState(warpState) || !FinitePositive(maxWarpSpeed) || !FiniteNonnegative(ordinarySpeedUpperBound)
            || controlLatencyGameTicks < 0 || controlLatencyGameTicks > MaximumControlLatencyGameTicks)
        {
            result.BlockerCode = "invalid_warp_exit_inputs";
            return result;
        }
        // During declared pre-clear latency a still-true command can increase native w.
        // Full speed over those ticks safely bounds attenuation and steering changes.
        var w = warpState;
        for (var tick = 0; tick < controlLatencyGameTicks; tick++)
            w = Math.Min(1f, w + VoyagerRouteBudgetPolicy.NativeWarpStateRisePerTick);
        var warpDistance = maxWarpSpeed * (controlLatencyGameTicks / VoyagerRouteBudgetPolicy.TicksPerSecond);
        var decayTicks = 0;
        while (w > 0f && decayTicks < MaximumNativeDecayGameTicks)
        {
            // Match native float subtraction before this tick's Sail speed calculation.
            w -= NativeWarpStateDecayPerTick;
            if (w <= 0f) w = 0f;
            warpDistance += maxWarpSpeed * ((Math.Pow(1001d, w) - 1d) / 1000d) / VoyagerRouteBudgetPolicy.TicksPerSecond;
            decayTicks++;
        }
        var ordinaryDistance = ordinarySpeedUpperBound * ((controlLatencyGameTicks + decayTicks) / VoyagerRouteBudgetPolicy.TicksPerSecond);
        var distance = warpDistance + ordinaryDistance;
        // Leave a small numerical margin; clearance itself remains a strict comparison.
        var roundedDistance = distance == 0d ? 0d : distance + Math.Max(1e-6d, distance * 1e-12d);
        if (w != 0f || !FiniteNonnegative(warpDistance) || !FiniteNonnegative(ordinaryDistance) || !FiniteNonnegative(roundedDistance))
        {
            result.BlockerCode = "warp_exit_bound_overflow";
            return result;
        }
        result.Computed = true;
        result.ControlLatencyGameTicks = controlLatencyGameTicks;
        result.NativeDecayGameTicks = decayTicks;
        result.WarpDistanceUpperBoundMeters = warpDistance;
        result.OrdinaryCoastUpperBoundMeters = ordinaryDistance;
        result.DistanceUpperBoundMeters = roundedDistance;
        return result;
    }

    private static bool IsWarpState(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
    private static bool FiniteNonnegative(double value) => IsFinite(value) && value >= 0d;
    private static bool FinitePositive(double value) => IsFinite(value) && value > 0d;
    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
