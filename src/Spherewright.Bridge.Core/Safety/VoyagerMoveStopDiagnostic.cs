using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Safety;

// Copied diagnostic facts only. This cannot authorize a tick, Resume, stop
// extension, cleanup or reinterpret an old unknown result as task success.
public sealed class VoyagerMoveStopDiagnosticFacts
{
    public long GameTick { get; set; }
    public DateTimeOffset NowUtc { get; set; }
    public long ArmGameTick { get; set; }
    public long DeadlineGameTick { get; set; }
    public bool? GroundSphereHit { get; set; }
    public bool ActualWalk { get; set; }
    public bool FrameWalk { get; set; }
    public double Speed { get; set; }
    public double WalkRtsSquared { get; set; }
    public double WalkManualSquared { get; set; }
    public double DriftRtsSquared { get; set; }
    public double DriftManualSquared { get; set; }
    public double CoreEnergy { get; set; }
    public double VerticalSpeed { get; set; }
    public double FixedDeltaTimeSeconds { get; set; }
    public double ControllerVelocitySquared { get; set; }
    public double? RigidVelocitySquared { get; set; }
    public bool NativePaused { get; set; }
    public bool CurrentOrderAbsent { get; set; }
    public int QueuedOrders { get; set; }
}

public static class VoyagerMoveStopDiagnostic
{
    public static NativeResumeFailureSnapshot Capture(VoyagerMoveStopDiagnosticFacts f)
    {
        var e = new NativeResumeDecisionEvidence("original_move_stop_pre_player", f.GameTick, f.NowUtc, true);
        e.Number("stop_arm_tick", f.ArmGameTick); e.Number("stop_deadline_tick", f.DeadlineGameTick);
        e.Number("speed_mps", f.Speed); e.Number("vertical_mps", f.VerticalSpeed);
        e.Number("walk_rts_squared", f.WalkRtsSquared); e.Number("walk_manual_squared", f.WalkManualSquared);
        e.Number("drift_rts_squared", f.DriftRtsSquared); e.Number("drift_manual_squared", f.DriftManualSquared);
        e.Number("core_j", f.CoreEnergy); e.Number("queued_orders", f.QueuedOrders);
        e.Number("fixed_delta_seconds", f.FixedDeltaTimeSeconds);
        e.Number("controller_velocity_squared", f.ControllerVelocitySquared); e.Number("rigid_velocity_squared", f.RigidVelocitySquared);
        e.Observe("native_paused_at_original_cut", f.NativePaused);
        e.Observe("actual_walk_at_original_cut", f.ActualWalk); e.Observe("frame_walk_at_original_cut", f.FrameWalk);
        e.Observe("original_order_absent", f.CurrentOrderAbsent);
        e.Require("strict_ground_sphere", f.GroundSphereHit);
        e.Require("strict_actual_walk", f.ActualWalk); e.Require("strict_frame_walk", f.FrameWalk);
        e.Require("strict_speed_at_rest", f.Speed <= .1d);
        e.Require("strict_walk_rts_zero", f.WalkRtsSquared == 0d); e.Require("strict_walk_manual_zero", f.WalkManualSquared == 0d);
        e.Require("strict_drift_rts_zero", f.DriftRtsSquared == 0d); e.Require("strict_drift_manual_zero", f.DriftManualSquared == 0d);
        e.NotReached("native_pause_after_failure", "post_player_settlement", "serialization", "resume");
        return e.Failure("voyager_local_native_tail_unproved", "The original finite stop ended; copied facts do not grant a later tick or change the task outcome.");
    }
}
