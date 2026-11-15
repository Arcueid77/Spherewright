using System.Globalization;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Bridge.Core.Safety;

// These are copied by the native caller from one retained same-session
// quarantine. They are not a public attestation and do not grant execution.
public sealed class VoyagerSameSessionSurfaceSourceFacts
{
    public object? World { get; set; }
    public object? Player { get; set; }
    public object? Controller { get; set; }
    public object? Rigidbody { get; set; }
    public object? Factory { get; set; }
    public object? NativeSceneBinding { get; set; }
    public object? NativeTransformBinding { get; set; }
    // The store is retained by reference; its Journey property can return
    // detached copies, so the record itself must be bound by fingerprints.
    public object? JourneyStore { get; set; }
    public object? OriginalActionRecord { get; set; }
    public string JourneyId { get; set; } = string.Empty;
    public string ExpectedJourneyId { get; set; } = string.Empty;
    public string JourneySnapshotFingerprint { get; set; } = string.Empty;
    public string DurableEvidenceFingerprint { get; set; } = string.Empty;
    public DateTimeOffset OriginalJourneyIssuedAtUtc { get; set; }
    public DateTimeOffset OriginalJourneyDeadlineUtc { get; set; }
    public DateTimeOffset? ProposedRecoveryDeadlineUtc { get; set; }
    public SessionState? Session { get; set; }
    public ActionResultSnapshot? OriginalMove { get; set; }
    public PlayerStateSnapshot? PlayerState { get; set; }
    public Vector3Snapshot? RigidbodyVelocity { get; set; }
    public Vector3Snapshot? RigidbodyAngularVelocity { get; set; }
    public Vector3Snapshot? Target { get; set; }
    public VoyagerSurfaceRecoveryFacts? Funding { get; set; }
    public bool? NativePaused { get; set; }
    public bool ExactNativeReferencesProved { get; set; }
    public bool SceneAndColliderOwnershipProved { get; set; }
    public bool NativeTransformStillMatches { get; set; }
    public bool DurableEvidenceVerified { get; set; }
    public bool CurrentTerrainSupportProved { get; set; }
    public bool DryLandingPatchProved { get; set; }
    public bool NoOtherActiveActions { get; set; }
    public bool NativeInputIdle { get; set; }
    public int RenderFrame { get; set; }
    public DateTimeOffset CapturedAtUtc { get; set; }
}

// A binding only preserves evidence for a later, separately authorized and
// guarded Native recovery task. It neither clears quarantine nor resumes DSP.
public sealed class VoyagerSameSessionSurfaceSourceBinding
{
    private readonly object _world, _player, _controller, _rigidbody, _factory, _scene, _transform, _journey, _originalAction;
    private readonly string _fingerprint;
    private readonly DateTimeOffset _lastCapture;
    private readonly int _lastFrame;
    private readonly Vector3Snapshot _target;

    internal VoyagerSameSessionSurfaceSourceBinding(VoyagerSameSessionSurfaceSourceFacts f, string fingerprint)
    {
        _world = f.World!; _player = f.Player!; _controller = f.Controller!; _rigidbody = f.Rigidbody!;
        _factory = f.Factory!; _scene = f.NativeSceneBinding!; _transform = f.NativeTransformBinding!;
        _journey = f.JourneyStore!; _originalAction = f.OriginalActionRecord!;
        _fingerprint = fingerprint; _lastCapture = f.CapturedAtUtc; _lastFrame = f.RenderFrame;
        OriginalActionId = f.OriginalMove!.ActionId;
        SourceGameTick = f.Session!.GameTick!.Value;
        _target = VoyagerSurfaceRecoveryPolicy.CopyTarget(f.Target)!;
    }

    public string OriginalActionId { get; }
    public long SourceGameTick { get; }
    public Vector3Snapshot Target => VoyagerSurfaceRecoveryPolicy.CopyTarget(_target)!;
    public string Fingerprint => _fingerprint;
    public bool GrantsResume => false;
    public bool OriginalOutcomeChanged => false;

    public bool MatchesFresh(VoyagerSameSessionSurfaceSourceFacts current, out string rejection)
    {
        rejection = VoyagerSameSessionSurfaceSourcePolicy.Blocker(current) ?? string.Empty;
        if (rejection.Length != 0) return false;
        if (!ReferenceEquals(_world, current.World) || !ReferenceEquals(_player, current.Player)
            || !ReferenceEquals(_controller, current.Controller) || !ReferenceEquals(_rigidbody, current.Rigidbody)
            || !ReferenceEquals(_factory, current.Factory) || !ReferenceEquals(_scene, current.NativeSceneBinding)
            || !ReferenceEquals(_transform, current.NativeTransformBinding) || !ReferenceEquals(_journey, current.JourneyStore)
            || !ReferenceEquals(_originalAction, current.OriginalActionRecord))
            { rejection = "same_session_surface_native_reference_changed"; return false; }
        if (current.RenderFrame < _lastFrame || current.CapturedAtUtc < _lastCapture
            || (current.CapturedAtUtc - _lastCapture).TotalSeconds > VoyagerSurfaceRecoveryPolicy.MaximumInitialBindingWallSeconds)
            { rejection = "same_session_surface_capture_expired"; return false; }
        if (VoyagerSameSessionSurfaceSourcePolicy.Fingerprint(current) != _fingerprint)
            { rejection = "same_session_surface_bound_state_changed"; return false; }
        return true;
    }
}

public static class VoyagerSameSessionSurfaceSourcePolicy
{
    public static bool TryBind(VoyagerSameSessionSurfaceSourceFacts first,
        VoyagerSameSessionSurfaceSourceFacts second, out VoyagerSameSessionSurfaceSourceBinding? binding,
        out string rejection)
    {
        binding = null;
        rejection = Blocker(first) ?? Blocker(second) ?? string.Empty;
        if (rejection.Length != 0) return false;
        if (second.RenderFrame <= first.RenderFrame || second.CapturedAtUtc < first.CapturedAtUtc
            || (second.CapturedAtUtc - first.CapturedAtUtc).TotalSeconds > VoyagerSurfaceRecoveryPolicy.MaximumInitialBindingWallSeconds)
            { rejection = "same_session_surface_stable_pause_unproved"; return false; }
        if (!SameReferences(first, second) || Fingerprint(first) != Fingerprint(second))
            { rejection = "same_session_surface_frozen_source_changed"; return false; }
        binding = new VoyagerSameSessionSurfaceSourceBinding(second, Fingerprint(second));
        return true;
    }

    internal static string? Blocker(VoyagerSameSessionSurfaceSourceFacts? f)
    {
        if (f is null || f.World is null || f.Player is null || f.Controller is null || f.Rigidbody is null
            || f.Factory is null || f.NativeSceneBinding is null || f.NativeTransformBinding is null
            || f.JourneyStore is null || f.OriginalActionRecord is null
            || !f.ExactNativeReferencesProved || !f.SceneAndColliderOwnershipProved || !f.NativeTransformStillMatches
            || !f.DurableEvidenceVerified || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(f.JourneySnapshotFingerprint)
            || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(f.DurableEvidenceFingerprint))
            return "same_session_surface_native_binding_unproved";
        var s = f.Session; var a = f.OriginalMove; var p = f.PlayerState;
        if (s is null || !s.GameLoaded || !s.OwnedBySpherewright || s.AccessRestricted || s.WritesAllowed
            || s.WriteHealth != WriteHealthStates.Quarantined || string.IsNullOrWhiteSpace(s.SessionId)
            || string.IsNullOrWhiteSpace(s.SaveName) || string.IsNullOrWhiteSpace(s.GameVersion)
            || s.PeacefulMode != PeacefulModeStates.ConfirmedPeaceful || s.SandboxMode != SandboxModeStates.ConfirmedDisabled
            || !s.ResourceMultiplier.HasValue || !Finite(s.ResourceMultiplier.Value) || s.ResourceMultiplier.Value <= 0
            || s.LocalPlanetId is null or <= 0 || s.GameTick is null or < 0 || s.Revision <= 0
            || s.WriteBlockers is null || s.WriteBlockers.Count != 1 || s.WriteBlockers[0] is null
            || s.WriteBlockers[0].Code != BridgeErrorCodes.WriteSubsystemQuarantined)
            return "same_session_surface_quarantine_unproved";
        if (a is null || !a.Terminal || a.Succeeded || a.State != NormalActionStates.OutcomeUnknown
            || a.ActionKind != NormalActionKinds.Move || a.FailureKind != "voyager_local_native_tail_unproved"
            || a.ReconciledFromOutcomeUnknown || !Guid.TryParse(a.ActionId, out _)
            || !Guid.TryParse(a.IdempotencyKey, out _) || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(a.BeforeStateHash)
            || string.IsNullOrWhiteSpace(a.Message) || a.NativeFirstFailure is not { } failure
            || failure.Code != "voyager_local_native_tail_unproved" || failure.Stage != "original_move_stop_pre_player"
            || failure.FactsState != "observed" || failure.FirstFailedPredicate != "strict_ground_sphere"
            || failure.CapturedAtGameTick != a.CompletedAtGameTick || failure.CapturedAtUtc is null
            || failure.Predicates is null || failure.Observations is null || failure.Values is null
            || !failure.Predicates.TryGetValue("strict_ground_sphere", out var groundMiss) || groundMiss != "false"
            || !failure.Observations.TryGetValue("original_order_absent", out var absent) || absent != "true"
            || failure.Predicates.Any(kv => kv.Value is null) || failure.Observations.Any(kv => kv.Value is null)
            || failure.Values.Any(kv => kv.Value.HasValue && !Finite(kv.Value.Value))
            || a.ActionId != s.WriteQuarantineActionId || a.SessionId != s.SessionId || a.PlanetId != s.LocalPlanetId
            || a.StartedAtGameTick is null or < 0 || a.CompletedAtGameTick is null
            || a.CompletedAtGameTick < a.StartedAtGameTick || a.CompletedAtGameTick > s.GameTick
            || s.WriteBlockers[0].Message != a.Message)
            return "same_session_surface_original_move_unproved";
        if (f.NativePaused != true || f.RenderFrame < 0 || f.CapturedAtUtc == default
            || !f.NoOtherActiveActions || !f.NativeInputIdle)
            return "same_session_surface_paused_order_unproved";
        if (p is null || p.SessionId != s.SessionId || p.PlanetId != s.LocalPlanetId
            || p.CapturedAtGameTick != s.GameTick || !p.IsAlive || !p.IsOnPlanet || p.IsFlying || p.IsSailing
            || p.MovementState != "Drift" || p.SpaceTransit is not null || p.InHandItem is not null || p.HandcraftQueue is null || p.HandcraftQueue.Count != 0
            || p.ConstructionDrones is null || p.ConstructionDrones.ActiveObservationsComplete != true
            || p.ConstructionDrones.Working != 0 || p.ConstructionDrones.Idle != p.ConstructionDrones.Total
            || p.ConstructionDrones.Alive != p.ConstructionDrones.Total
            || p.ConstructionDrones.PendingBuildTargets != 0 || p.ConstructionDrones.PendingRepairTargets != 0
            || p.StateHashVersion != CanonicalStateHash.Version || !Finite(p.Position) || !PlayerHashMatches(p))
            return "same_session_surface_player_unproved";
        var physics = p.NativePhysics;
        var ground = physics?.GroundContact;
        if (physics is null || physics.State != "observed" || physics.SimulationMode != "FixedUpdate"
            || physics.AutoSimulation != true || !physics.AutoSyncTransforms.HasValue || !physics.FixedDeltaTimeSeconds.HasValue
            || ground is null || ground.State != "observed" || ground.CapturedAtGameTick != s.GameTick
            || !ground.CheckSphereHit.HasValue || !ground.QueriesHitTriggers.HasValue
            || !physics.UseGravity.HasValue || string.IsNullOrWhiteSpace(physics.Constraints)
            || string.IsNullOrWhiteSpace(physics.CollisionDetectionMode)
            || physics.SameDefaultPhysicsScene != true || physics.CollidersComplete != true || physics.CollidersTruncated != false
            || physics.IsKinematic != false || physics.DetectCollisions != true
            || physics.ControllerMovementStateInFrame != "Drift" || physics.CurrentOrderPresent != false
            || physics.QueuedOrderCount != 0 || !Zero(physics.Velocity) || !Zero(physics.WalkRtsVelocity)
            || !Zero(physics.DriftRtsVelocity) || !Zero(physics.WalkManualVelocity) || !Zero(physics.DriftManualVelocity)
            || !Zero(f.RigidbodyVelocity) || !Zero(f.RigidbodyAngularVelocity)
            || physics.ControllerVerticalSpeed != 0 || !Finite(p.Speed) || p.Speed < 0 || p.Speed > .15f)
            return "same_session_surface_frozen_pose_unproved";
        if (!f.CurrentTerrainSupportProved || !f.DryLandingPatchProved || !ValidTarget(p.Position, f.Target))
            return "same_session_surface_target_or_support_unproved";
        if (!Guid.TryParse(f.JourneyId, out _) || f.JourneyId != f.ExpectedJourneyId
            || f.Funding is null || f.Funding.OriginalJourney is null
            || f.Funding.GameTick != s.GameTick || f.Funding.NowUtc != f.CapturedAtUtc
            || f.Funding.CoreEnergyJoules != p.CoreEnergy
            || f.Funding.StopWallPatienceSeconds != VoyagerActiveMoveGroundGapPolicy.StopWallPatience(physics.FixedDeltaTimeSeconds!.Value)
            || p.NativeLocalControlEnergy is not { CostInputsProved: true } energy
            || !(energy.IndependentDebitsExcluded || energy.IndependentDebitsBudgeted)
            || string.IsNullOrWhiteSpace(energy.Basis) || !Known(energy.PerNativeTickEnergyUpperJoules)
            || !KnownIfPresent(energy.IndependentEnergyPerNativeTickUpperJoules)
            || !KnownIfPresent(energy.ShieldRechargeEnergyPerNativeTickUpperJoules)
            || !KnownIfPresent(energy.LaserRechargeEnergyPerNativeTickUpperJoules)
            || energy.IndependentDebitsBudgeted && (!Known(energy.IndependentEnergyPerNativeTickUpperJoules)
                || !Known(energy.ShieldRechargeEnergyPerNativeTickUpperJoules) || !Known(energy.LaserRechargeEnergyPerNativeTickUpperJoules)
                || energy.IndependentEnergyPerNativeTickUpperJoules > energy.PerNativeTickEnergyUpperJoules)
            || f.Funding.OriginalJourney.SavedLandingAccountingAnchorGameTick.HasValue
            || f.Funding.OriginalJourney.SavedLandingSourceBindingProved
            || !f.Funding.OriginalJourney.LastLandedGameTick.HasValue
            || a.StartedAtGameTick < f.Funding.OriginalJourney.LastLandedGameTick
            || f.Funding.PerNativeTickEnergyJoules != energy.PerNativeTickEnergyUpperJoules)
            return "same_session_surface_original_reserve_unproved";
        var budget = f.Funding.OriginalJourney;
        if (f.OriginalJourneyIssuedAtUtc == default || f.OriginalJourneyIssuedAtUtc.Offset != TimeSpan.Zero
            || f.OriginalJourneyDeadlineUtc.Offset != TimeSpan.Zero || f.OriginalJourneyDeadlineUtc <= f.OriginalJourneyIssuedAtUtc
            || f.OriginalJourneyIssuedAtUtc > f.CapturedAtUtc || budget.JourneyDeadlineUtc == default
            || budget.JourneyDeadlineUtc.Offset != TimeSpan.Zero)
            return "same_session_surface_original_window_unproved";
        if (f.ProposedRecoveryDeadlineUtc is { } proposed)
        {
            var proposal = new VoyagerRecoveryWindowProposal(proposed, VoyagerRecoveryWindowProposal.TaskScope,
                VoyagerSurfaceRecoveryPolicy.MaximumWorkGameTicks, VoyagerSurfaceRecoveryPolicy.MaximumWallSeconds);
            if (f.CapturedAtUtc.Offset != TimeSpan.Zero || proposed.Offset != TimeSpan.Zero
                || proposed <= budget.JourneyDeadlineUtc || proposed <= f.OriginalJourneyDeadlineUtc
                || VoyagerRecoveryWindowPolicy.ProposalBlocker(proposal, f.CapturedAtUtc,
                    f.OriginalJourneyIssuedAtUtc, f.OriginalJourneyDeadlineUtc, budget.ReturnReserveGameTicks) is not null)
                return "same_session_surface_proposed_window_invalid";
            if (!VoyagerSurfaceRecoveryPolicy.ProspectiveWindowReserveAllows(f.Funding, proposed))
                return "same_session_surface_proposed_window_reserve_unproved";
            return null;
        }
        if (budget.ReturnReserveGameTicks is >= 3600 and <= 108000 && budget.NowUtc == f.CapturedAtUtc
            && Finite(f.Funding.StopWallPatienceSeconds) && f.Funding.StopWallPatienceSeconds >= 1d
            && f.Funding.StopWallPatienceSeconds <= VoyagerActiveMoveGroundGapPolicy.MaximumStopWallPatienceSeconds
            && (budget.JourneyDeadlineUtc - f.CapturedAtUtc).TotalSeconds
                <= budget.ReturnReserveGameTicks / 60d + f.Funding.StopWallPatienceSeconds + 1d / 60d)
            return "same_session_surface_original_wall_reserve_unproved";
        if (!VoyagerSurfaceRecoveryPolicy.OriginalReserveAllows(f.Funding))
            return "same_session_surface_original_reserve_unproved";
        return null;
    }

    private static bool SameReferences(VoyagerSameSessionSurfaceSourceFacts a, VoyagerSameSessionSurfaceSourceFacts b) =>
        ReferenceEquals(a.World, b.World) && ReferenceEquals(a.Player, b.Player) && ReferenceEquals(a.Controller, b.Controller)
        && ReferenceEquals(a.Rigidbody, b.Rigidbody) && ReferenceEquals(a.Factory, b.Factory)
        && ReferenceEquals(a.NativeSceneBinding, b.NativeSceneBinding) && ReferenceEquals(a.NativeTransformBinding, b.NativeTransformBinding)
        && ReferenceEquals(a.JourneyStore, b.JourneyStore) && ReferenceEquals(a.OriginalActionRecord, b.OriginalActionRecord);

    internal static string Fingerprint(VoyagerSameSessionSurfaceSourceFacts f)
    {
        var s = f.Session!; var a = f.OriginalMove!; var p = f.PlayerState!; var x = p.NativePhysics!;
        var j = f.Funding!.OriginalJourney!; var target = f.Target!; var energy = p.NativeLocalControlEnergy!;
        return CanonicalStateHash.Combine("same-session-surface-source-v1", s.SessionId, s.SaveName, s.GameVersion,
            s.PeacefulMode, s.SandboxMode, F(s.ResourceMultiplier!.Value),
            s.LocalPlanetId, s.GameTick, s.Revision, s.WriteQuarantineActionId, s.WriteBlockers[0].Message,
            a.ActionId, a.IdempotencyKey, a.BeforeStateHash, a.StartedAtGameTick, a.CompletedAtGameTick,
            FailureFingerprint(a.NativeFirstFailure!),
            f.JourneyId, f.JourneySnapshotFingerprint, f.DurableEvidenceFingerprint,
            f.OriginalJourneyIssuedAtUtc.UtcDateTime.Ticks, f.OriginalJourneyDeadlineUtc.UtcDateTime.Ticks,
            f.ProposedRecoveryDeadlineUtc?.UtcDateTime.Ticks,
            p.StateHash, CanonicalStateHash.Player(p), V(p.Position), p.MovementState, F(p.Speed), x.ControllerMovementStateInFrame,
            x.AutoSimulation, x.AutoSyncTransforms, F(x.FixedDeltaTimeSeconds!.Value), x.GroundContact!.QueriesHitTriggers, x.GroundContact.CheckSphereHit,
            x.UseGravity, x.Constraints, x.CollisionDetectionMode,
            V(x.Velocity!), V(x.WalkRtsVelocity!), V(x.DriftRtsVelocity!), V(x.WalkManualVelocity!),
            V(x.DriftManualVelocity!), V(f.RigidbodyVelocity!), V(f.RigidbodyAngularVelocity!), F(x.ControllerVerticalSpeed!.Value),
            energy.Basis, energy.IndependentDebitsExcluded, energy.IndependentDebitsBudgeted,
            OptionalD(energy.IndependentEnergyPerNativeTickUpperJoules), OptionalD(energy.ShieldRechargeEnergyPerNativeTickUpperJoules),
            OptionalD(energy.LaserRechargeEnergyPerNativeTickUpperJoules),
            V(target), j.CurrentGameTick, j.LastLandedGameTick, j.SpentGameTicks, j.MaximumGameTicks, j.ReturnReserveGameTicks,
            j.WorkGameTicks, j.TailGameTicks, j.JourneyDeadlineUtc.UtcDateTime.Ticks, D(j.AvailableLoadedEnergyJoules!.Value),
            D(j.LockedReturnFloorJoules!.Value), j.NativeWarperCount, j.OriginalExtraWarperReserve,
            D(f.Funding.CoreEnergyJoules!.Value), D(f.Funding.PerNativeTickEnergyJoules!.Value), D(f.Funding.StopWallPatienceSeconds));
    }

    private static bool ValidTarget(Vector3Snapshot? source, Vector3Snapshot? target)
    {
        if (!Finite(source) || !Finite(target)) return false;
        var sr = Length(source!); var tr = Length(target!);
        var dx = (double)source!.X - target!.X; var dy = (double)source.Y - target.Y; var dz = (double)source.Z - target.Z;
        var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return sr >= 1 && Math.Abs(sr - tr) <= 4 && distance >= 1
            && distance <= VoyagerSurfaceRecoveryPolicy.MaximumTargetDistanceMeters;
    }
    private static double Length(Vector3Snapshot p) => Math.Sqrt((double)p.X * p.X + (double)p.Y * p.Y + (double)p.Z * p.Z);
    private static bool Zero(Vector3Snapshot? p) => Finite(p) && p!.X == 0 && p.Y == 0 && p.Z == 0;
    private static bool PlayerHashMatches(PlayerStateSnapshot p)
    {
        try { return !string.IsNullOrWhiteSpace(p.StateHash) && p.StateHash == CanonicalStateHash.PlayerAction(p); }
        catch (Exception) { return false; }
    }
    private static string FailureFingerprint(NativeResumeFailureSnapshot failure)
    {
        var fields = new List<object?> { failure.Code, failure.FirstFailedPredicate, failure.Stage,
            failure.CapturedAtGameTick, failure.CapturedAtUtc, failure.FactsState };
        fields.Add(failure.Predicates.Count);
        foreach (var pair in failure.Predicates.OrderBy(p => p.Key, StringComparer.Ordinal))
        { fields.Add(pair.Key); fields.Add(pair.Value); }
        fields.Add(failure.Observations.Count);
        foreach (var pair in failure.Observations.OrderBy(p => p.Key, StringComparer.Ordinal))
        { fields.Add(pair.Key); fields.Add(pair.Value); }
        fields.Add(failure.Values.Count);
        foreach (var pair in failure.Values.OrderBy(p => p.Key, StringComparer.Ordinal))
        { fields.Add(pair.Key); fields.Add(pair.Value.HasValue ? D(pair.Value.Value) : null); }
        return CanonicalStateHash.Combine("same-session-original-move-failure-v1", fields.ToArray());
    }
    private static bool Finite(Vector3Snapshot? p) => p is not null && Finite(p.X) && Finite(p.Y) && Finite(p.Z);
    private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    private static bool Known(double? x) => x.HasValue && Finite(x.Value) && x.Value >= 0;
    private static bool KnownIfPresent(double? x) => !x.HasValue || Known(x);
    private static string F(float x) => x.ToString("R", CultureInfo.InvariantCulture);
    private static string D(double x) => x.ToString("R", CultureInfo.InvariantCulture);
    private static string? OptionalD(double? x) => x.HasValue ? D(x.Value) : null;
    private static string V(Vector3Snapshot p) => F(p.X) + "," + F(p.Y) + "," + F(p.Z);
}

public enum VoyagerSameSessionSurfaceObservationState { AwaitingFirstFrame, AwaitingSecondFrame, Bound, Rejected }

// Collects exactly two distinct paused render frames within the original
// finite observation window. This object owns no Native resources and cannot
// issue an order, clear quarantine, load a world, or resume simulation.
public sealed class VoyagerSameSessionSurfaceObservation
{
    private VoyagerSameSessionSurfaceSourceFacts? _first;
    private string? _firstFingerprint;
    public VoyagerSameSessionSurfaceObservationState State { get; private set; } = VoyagerSameSessionSurfaceObservationState.AwaitingFirstFrame;
    public VoyagerSameSessionSurfaceSourceBinding? Binding { get; private set; }
    public string? Rejection { get; private set; }

    public VoyagerSameSessionSurfaceObservationState Observe(VoyagerSameSessionSurfaceSourceFacts sample)
    {
        if (State is VoyagerSameSessionSurfaceObservationState.Bound or VoyagerSameSessionSurfaceObservationState.Rejected) return State;
        var blocker = VoyagerSameSessionSurfaceSourcePolicy.Blocker(sample);
        if (blocker is not null) return Reject(blocker);
        var fingerprint = VoyagerSameSessionSurfaceSourcePolicy.Fingerprint(sample);
        if (_first is null)
        {
            _first = sample; _firstFingerprint = fingerprint;
            return State = VoyagerSameSessionSurfaceObservationState.AwaitingSecondFrame;
        }
        if (VoyagerSameSessionSurfaceSourcePolicy.Blocker(_first) is not null
            || VoyagerSameSessionSurfaceSourcePolicy.Fingerprint(_first) != _firstFingerprint)
            return Reject("same_session_surface_first_sample_changed");
        if (sample.CapturedAtUtc < _first.CapturedAtUtc || sample.RenderFrame < _first.RenderFrame)
            return Reject("same_session_surface_observation_order_unproved");
        if ((sample.CapturedAtUtc - _first.CapturedAtUtc).TotalSeconds > VoyagerSurfaceRecoveryPolicy.MaximumInitialBindingWallSeconds)
            return Reject("same_session_surface_observation_expired");
        if (sample.RenderFrame == _first.RenderFrame)
            return fingerprint == _firstFingerprint ? State : Reject("same_session_surface_same_frame_changed");
        if (!VoyagerSameSessionSurfaceSourcePolicy.TryBind(_first, sample, out var binding, out var rejection)) return Reject(rejection);
        Binding = binding;
        return State = VoyagerSameSessionSurfaceObservationState.Bound;
    }

    public VoyagerSameSessionSurfaceObservationState Expire(DateTimeOffset now)
    {
        if (State == VoyagerSameSessionSurfaceObservationState.Rejected || _first is null) return State;
        if (now < _first.CapturedAtUtc || (now - _first.CapturedAtUtc).TotalSeconds > VoyagerSurfaceRecoveryPolicy.MaximumInitialBindingWallSeconds)
            return Reject("same_session_surface_observation_expired");
        return State;
    }

    public VoyagerSameSessionSurfaceObservationState Invalidate(string rejection)
    {
        if (State == VoyagerSameSessionSurfaceObservationState.Rejected) return State;
        return Reject(string.IsNullOrWhiteSpace(rejection) ? "same_session_surface_native_observation_unproved" : rejection);
    }

    private VoyagerSameSessionSurfaceObservationState Reject(string rejection)
    {
        Rejection ??= rejection; Binding = null;
        return State = VoyagerSameSessionSurfaceObservationState.Rejected;
    }
}
