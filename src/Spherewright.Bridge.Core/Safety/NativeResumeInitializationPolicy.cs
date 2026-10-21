namespace Spherewright.Bridge.Core.Safety;

public enum NativeResumeInitializationStep { Pending, Ready, Rejected }

// Copied main-thread observations. The caller retains the exact Native references;
// unchanged state includes pose, velocity, energy, input, orders, work and materials.
// Ordinary registration changes are observed separately through RegisteredCount.
public sealed class NativeResumeInitializationFacts
{
    public long CurrentGameTick { get; set; }
    public DateTimeOffset NowUtc { get; set; }
    public bool ExactSourceWorldBinding { get; set; }
    public bool NativeStateUnchanged { get; set; }
    public bool NativePaused { get; set; }
    public int? RegisteredCount { get; set; }
    public bool CurrentSupportOrGroundProof { get; set; }
}

// A paused, zero-Native-tick initialization hold, independent of the later
// contact policy's eight601/2s/14P limits. Ready never grants idle or a Save.
public sealed class NativeResumeInitializationPolicy
{
    public const string PendingCode = "remote_resume_native_initialization_pending";
    public const int MaximumHostPumpObservations = 8;
    public const double MaximumWallSeconds = 2d;
    public const int MaximumRegisteredColliders = 4096;
    public long SourceGameTick { get; }
    public DateTimeOffset StartedUtc { get; }
    public int ObservationCount { get; private set; }
    public bool ResumeAuthorized { get; private set; }
    public string? FirstFailure { get; private set; }
    private DateTimeOffset _lastObservationUtc;
    private bool _ready;

    public NativeResumeInitializationPolicy(long sourceGameTick, DateTimeOffset startedUtc)
    {
        if (sourceGameTick < 0 || startedUtc == default)
            throw new ArgumentOutOfRangeException(nameof(sourceGameTick));
        SourceGameTick = sourceGameTick;
        StartedUtc = _lastObservationUtc = startedUtc;
    }

    public NativeResumeInitializationStep Observe(NativeResumeInitializationFacts f)
    {
        if (FirstFailure is not null) return NativeResumeInitializationStep.Rejected;
        if (ResumeAuthorized) return Reject("remote_resume_initialization_resume_already_authorized");
        if (ObservationCount >= MaximumHostPumpObservations)
            return Reject("remote_resume_initialization_observations_exhausted");
        var blocker = Blocker(f);
        if (blocker is not null) return Reject(blocker);
        ObservationCount++;
        _lastObservationUtc = f.NowUtc;
        _ready = f.RegisteredCount > 0;
        if (_ready) return NativeResumeInitializationStep.Ready;
        return ObservationCount < MaximumHostPumpObservations ? NativeResumeInitializationStep.Pending
            : Reject("remote_resume_initialization_observations_exhausted");
    }

    // Recheck fresh facts, without charging another host observation. A positive
    // collection alone is insufficient. The Plugin must resume at most once and
    // immediately run its original full preflight before any Native601 can run.
    public bool TryAuthorizeResume(NativeResumeInitializationFacts freshFacts)
    {
        if (FirstFailure is not null) return false;
        if (ResumeAuthorized)
        { Reject("remote_resume_initialization_resume_already_authorized"); return false; }
        var blocker = Blocker(freshFacts);
        if (blocker is not null) { Reject(blocker); return false; }
        if (!_ready || ObservationCount == 0 || freshFacts.RegisteredCount <= 0
            || !freshFacts.CurrentSupportOrGroundProof)
        { Reject("remote_resume_initialization_current_support_unproved"); return false; }
        ResumeAuthorized = true;
        return true;
    }

    public void Cancel(string reason = "remote_resume_initialization_cancelled")
        => Reject(string.IsNullOrWhiteSpace(reason) ? "remote_resume_initialization_cancelled" : reason);

    private NativeResumeInitializationStep Reject(string reason)
    {
        FirstFailure ??= reason;
        return NativeResumeInitializationStep.Rejected;
    }

    private string? Blocker(NativeResumeInitializationFacts f)
    {
        if (f is null || !f.ExactSourceWorldBinding)
            return "remote_resume_initialization_binding_unproved";
        if (f.CurrentGameTick != SourceGameTick)
            return "remote_resume_initialization_tick_changed";
        if (!f.NativeStateUnchanged)
            return "remote_resume_initialization_state_changed";
        if (!f.NativePaused)
            return "remote_resume_initialization_pause_unproved";
        if (!f.RegisteredCount.HasValue || f.RegisteredCount < 0 || f.RegisteredCount > MaximumRegisteredColliders)
            return "remote_resume_initialization_registered_count_unproved";
        if (f.NowUtc == default || f.NowUtc < StartedUtc || f.NowUtc < _lastObservationUtc)
            return "remote_resume_initialization_clock_unproved";
        return (f.NowUtc - StartedUtc).TotalSeconds >= MaximumWallSeconds
            ? "remote_resume_initialization_wall_exhausted" : null;
    }
}
