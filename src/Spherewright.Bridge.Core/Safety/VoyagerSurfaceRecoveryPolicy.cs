using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Bridge.Core.Safety;

// An explicitly confirmed NEW native Move inside one owned resume task.
// The caller retains all Unity/source/order references. Copied facts never
// manufacture a ground hit, strict idle, energy, a Save or an old outcome.
public sealed class VoyagerSurfaceRecoveryFacts
{
    public long GameTick { get; set; }
    public DateTimeOffset NowUtc { get; set; }
    public bool ExactSourceWorldAndScene { get; set; }
    public bool NativeModesCostsAndOrderProved { get; set; }
    public string? NativeModeBlocker { get; set; }
    public bool CurrentTerrainSupportProved { get; set; }
    public bool DryLandingPatchProved { get; set; }
    public bool StrictGroundedWalkWithZeroTail { get; set; }
    public bool OriginalOrderAbsent { get; set; }
    public double DistanceFromSourceMeters { get; set; }
    public double DistanceToTargetMeters { get; set; }
    public VoyagerLocalActionBudgetFacts? OriginalJourney { get; set; }
    public double? CoreEnergyJoules { get; set; }
    public double? PerNativeTickEnergyJoules { get; set; }
    public double StopWallPatienceSeconds { get; set; } = 1d;
}

public enum VoyagerSurfaceRecoveryStep { Continue, Complete, Rejected }

public sealed class VoyagerSurfaceRecoveryPolicy
{
    public const int MaximumWorkGameTicks = 600;
    public const double MaximumWallSeconds = 20d;
    public const double MaximumInitialBindingWallSeconds = 2d;
    public const double MaximumTargetDistanceMeters = 16d;
    public const double MaximumDisplacementMeters = 24d;
    public const double MaximumArrivalDistanceMeters = 3.5d;
    public long SourceGameTick { get; }
    public DateTimeOffset StartedUtc { get; }
    public long LastPhysicsTick { get; private set; }
    public long? LastUniverseTick { get; private set; }
    public bool Completed { get; private set; }
    public string? Failure { get; private set; }
    private DateTimeOffset _lastUtc;

    public static string? ValidateRequest(PrepareOwnedWorldResumeRequest r)
    {
        var p = r.SurfaceRecoveryTarget;
        if (p is null) return null;
        return r.RecoveryMode == OwnedWorldResumeModes.AuthorizeRemotePrimaryRecovery
            && r.AdditionalVoyagerGameTicks == 0 && r.MaximumChargeGameTicks >= MaximumWorkGameTicks
            && Known(r.MaximumChargeWallSeconds) && r.MaximumChargeWallSeconds >= MaximumWallSeconds
            && Finite(p.X) && Finite(p.Y) && Finite(p.Z)
            && (double)p.X * p.X + (double)p.Y * p.Y + (double)p.Z * p.Z >= 1d
            ? null : "Surface recovery requires explicit zero-addition current-world authorization, one finite target and charge caps at least600 ticks/20 seconds; no default fallback or replay.";
    }

    public static Vector3Snapshot? CopyTarget(Vector3Snapshot? p) => p is null ? null
        : new Vector3Snapshot { X = p.X, Y = p.Y, Z = p.Z };

    public static bool HasMatchingEcho(PrepareOwnedWorldResumeRequest r, PreparedOwnedWorldResumePlan? p)
    {
        if (p is null) return false;
        var a = r.SurfaceRecoveryTarget; var b = p.SurfaceRecoveryTarget;
        if (a is null) return b is null && p.SurfaceRecoveryMaximumWorkGameTicks == 0 && p.SurfaceRecoveryMaximumWallSeconds == 0d;
        return ValidateRequest(r) is null && b is not null && a.X == b.X && a.Y == b.Y && a.Z == b.Z
            && p.RecoveryMode == r.RecoveryMode
            && p.RemotePrimaryRecoveryAuthorizationUsed && p.CompletedSavedLandingAdmissionUsed
            && p.SurfaceRecoveryMaximumWorkGameTicks == MaximumWorkGameTicks && p.SurfaceRecoveryMaximumWallSeconds == MaximumWallSeconds
            && p.AdditionalVoyagerGameTicks == 0 && p.UserConfirmationRequired && !string.IsNullOrEmpty(p.ConfirmationDigest);
    }

    public VoyagerSurfaceRecoveryPolicy(long sourceGameTick, DateTimeOffset startedUtc)
    {
        if (sourceGameTick < 0 || startedUtc == default) throw new ArgumentOutOfRangeException(nameof(sourceGameTick));
        SourceGameTick = LastPhysicsTick = sourceGameTick;
        StartedUtc = _lastUtc = startedUtc;
    }

    // Only the real Native151 callback may publish this prerequisite. It
    // grants no601, and a duplicate cannot renew a clock or physics budget.
    public string? ObserveUniverse(long tick, DateTimeOffset now, bool exactBinding)
    {
        if (Failure is not null) return Failure;
        if (Completed) return Reject("surface_recovery_already_completed");
        if (!exactBinding || !ClockOpen(now) || !InitialBindingOpen(now) || LastPhysicsTick == long.MaxValue || tick != LastPhysicsTick + 1
            || LastUniverseTick.HasValue && tick <= LastUniverseTick.Value)
            return Reject("surface_recovery_native151_binding_or_clock_unproved");
        LastUniverseTick = tick;
        _lastUtc = now;
        return null;
    }

    // Called once at the real501 BEFORE the next complete native601. The
    // same original journey must pay that whole tick and its13-tick tail.
    public VoyagerSurfaceRecoveryStep ObservePhysics(VoyagerSurfaceRecoveryFacts f)
    {
        if (Failure is not null) return VoyagerSurfaceRecoveryStep.Rejected;
        if (Completed) { Reject("surface_recovery_already_completed"); return VoyagerSurfaceRecoveryStep.Rejected; }
        string? blocker = f is null ? "surface_recovery_facts_missing"
            : !f.ExactSourceWorldAndScene || !f.NativeModesCostsAndOrderProved ? "surface_recovery_native_binding_unproved"
            : f.NativeModeBlocker is not null && f.NativeModeBlocker != "remote_idle_ground_contact_unproved"
                ? "surface_recovery_native_mode_unproved"
            : !f.CurrentTerrainSupportProved || !f.DryLandingPatchProved ? "surface_recovery_surface_unproved"
            : !ClockOpen(f.NowUtc) || !InitialBindingOpen(f.NowUtc) ? "surface_recovery_wall_exhausted"
            : LastPhysicsTick == long.MaxValue || f.GameTick != LastPhysicsTick + 1 || LastUniverseTick != f.GameTick
                ? "surface_recovery_native151_501_sequence_unproved"
            : (decimal)f.GameTick - SourceGameTick >= MaximumWorkGameTicks ? "surface_recovery_work_exhausted"
            : !Known(f.DistanceFromSourceMeters) || f.DistanceFromSourceMeters > MaximumDisplacementMeters
                || !Known(f.DistanceToTargetMeters) ? "surface_recovery_displacement_unproved"
            : !OriginalReserveAllows(f) ? "surface_recovery_original_reserve_unproved" : null;
        if (blocker is not null) { Reject(blocker); return VoyagerSurfaceRecoveryStep.Rejected; }
        LastPhysicsTick = f!.GameTick;
        _lastUtc = f.NowUtc;
        if (f.NativeModeBlocker is null && f.StrictGroundedWalkWithZeroTail && f.OriginalOrderAbsent && f.DistanceToTargetMeters <= MaximumArrivalDistanceMeters)
        { Completed = true; return VoyagerSurfaceRecoveryStep.Complete; }
        return VoyagerSurfaceRecoveryStep.Continue;
    }

    private bool ClockOpen(DateTimeOffset now) => now != default && now >= _lastUtc && now >= StartedUtc
        && (now - StartedUtc).TotalSeconds < MaximumWallSeconds;
    private bool InitialBindingOpen(DateTimeOffset now) => LastPhysicsTick != SourceGameTick
        || (now - StartedUtc).TotalSeconds < MaximumInitialBindingWallSeconds;
    public static bool OriginalReserveAllows(VoyagerSurfaceRecoveryFacts f)
    {
        var j = f.OriginalJourney;
        // This controller starts from Drift. It must not impersonate a
        // strictly admitted grounded-start Move or publish NativePlayerIdle.
        if (j is null || !j.ExactRemoteBinding || j.NativePlayerIdle || j.CurrentGameTick != f.GameTick
            || !j.LastLandedGameTick.HasValue || j.LastLandedGameTick < 0 || j.CurrentGameTick < j.LastLandedGameTick
            || j.SpentGameTicks < 0 || j.MaximumGameTicks < 1 || j.ReturnReserveGameTicks < 3600
            || j.ReturnReserveGameTicks > 108000 || j.WorkGameTicks != 1 || j.TailGameTicks != VoyagerLocalWalkReservePolicy.StopGameTicks
            || j.OriginalExtraWarperReserve < 0 || j.OriginalExtraWarperReserve > 100
            || !j.NativeWarperCount.HasValue || j.NativeWarperCount < 1 + j.OriginalExtraWarperReserve
            || j.NowUtc != f.NowUtc || !Known(f.StopWallPatienceSeconds) || f.StopWallPatienceSeconds < 1d
            || f.StopWallPatienceSeconds > VoyagerActiveMoveGroundGapPolicy.MaximumStopWallPatienceSeconds
            || (j.JourneyDeadlineUtc - f.NowUtc).TotalSeconds <= j.ReturnReserveGameTicks / 60d + f.StopWallPatienceSeconds + 1d / 60d
            || !Known(f.CoreEnergyJoules) || !Known(f.PerNativeTickEnergyJoules)
            || !Known(j.AvailableLoadedEnergyJoules) || !Known(j.LockedReturnFloorJoules)) return false;
        var ticks = (decimal)j.SpentGameTicks + j.CurrentGameTick - j.LastLandedGameTick.Value
            + 1 + VoyagerLocalWalkReservePolicy.StopGameTicks + j.ReturnReserveGameTicks;
        var energy = f.PerNativeTickEnergyJoules!.Value * (1 + VoyagerLocalWalkReservePolicy.StopGameTicks);
        var loaded = j.LockedReturnFloorJoules!.Value + energy;
        return ticks <= j.MaximumGameTicks && Known(energy) && Known(loaded)
            && f.CoreEnergyJoules > 0d && f.CoreEnergyJoules >= energy && j.AvailableLoadedEnergyJoules >= loaded;
    }
    private string Reject(string code) { Failure ??= code; return Failure; }
    private static bool Known(double v) => Finite(v) && v >= 0d;
    private static bool Known(double? v) => v.HasValue && Known(v.Value);
    private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}
