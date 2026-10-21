namespace Spherewright.Bridge.Core.Safety;

public enum NativeResumeContactStep { Pending, Ready, AlreadyRetired, Rejected }

// Copied observations for this recovery's original Native frame, never a Move
// marker, an idle=true projection, or a prediction that the fall will succeed.
public sealed class NativeResumeContactFacts
{
    public bool ExactRecoveryBinding { get; set; }
    public bool NonGroundNativePremisesProved { get; set; }
    public bool CurrentSupportReferenceProved { get; set; }
    public bool FootQueryComplete { get; set; }
    public int FootOverlapCount { get; set; }
    public bool AllFootHitsOwned { get; set; }
    public bool? NativeGroundSphereHit { get; set; }
    public bool StrictPhysicalIdleProved { get; set; }
    public double? CastDistanceMeters { get; set; }
    public double? CastNormalDotUp { get; set; }
    public double? InwardDisplacementMeters { get; set; }
    public double? TangentialDisplacementMeters { get; set; }
    public double? SpeedMetersPerSecond { get; set; }
    public double? CoreEnergyJoules { get; set; }
    public double? PerNativeTickEnergyJoules { get; set; }
    public DateTimeOffset NowUtc { get; set; }
    public VoyagerLocalActionBudgetFacts? OriginalJourney { get; set; }
}

// One attempt, at most eight601s including the failure frame after Pause.
// The eighth501 must retire with actual idle or Pause before that frame's601.
// A151 observation never publishes idle or consumes a501. Nothing
// here moves an actor, permits a Save, pays energy, or renews the old journey.
public sealed class NativeResumeContactPolicy
{
    public const string GroundingPending = "remote_resume_native_contact_pending";
    public const int MaximumNativeWorkTicks = 8;
    public const double MaximumWallSeconds = 2d;
    public const double MaximumCastDistanceMeters = .25d;
    public const double MinimumNormalDotUp = .5d;
    public const double MaximumInwardDisplacementMeters = .25d;
    public const double MaximumTangentialDisplacementMeters = .05d;
    public const double MaximumSpeedMetersPerSecond = 1.5d;
    public const int FundedTicks = 1 + VoyagerLocalWalkReservePolicy.StopGameTicks;
    public long SourceGameTick { get; }
    public long LastObservedGameTick { get; private set; }
    public long LastUniverseGameTick { get; private set; }
    public DateTimeOffset StartedUtc { get; }
    public bool Retired { get; private set; }
    public string? Failure { get; private set; }

    public NativeResumeContactPolicy(long sourceGameTick, DateTimeOffset startedUtc)
    {
        if (sourceGameTick < 0 || sourceGameTick > long.MaxValue - MaximumNativeWorkTicks - 1 || startedUtc == default)
            throw new ArgumentOutOfRangeException(nameof(sourceGameTick));
        SourceGameTick = LastObservedGameTick = LastUniverseGameTick = sourceGameTick;
        StartedUtc = startedUtc;
    }

    public string? PreflightBlocker(NativeResumeContactFacts f)
    {
        var blocker = CommonBlocker(f);
        if (blocker is not null) return blocker;
        return f.OriginalJourney!.CurrentGameTick == SourceGameTick && f.NativeGroundSphereHit == false
            && !f.StrictPhysicalIdleProved && f.FootOverlapCount == 0
            && f.SpeedMetersPerSecond <= .1d && f.InwardDisplacementMeters == 0d
            && f.TangentialDisplacementMeters == 0d ? null : "remote_resume_contact_preflight_unproved";
    }

    public string? ObserveUniverse(NativeResumeContactFacts f)
    {
        if (Failure is not null) return Failure;
        if (Retired) return "remote_resume_contact_attempt_retired";
        var blocker = CommonBlocker(f);
        var tick = f?.OriginalJourney?.CurrentGameTick;
        if (blocker is not null || tick != LastObservedGameTick + 1 || tick > SourceGameTick + MaximumNativeWorkTicks
            || LastUniverseGameTick != LastObservedGameTick)
        { Reject(blocker ?? "remote_resume_contact_universe_sequence_unproved"); return Failure; }
        LastUniverseGameTick = tick!.Value;
        return null;
    }

    public NativeResumeContactStep Observe(NativeResumeContactFacts f)
    {
        if (Failure is not null) return NativeResumeContactStep.Rejected;
        if (Retired) return NativeResumeContactStep.AlreadyRetired;
        var blocker = CommonBlocker(f);
        if (blocker is not null) return Reject(blocker);
        var tick = f.OriginalJourney!.CurrentGameTick;
        if (tick != LastObservedGameTick + 1 || tick != LastUniverseGameTick || tick > SourceGameTick + MaximumNativeWorkTicks)
            return Reject("remote_resume_contact_sequence_unproved");
        LastObservedGameTick = tick;
        if (f.StrictPhysicalIdleProved && f.NativeGroundSphereHit == true)
        {
            Retired = true;
            return NativeResumeContactStep.Ready;
        }
        return tick < SourceGameTick + MaximumNativeWorkTicks ? NativeResumeContactStep.Pending
            : Reject("remote_resume_contact_attempt_exhausted");
    }

    public NativeResumeContactStep Reject(string reason)
    {
        Failure ??= reason;
        return NativeResumeContactStep.Rejected;
    }

    private string? CommonBlocker(NativeResumeContactFacts f)
    {
        if (f is null || !f.ExactRecoveryBinding || !f.NonGroundNativePremisesProved
            || !f.CurrentSupportReferenceProved || !f.FootQueryComplete || !f.NativeGroundSphereHit.HasValue
            || f.FootOverlapCount < 0 || f.FootOverlapCount > NativeTerrainObservationPolicy.MaximumSurfaceColliders
            || (f.NativeGroundSphereHit == false ? f.FootOverlapCount != 0 : f.FootOverlapCount < 1 || !f.AllFootHitsOwned)
            || f.StrictPhysicalIdleProved && f.NativeGroundSphereHit != true)
            return "remote_resume_contact_native_support_unproved";
        if (f.NowUtc == default || f.NowUtc < StartedUtc || (f.NowUtc - StartedUtc).TotalSeconds >= MaximumWallSeconds)
            return "remote_resume_contact_wall_exhausted";
        if (!f.NativeGroundSphereHit.Value && (!Known(f.CastDistanceMeters) || f.CastDistanceMeters > MaximumCastDistanceMeters
            || !Finite(f.CastNormalDotUp) || f.CastNormalDotUp < MinimumNormalDotUp || f.CastNormalDotUp > 1d))
            return "remote_resume_contact_near_support_unproved";
        if (!Finite(f.InwardDisplacementMeters) || f.InwardDisplacementMeters < -.01d
            || f.InwardDisplacementMeters > MaximumInwardDisplacementMeters || !Known(f.TangentialDisplacementMeters)
            || f.TangentialDisplacementMeters > MaximumTangentialDisplacementMeters || !Known(f.SpeedMetersPerSecond)
            || f.SpeedMetersPerSecond > MaximumSpeedMetersPerSecond
            || f.StrictPhysicalIdleProved && !(f.SpeedMetersPerSecond <= .1d))
            return "remote_resume_contact_motion_unproved";
        var j = f.OriginalJourney;
        if (j is null || !j.ExactRemoteBinding || j.NativePlayerIdle != f.StrictPhysicalIdleProved
            || !j.LastLandedGameTick.HasValue || j.LastLandedGameTick < 0 || j.CurrentGameTick < j.LastLandedGameTick
            || j.SpentGameTicks < 0 || j.MaximumGameTicks < 1 || j.ReturnReserveGameTicks < 3600 || j.ReturnReserveGameTicks > 108000
            || j.WorkGameTicks != 1 || j.TailGameTicks != VoyagerLocalWalkReservePolicy.StopGameTicks
            || j.OriginalExtraWarperReserve < 0 || j.OriginalExtraWarperReserve > 100
            || !j.NativeWarperCount.HasValue || j.NativeWarperCount < 1 + j.OriginalExtraWarperReserve
            || j.NowUtc == default || f.NowUtc < j.NowUtc || f.NowUtc - j.NowUtc > TimeSpan.FromSeconds(2)
            || (decimal)j.SpentGameTicks + j.CurrentGameTick - j.LastLandedGameTick.Value + FundedTicks + j.ReturnReserveGameTicks > j.MaximumGameTicks
            || (j.JourneyDeadlineUtc - f.NowUtc).TotalSeconds <= j.ReturnReserveGameTicks / 60d + 1d + 1d / 60d)
            return "remote_resume_contact_original_budget_unproved";
        if (!Known(f.CoreEnergyJoules) || !Known(f.PerNativeTickEnergyJoules) || f.PerNativeTickEnergyJoules <= 0d
            || !Known(j.AvailableLoadedEnergyJoules) || !Known(j.LockedReturnFloorJoules))
            return "remote_resume_contact_funding_unproved";
        var cost = f.PerNativeTickEnergyJoules!.Value * FundedTicks;
        var loaded = j.LockedReturnFloorJoules!.Value + cost;
        return Known(cost) && Known(loaded) && f.CoreEnergyJoules >= cost && j.AvailableLoadedEnergyJoules >= loaded
            ? null : "remote_resume_contact_current_and_stop_unfunded";
    }
    private static bool Known(double? n) => Finite(n) && n >= 0d;
    private static bool Finite(double? n) => n.HasValue && !double.IsNaN(n.Value) && !double.IsInfinity(n.Value);
}
