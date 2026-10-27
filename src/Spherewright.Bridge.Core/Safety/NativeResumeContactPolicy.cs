using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Safety;

public enum NativeResumeContactStep { Pending, Ready, AlreadyRetired, Rejected }

// Copied observations for this recovery's original Native frame, never a Move
// marker, an idle=true projection, or a prediction that the fall will succeed.
public sealed class NativeResumeContactFacts
{
    public long? CapturedAtGameTick { get; set; }
    public bool? ExactRecoveryBinding { get; set; }
    public bool? NonGroundNativePremisesProved { get; set; }
    public bool? CurrentSupportReferenceProved { get; set; }
    public bool? FootQueryComplete { get; set; }
    public int? FootOverlapCount { get; set; }
    public bool? AllFootHitsOwned { get; set; }
    public bool? NativeGroundSphereHit { get; set; }
    public bool? StrictPhysicalIdleProved { get; set; }
    public double? CastDistanceMeters { get; set; }
    public double? CastNormalDotUp { get; set; }
    public double? InwardDisplacementMeters { get; set; }
    public double? TangentialDisplacementMeters { get; set; }
    public double? SpeedMetersPerSecond { get; set; }
    public double? CoreEnergyJoules { get; set; }
    public double? PerNativeTickEnergyJoules { get; set; }
    public DateTimeOffset NowUtc { get; set; }
    public VoyagerLocalActionBudgetFacts? OriginalJourney { get; set; }
    public VoyagerReturnOnlyIdleBudgetFacts? ReturnOnlyBudget { get; set; }
}

// One attempt, at most eight601s including the failure frame after Pause.
// The eighth501 must retire with actual idle or Pause before that frame's601.
// A151 observation never publishes idle or consumes a501. Nothing
// here moves an actor, permits a Save, pays energy, or renews the old journey.
public sealed class NativeResumeContactPolicy
{
    // Conditional source geometry for this same eight-tick contact controller.
    // Actual Native ground remains false; layer31 is never passed off as9.
    public static bool LoadedTerrainCastProved(NativeNearGroundSupportSnapshot s) => s.State == "observed"
        && s.ActualGroundSphereHit == false && s.CastAttempted == true && s.CastHit == true
        && s.CastLayerMask == (1 << 31) && s.Layer == 31 && s.Shape == "mesh" && s.OwnerType == "current_terrain"
        && s.CastDistance.HasValue && !float.IsNaN(s.CastDistance.Value) && s.CastDistance >= 0f
        && s.CastDistance <= MaximumCastDistanceMeters && s.NormalDotUp.HasValue && !float.IsNaN(s.NormalDotUp.Value)
        && s.NormalDotUp >= MinimumNormalDotUp && s.NormalDotUp <= 1.001f
        && s.Enabled == true && s.ActiveInHierarchy == true && s.IsTrigger == false && s.SameDefaultPhysicsScene == true
        && s.CurrentPoolReference == true && s.LiveObject == true && s.ColliderChain == true && s.NativeLiveFlag is null
        && s.FootOverlapComplete == true && s.FootOverlapCount == 0 && s.AllFootHitsCurrentSupportOrTerrain == true;
    public static bool InitializedTerrainCastProved(NativeNearGroundSupportSnapshot s) => s.State == "observed"
        && s.ActualGroundSphereHit == false && s.CastAttempted == true && s.CastHit == true
        && s.CastLayerMask == 15873 && s.Layer == 9 && s.Shape == "mesh" && s.OwnerType == "current_terrain"
        && s.CastDistance.HasValue && !float.IsNaN(s.CastDistance.Value) && s.CastDistance >= 0f && s.CastDistance <= .5f
        && s.NormalDotUp.HasValue && !float.IsNaN(s.NormalDotUp.Value) && s.NormalDotUp >= MinimumNormalDotUp && s.NormalDotUp <= 1.001f
        && s.Enabled == true && s.ActiveInHierarchy == true && s.IsTrigger == false && s.SameDefaultPhysicsScene == true
        && s.CurrentPoolReference == true && s.LiveObject == true && s.ColliderChain == true && s.NativeLiveFlag is null
        && s.FootOverlapComplete == true && s.FootOverlapCount == 0 && s.AllFootHitsCurrentSupportOrTerrain == true;
    public const string GroundingPending = "remote_resume_native_contact_pending";
    public const int MaximumNativeWorkTicks = 8;
    public const double MaximumWallSeconds = 2d;
    public const double MaximumCastDistanceMeters = .25d;
    public const double MinimumNormalDotUp = .5d;
    public const double MaximumInwardDisplacementMeters = .25d;
    public const double MaximumTangentialDisplacementMeters = .05d;
    public const double MaximumSpeedMetersPerSecond = 1.5d;
    public const int FundedTicks = 1 + VoyagerLocalWalkReservePolicy.StopGameTicks;
    public int WorkTickLimit { get; }
    public double CastDistanceLimit { get; }
    public double SpeedLimit { get; }
    public double InwardLimit { get; }
    public long SourceGameTick { get; }
    public long LastObservedGameTick { get; private set; }
    public long LastUniverseGameTick { get; private set; }
    public DateTimeOffset StartedUtc { get; }
    public bool Retired { get; private set; }
    public string? Failure { get; private set; }
    public string? FailureDetail { get; private set; }
    public NativeResumeFailureSnapshot? FailureEvidence { get; private set; }
    public NativeResumeFailureSnapshot? LastDecisionEvidence { get; private set; }
    private NativeResumeDecisionEvidence? _decision;

    public NativeResumeContactPolicy(long sourceGameTick, DateTimeOffset startedUtc, bool initializedTerrain = false)
    {
        WorkTickLimit=initializedTerrain?32:MaximumNativeWorkTicks;
        CastDistanceLimit=initializedTerrain ? .5d : MaximumCastDistanceMeters;
        SpeedLimit=initializedTerrain?3d:MaximumSpeedMetersPerSecond;
        InwardLimit=initializedTerrain ? .5d : MaximumInwardDisplacementMeters;
        if (sourceGameTick < 0 || sourceGameTick > long.MaxValue - WorkTickLimit - 1 || startedUtc == default)
            throw new ArgumentOutOfRangeException(nameof(sourceGameTick));
        SourceGameTick = LastObservedGameTick = LastUniverseGameTick = sourceGameTick;
        StartedUtc = startedUtc;
    }

    public string? PreflightBlocker(NativeResumeContactFacts f, string stage = "load_preflight_before_first_frame")
    {
        var blocker = CommonBlocker(f, stage, out _);
        if (blocker is not null) return blocker;
        return _decision!.Require("preflight_source_tick", f.OriginalJourney!.CurrentGameTick == SourceGameTick)
            && _decision.Require("preflight_no_ground", f.NativeGroundSphereHit == false)
            && _decision.Require("preflight_not_idle", f.StrictPhysicalIdleProved == false)
            && _decision.Require("preflight_no_foot_overlap", f.FootOverlapCount == 0)
            && _decision.Require("preflight_at_rest", f.SpeedMetersPerSecond <= .1d)
            && _decision.Require("preflight_zero_inward", f.InwardDisplacementMeters == 0d)
            && _decision.Require("preflight_zero_tangent", f.TangentialDisplacementMeters == 0d)
            ? null : DecisionFailure("remote_resume_contact_preflight_unproved");
    }

    public string? ObserveUniverse(NativeResumeContactFacts f)
    {
        if (Failure is not null) return Failure;
        if (Retired) return "remote_resume_contact_attempt_retired";
        var blocker = CommonBlocker(f, "native_universe_post151", out var detail);
        var tick = f?.OriginalJourney?.CurrentGameTick;
        if (blocker is not null || tick != LastObservedGameTick + 1 || tick > SourceGameTick + WorkTickLimit
            || LastUniverseGameTick != LastObservedGameTick)
        { Reject(blocker ?? "remote_resume_contact_universe_sequence_unproved", detail); return Failure; }
        LastUniverseGameTick = tick!.Value;
        return null;
    }

    public NativeResumeContactStep Observe(NativeResumeContactFacts f)
    {
        if (Failure is not null) return NativeResumeContactStep.Rejected;
        if (Retired) return NativeResumeContactStep.AlreadyRetired;
        var blocker = CommonBlocker(f, "native_physics_post501", out var detail);
        if (blocker is not null) return Reject(blocker, detail);
        var tick = f.OriginalJourney!.CurrentGameTick;
        if (tick != LastObservedGameTick + 1 || tick != LastUniverseGameTick || tick > SourceGameTick + WorkTickLimit)
            return Reject("remote_resume_contact_sequence_unproved");
        LastObservedGameTick = tick;
        if (f.StrictPhysicalIdleProved == true && f.NativeGroundSphereHit == true)
        {
            Retired = true;
            return NativeResumeContactStep.Ready;
        }
        return tick < SourceGameTick + WorkTickLimit ? NativeResumeContactStep.Pending
            : Reject("remote_resume_contact_attempt_exhausted");
    }

    public NativeResumeContactStep Reject(string reason, string? detail = null)
    {
        if (Failure is null)
        {
            Failure = reason;
            FailureEvidence = LastDecisionEvidence is { } previous && previous.Code == reason
                ? previous.Copy() : _decision?.Failure(reason, detail);
            FailureDetail = FailureEvidence?.FirstFailedPredicate ?? detail;
        }
        return NativeResumeContactStep.Rejected;
    }

    private string DecisionFailure(string code, string? predicate = null)
    {
        LastDecisionEvidence = _decision!.Failure(code, predicate);
        return code;
    }

    private string DecisionFailure(NativeResumeFailureSnapshot failure)
    {
        var current=_decision!.Failure(failure.Code);
        foreach(var p in current.Values)if(!failure.Values.ContainsKey(p.Key))failure.Values[p.Key]=p.Value;
        foreach(var p in current.Observations)if(!failure.Observations.ContainsKey(p.Key))failure.Observations[p.Key]=p.Value;
        foreach(var p in current.Predicates)if(!failure.Predicates.ContainsKey(p.Key))failure.Predicates[p.Key]=p.Value;
        failure.Stage=current.Stage;failure.CapturedAtGameTick=current.CapturedAtGameTick;failure.CapturedAtUtc=current.CapturedAtUtc;
        LastDecisionEvidence=failure.Copy();return failure.Code;
    }

    private string? CommonBlocker(NativeResumeContactFacts f, string stage, out string? detail)
    {
        // Classify the predicate used by this decision, never re-query Native
        // after Pause or infer a cause from a later player snapshot.
        LastDecisionEvidence = null;
        _decision = CaptureEvidence(f, stage);
        if (f is null) { detail = "contact_facts_missing"; return DecisionFailure("remote_resume_contact_native_support_unproved", detail); }
        bool R(string name, bool? value) => _decision.Require(name, value);
        detail = !R("contact_exact_recovery_binding_unproved", f.ExactRecoveryBinding) ? "contact_exact_recovery_binding_unproved"
            : !R("contact_non_ground_native_premises_unproved", f.NonGroundNativePremisesProved) ? "contact_non_ground_native_premises_unproved"
            : !R("contact_current_support_reference_unproved", f.CurrentSupportReferenceProved) ? "contact_current_support_reference_unproved"
            : !R("contact_foot_query_incomplete", f.FootQueryComplete) ? "contact_foot_query_incomplete"
            : !R("contact_native_ground_query_unavailable", f.NativeGroundSphereHit.HasValue ? true : null) ? "contact_native_ground_query_unavailable"
            : !R("contact_foot_count_unproved", f.FootOverlapCount.HasValue ? f.FootOverlapCount >= 0 && f.FootOverlapCount <= NativeTerrainObservationPolicy.MaximumSurfaceColliders : null) ? "contact_foot_count_unproved"
            : f.NativeGroundSphereHit == false && !R("contact_airborne_foot_overlap", f.FootOverlapCount == 0) ? "contact_airborne_foot_overlap"
            : f.NativeGroundSphereHit == true && !R("contact_grounded_foot_count", f.FootOverlapCount >= 1) ? "contact_grounded_foot_count"
            : f.NativeGroundSphereHit == true && !R("contact_grounded_foot_owner_unproved", f.AllFootHitsOwned) ? "contact_grounded_foot_owner_unproved"
            : !R("contact_idle_observation", f.StrictPhysicalIdleProved.HasValue ? true : null) ? "contact_idle_observation"
            : f.StrictPhysicalIdleProved == true && !R("contact_idle_without_native_ground", f.NativeGroundSphereHit == true) ? "contact_idle_without_native_ground"
            : null;
        if (detail is not null) return DecisionFailure("remote_resume_contact_native_support_unproved", detail);
        bool All(params (string Name, bool? Value)[] checks)
        {
            _decision.NotReached(checks.Select(c => c.Name).ToArray());
            foreach (var check in checks) if (!R(check.Name, check.Value)) return false;
            return true;
        }
        if (!All(("clock_present", f.NowUtc != default), ("clock_after_start", f.NowUtc >= StartedUtc),
            ("contact_wall_remaining", (f.NowUtc - StartedUtc).TotalSeconds < MaximumWallSeconds)))
            return DecisionFailure("remote_resume_contact_wall_exhausted");
        if (f.NativeGroundSphereHit == false && !All(
            ("cast_distance_known", Known(f.CastDistanceMeters)), ("cast_distance_bounded", f.CastDistanceMeters <= CastDistanceLimit),
            ("cast_normal_known", Finite(f.CastNormalDotUp)), ("cast_normal_supporting", f.CastNormalDotUp >= MinimumNormalDotUp),
            ("cast_normal_normalized", f.CastNormalDotUp <= 1d)))
            return DecisionFailure("remote_resume_contact_near_support_unproved");
        if (!All(("inward_known", Finite(f.InwardDisplacementMeters)), ("inward_lower_bound", f.InwardDisplacementMeters >= -.01d),
            ("inward_upper_bound", f.InwardDisplacementMeters <= InwardLimit),
            ("tangent_known", Known(f.TangentialDisplacementMeters)), ("tangent_bounded", f.TangentialDisplacementMeters <= MaximumTangentialDisplacementMeters),
            ("speed_known", Known(f.SpeedMetersPerSecond)), ("speed_bounded", f.SpeedMetersPerSecond <= SpeedLimit),
            ("idle_speed_bounded", f.StrictPhysicalIdleProved != true || f.SpeedMetersPerSecond <= .1d)))
            return DecisionFailure("remote_resume_contact_motion_unproved");
        var j = f.OriginalJourney;
        if (!R("journey_observed", j is not null)) return DecisionFailure("remote_resume_contact_original_budget_unproved");
        if (!All(("journey_binding", j!.ExactRemoteBinding), ("journey_idle_consistent", j.NativePlayerIdle == f.StrictPhysicalIdleProved),
            ("last_landed_known", j.LastLandedGameTick.HasValue), ("last_landed_nonnegative", j.LastLandedGameTick >= 0),
            ("current_tick_after_landing", j.CurrentGameTick >= j.LastLandedGameTick), ("spent_nonnegative", j.SpentGameTicks >= 0),
            ("maximum_ticks_positive", j.MaximumGameTicks >= 1), ("return_ticks_minimum", j.ReturnReserveGameTicks >= 3600),
            ("return_ticks_maximum", j.ReturnReserveGameTicks <= 108000), ("current_work_tick", j.WorkGameTicks == 1),
            ("original_stop_ticks", j.TailGameTicks == VoyagerLocalWalkReservePolicy.StopGameTicks),
            ("extra_warpers_nonnegative", j.OriginalExtraWarperReserve >= 0), ("extra_warpers_bounded", j.OriginalExtraWarperReserve <= 100),
            ("warpers_observed", j.NativeWarperCount.HasValue), ("original_warpers_retained", j.NativeWarperCount >= 1 + j.OriginalExtraWarperReserve),
            ("journey_clock_present", j.NowUtc != default), ("journey_clock_not_future", f.NowUtc >= j.NowUtc),
            ("journey_sample_fresh", f.NowUtc - j.NowUtc <= TimeSpan.FromSeconds(2))))
            return DecisionFailure("remote_resume_contact_original_budget_unproved");
        if(f.ReturnOnlyBudget is { } returnBudget)
        {
            var failed=VoyagerReturnOnlyIdleBudget.TimeFailure(returnBudget,j)
                ??VoyagerReturnOnlyIdleBudget.ReserveFailure(returnBudget,j,requireStrictIdle:false);
            if(failed is not null)return DecisionFailure(failed);
        }
        else if(!All(("original_tick_reserve",j.LastLandedGameTick.HasValue
                &&(decimal)j.SpentGameTicks+j.CurrentGameTick-j.LastLandedGameTick.Value+FundedTicks+j.ReturnReserveGameTicks<=j.MaximumGameTicks),
            ("original_wall_reserve",(j.JourneyDeadlineUtc-f.NowUtc).TotalSeconds>j.ReturnReserveGameTicks/60d+1d+1d/60d)))
            return DecisionFailure("remote_resume_contact_original_budget_unproved");
        if (!All(("core_observed", Known(f.CoreEnergyJoules)), ("per_tick_observed", Known(f.PerNativeTickEnergyJoules)),
            ("per_tick_positive", f.PerNativeTickEnergyJoules > 0d), ("loaded_observed", Known(j.AvailableLoadedEnergyJoules)),
            ("return_floor_observed", Known(j.LockedReturnFloorJoules))))
            return DecisionFailure("remote_resume_contact_funding_unproved");
        var cost = f.PerNativeTickEnergyJoules!.Value * FundedTicks;
        var loaded = j.LockedReturnFloorJoules!.Value + cost;
        return All(("current_and_stop_cost_finite", Known(cost)), ("loaded_requirement_finite", Known(loaded)),
            ("current_and_stop_core_funded", f.CoreEnergyJoules >= cost), ("current_and_stop_loaded_funded", j.AvailableLoadedEnergyJoules >= loaded))
            ? null : DecisionFailure("remote_resume_contact_current_and_stop_unfunded");
    }

    private NativeResumeDecisionEvidence CaptureEvidence(NativeResumeContactFacts? f, string stage)
    {
        var e = new NativeResumeDecisionEvidence(stage, f?.CapturedAtGameTick ?? f?.OriginalJourney?.CurrentGameTick,
            f is null || f.NowUtc == default ? null : f.NowUtc, f is not null);
        e.Observe("native_ground_sphere_hit", f?.NativeGroundSphereHit);
        e.Observe("all_foot_hits_owned", f?.AllFootHitsOwned);
        e.Observe("strict_physical_idle", f?.StrictPhysicalIdleProved);
        e.NotReached("contact_exact_recovery_binding_unproved", "contact_non_ground_native_premises_unproved",
            "contact_current_support_reference_unproved", "contact_foot_query_incomplete", "contact_native_ground_query_unavailable",
            "contact_foot_count_unproved", "contact_airborne_foot_overlap", "contact_grounded_foot_count",
            "contact_grounded_foot_owner_unproved", "contact_idle_observation", "contact_idle_without_native_ground");
        e.Number("foot_overlap_count", f?.FootOverlapCount); e.Number("cast_distance_m", f?.CastDistanceMeters);
        e.Number("cast_normal_dot_up", f?.CastNormalDotUp); e.Number("inward_displacement_m", f?.InwardDisplacementMeters);
        e.Number("tangential_displacement_m", f?.TangentialDisplacementMeters); e.Number("speed_mps", f?.SpeedMetersPerSecond);
        e.Number("core_j", f?.CoreEnergyJoules); e.Number("per_tick_j", f?.PerNativeTickEnergyJoules);
        e.Number("funded_ticks", FundedTicks); e.Number("maximum_contact_ticks", WorkTickLimit);
        var j = f?.OriginalJourney;
        e.Number("spent_ticks", j?.SpentGameTicks); e.Number("last_landed_tick", j?.LastLandedGameTick);
        e.Number("maximum_journey_ticks", j?.MaximumGameTicks); e.Number("return_ticks", j?.ReturnReserveGameTicks);
        e.Number("loaded_j", j?.AvailableLoadedEnergyJoules); e.Number("return_floor_j", j?.LockedReturnFloorJoules);
        e.Number("warpers", j?.NativeWarperCount); e.Number("original_extra_warpers", j?.OriginalExtraWarperReserve);
        e.Observe("journey_exact_binding", j?.ExactRemoteBinding); e.Observe("journey_native_idle", j?.NativePlayerIdle);
        e.Number("return_wall_seconds", j is not null && f is not null ? (j.JourneyDeadlineUtc - f.NowUtc).TotalSeconds : null);
        return e;
    }
    private static bool Known(double? n) => Finite(n) && n >= 0d;
    private static bool Finite(double? n) => n.HasValue && !double.IsNaN(n.Value) && !double.IsInfinity(n.Value);
}
