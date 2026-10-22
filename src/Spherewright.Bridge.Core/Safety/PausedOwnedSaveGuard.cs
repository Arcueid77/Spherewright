using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Safety;

// Serialization permission only. None of these observations certifies ground,
// physical idle, available return time, a successful action or permission to Resume.
public sealed class PausedOwnedSaveObservation
{
    public object? World { get; set; }
    public string? SessionId { get; set; }
    public string? OwnedSaveName { get; set; }
    public string? NativeVersion { get; set; }
    public int PlanetId { get; set; }
    public long Revision { get; set; }
    public long GameTick { get; set; }
    public int RenderFrame { get; set; }
    public bool? NativePaused { get; set; }
    public string? QuarantineActionId { get; set; }
    public string? QuarantineReason { get; set; }
    public string? Blocker { get; set; }
    public string? FirstFailedPredicate { get; set; }
    public DateTimeOffset? CapturedAtUtc { get; set; }
    public Dictionary<string, string> Predicates { get; set; } = new();

    internal PausedOwnedSaveObservation Copy()
    { var copy = (PausedOwnedSaveObservation)MemberwiseClone(); copy.Predicates = new(Predicates); return copy; }
    internal string Fingerprint => CanonicalStateHash.Combine("paused-owned-save-v1", SessionId,
        OwnedSaveName, NativeVersion, PlanetId, Revision, GameTick, QuarantineActionId, QuarantineReason);
}

public sealed class PausedOwnedSaveBinding
{
    private NativeResumeFailureSnapshot? _failure;
    internal PausedOwnedSaveBinding(PausedOwnedSaveObservation observation) => Observation = observation.Copy();
    internal PausedOwnedSaveObservation Observation { get; }
    public long GameTick => Observation.GameTick;
    public string Fingerprint => Observation.Fingerprint;
    public NativeResumeFailureSnapshot? FirstFailure => _failure?.Copy();
    internal void RetainFailure(NativeResumeFailureSnapshot failure) => _failure ??= failure.Copy();

    public void RecordNativeSaveFailure(NativeResumeFailureSnapshot cut) => RetainFailure(cut);

    // Only the one exact quarantine instance is a serialization exception.
    // A Journey-load error can have the same code and must remain a blocker.
    public IReadOnlyList<WriteBlocker> RemainingBlockers(IReadOnlyList<WriteBlocker> blockers)
    {
        var matches = blockers.Count(b => b.Code == BridgeErrorCodes.WriteSubsystemQuarantined
            && b.Message == Observation.QuarantineReason);
        return matches == 1 ? blockers.Where(b => b.Code != BridgeErrorCodes.WriteSubsystemQuarantined
            || b.Message != Observation.QuarantineReason).ToArray() : blockers.ToArray();
    }
}

public sealed class PausedOwnedSaveGuard
{
    private PausedOwnedSaveObservation? _previous;
    private PausedOwnedSaveBinding? _stable;
    private NativeResumeFailureSnapshot? _lastFailure;
    public NativeResumeFailureSnapshot? LastFailure => _lastFailure?.Copy();

    public void Observe(PausedOwnedSaveObservation current)
    {
        if (Blocker(current) is not null) { Reset(); return; }
        if (_previous is { } previous)
        {
            if (current.RenderFrame < previous.RenderFrame) { Reset(); return; }
            if (current.RenderFrame == previous.RenderFrame) return;
            _stable = Matches(previous, current) ? _stable ?? new PausedOwnedSaveBinding(current) : null;
        }
        _previous = current.Copy();
    }

    public bool TryBind(PausedOwnedSaveObservation current, out PausedOwnedSaveBinding? binding, out string rejection)
    {
        binding = null;
        rejection = Blocker(current) ?? (_stable is null ? "paused_save_tail_not_observed" :
            !Matches(_stable.Observation, current) ? "paused_save_binding_changed" :
            current.RenderFrame < _stable.Observation.RenderFrame ? "paused_save_observation_out_of_order" : "");
        if (rejection.Length != 0 && rejection != "paused_save_tail_not_observed") Reset();
        if (rejection.Length != 0)
        {
            _lastFailure = Failure(current, BridgeErrorCodes.StaleState,
                current.FirstFailedPredicate ?? rejection, "paused_owned_save_prepare_or_commit");
            return false;
        }
        _lastFailure = null;
        binding = _stable;
        return true;
    }

    public bool Validate(PausedOwnedSaveBinding binding, PausedOwnedSaveObservation current, out string rejection)
    {
        if (binding.FirstFailure is { } original)
        { rejection = original.FirstFailedPredicate; return false; }
        if (!TryBind(current, out var active, out rejection))
        { binding.RetainFailure(_lastFailure!); return false; }
        if (!ReferenceEquals(binding, active))
        {
            rejection = "paused_save_prepared_binding_expired";
            binding.RetainFailure(Failure(current, BridgeErrorCodes.StaleState, rejection, "paused_owned_save_commit"));
            return false;
        }
        return true;
    }

    private void Reset() { _previous = null; _stable = null; }
    internal static NativeResumeFailureSnapshot Failure(PausedOwnedSaveObservation observation, string code,
        string predicate, string stage) => new()
    {
        Code = code, FirstFailedPredicate = predicate, Stage = stage, FactsState = "observed",
        CapturedAtGameTick = observation.GameTick, CapturedAtUtc = observation.CapturedAtUtc,
        Predicates = new(observation.Predicates) { [predicate] = observation.Predicates.TryGetValue(predicate, out var state)
            ? state : predicate == "paused_save_pause_not_observed" || predicate == "paused_save_tail_not_observed" ? "unknown" : "false",
            ["native_save"] = "not_reached" },
        Observations = new() { ["native_pause"] = observation.NativePaused.HasValue
            ? observation.NativePaused.Value ? "true" : "false" : "unknown", ["ground"] = "not_observed", ["physical_idle"] = "not_observed" },
        Values = new() { ["observed_game_tick"] = observation.GameTick, ["render_frame"] = observation.RenderFrame,
            ["native_ticks_requested"] = 0, ["original_journey_remaining_ticks"] = null },
    };
    private static bool Matches(PausedOwnedSaveObservation a, PausedOwnedSaveObservation b) =>
        ReferenceEquals(a.World, b.World) && a.Fingerprint == b.Fingerprint;
    private static string? Blocker(PausedOwnedSaveObservation o) => o.Blocker
        ?? (o.NativePaused is null ? "paused_save_pause_not_observed" : o.NativePaused != true ? "paused_save_native_not_paused" :
            o.World is null || string.IsNullOrWhiteSpace(o.SessionId) || string.IsNullOrWhiteSpace(o.OwnedSaveName)
            || string.IsNullOrWhiteSpace(o.NativeVersion) || o.PlanetId <= 0 || o.GameTick < 0 || o.RenderFrame < 0
            || string.IsNullOrWhiteSpace(o.QuarantineActionId) || string.IsNullOrWhiteSpace(o.QuarantineReason)
                ? "paused_save_identity_or_quarantine_unproved" : null);
}
