using System.Diagnostics;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    // A read-only Native capture, not an action plan. Leases are bounded by
    // its owner and must be released on every refusal, expiry and completion.
    private sealed class SameSessionSurfaceCapture : IDisposable
    {
        public SameSessionSurfaceCapture(ActionRecord original, FirstFrameSurfaceState scene,
            NativeContactTransformBinding transform, Vector3 target, DateTimeOffset? proposedRecoveryDeadlineUtc, SameSessionSurfaceJourneyEvidence route,
            SameSessionSurfaceJournalEvidence journal)
        { Original = original; Scene = scene; Transform = transform; Target = target; ProposedRecoveryDeadlineUtc = proposedRecoveryDeadlineUtc; Route = route; Journal = journal; }
        public ActionRecord Original { get; }
        public FirstFrameSurfaceState Scene { get; }
        public NativeContactTransformBinding Transform { get; }
        public Vector3 Target { get; }
        public DateTimeOffset? ProposedRecoveryDeadlineUtc { get; }
        public SameSessionSurfaceJourneyEvidence Route { get; }
        public SameSessionSurfaceJournalEvidence Journal { get; }
        public bool IsDisposed { get; private set; }
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            try { Journal.Dispose(); } finally { Route.Dispose(); }
        }
    }

    private bool TryOpenSameSessionSurfaceCapture(string actionId, string expectedPlayerStateHash, Vector3 requestedTarget, DateTimeOffset? proposedRecoveryDeadlineUtc,
        out SameSessionSurfaceCapture? capture, out string rejection)
    {
        capture = null; rejection = "same_session_surface_original_context_unproved";
        SameSessionSurfaceJourneyEvidence? route = null; SameSessionSurfaceJournalEvidence? journal = null;
        try
        {
            var session = _sessions.CaptureOnMainThread();
            if (!GameMain.isRunning || !GameMain.isPaused || !VoyagerLocalPrePlayerTickAvailable
                || session.SessionId is null || session.SaveName is null || session.LocalPlanetId is not int planetId
                || session.WriteHealth != WriteHealthStates.Quarantined || session.WriteQuarantineActionId != actionId
                || !_actions.TryGetValue(actionId, out var original) || original is null
                || original.ActionKind != NormalActionKinds.Move || !original.Terminal || original.Succeeded
                || original.State != NormalActionStates.OutcomeUnknown || !VoyagerLocalReferencesMatch(original)
                || original.Plan.VoyagerLocalWalkScene?.MatchesCurrent() != true
                || original.Plan.VoyagerLocalBinding is not { } binding || original.Plan.VoyagerLocalJourneyId is not { } journeyId
                || _actions.ActiveValues.Any(a => !a.Terminal) || !IsFinite(requestedTarget.x)
                || !IsFinite(requestedTarget.y) || !IsFinite(requestedTarget.z) || requestedTarget.sqrMagnitude < 1f)
                return false;
            var player = binding.Player;
            if (player.controller?.rigid is null || player.transform is null
                || Math.Abs(requestedTarget.magnitude - player.position.magnitude) > 4f) return false;
            var state = _reader.GetPlayerStateOnMainThread(session.SessionId, new LocalPlanetRequest { PlanetId = planetId });
            if (!state.Success || state.Value is null || state.Value.StateHash != expectedPlayerStateHash) return false;
            var scene = CaptureNativeLocalWalkScene();
            if (scene is null || !scene.MatchesCurrent()) return false;
            var surface = new FirstFrameSurfaceState(scene, null);
            var transform = NativeContactTransformBinding.Capture(player.transform);
            if (!surface.Matches() || transform is null || !transform.Matches(player.transform)) return false;
            if (!_flightCheckpoints.TryOpenSameSessionSurfaceEvidence(session.SessionId, session.SaveName, planetId,
                journeyId, GameMain.gameTick, Process.GetCurrentProcess().Id, out route, out rejection) || route is null) return false;
            journal = _sessions.OpenQuiescentSurfaceJournalOnMainThread();
            var target = requestedTarget.normalized * player.position.magnitude;
            capture = new SameSessionSurfaceCapture(original, surface, transform, target, proposedRecoveryDeadlineUtc, route, journal);
            route = null; journal = null; rejection = string.Empty;
            return true;
        }
        catch (Exception ex)
        { rejection = "same_session_surface_capture_failed:" + ex.GetType().Name; return false; }
        finally
        {
            try { journal?.Dispose(); }
            catch (Exception ex) { _sameSessionSurfacePreviewReleaseFailure ??= "same_session_surface_preview_release_failed:" + ex.GetType().Name; }
            try { route?.Dispose(); }
            catch (Exception ex) { _sameSessionSurfacePreviewReleaseFailure ??= "same_session_surface_preview_release_failed:" + ex.GetType().Name; }
        }
    }

    private bool TryCaptureSameSessionSurfaceSource(SameSessionSurfaceCapture capture,
        out VoyagerSameSessionSurfaceSourceFacts? facts, out string rejection)
    {
        facts = null; rejection = "same_session_surface_capture_reference_changed";
        try
        {
            if (capture.IsDisposed || !_flightCheckpoints.SameSessionSurfaceEvidenceStillMatches(capture.Route)
                || capture.Journal.IsDisposed || !_actions.TryGetValue(capture.Original.ActionId, out var original)
                || !ReferenceEquals(original, capture.Original) || !VoyagerLocalReferencesMatch(original)
                || !capture.Scene.Matches() || !capture.Transform.Matches(original.Plan.VoyagerLocalBinding!.Player.transform)) return false;
            var session = _sessions.CaptureOnMainThread(); var player = original.Plan.VoyagerLocalBinding!.Player;
            if (session.SessionId is null || session.LocalPlanetId is not int planetId || player.controller?.rigid is null) return false;
            var read = _reader.GetPlayerStateOnMainThread(session.SessionId, new LocalPlanetRequest { PlanetId = planetId });
            var journey = _flightCheckpoints.VoyagerJourney;
            if (!read.Success || read.Value is null || journey is null || capture.Route.SourceGameTick != GameMain.gameTick) return false;
            var mode = NativeWalkModeBlocker(player, true, capture.Scene.Scene, true, true, true, allowNativePause: true);
            var noOrdersOrWork = player.currentOrder is null && player.orders.orderCount == 0 && BuildUiIsIdle(player)
                && VoyagerMechaWorkIdle(player)
                && TryObserveOwnedConstructionDrones(player.factory, player.mecha.constructionModule, out var dronesIdle, out _) && dronesIdle;
            var cost = EvaluateNativeLocalMovementCost(player, mode is null && noOrdersOrWork);
            var now = DateTimeOffset.UtcNow;
            var proof = new NativeResumeDecisionEvidence("same_session_surface_source", GameMain.gameTick, now, true);
            var up = player.position.normalized;
            var grounded = Physics.CheckSphere(player.position + up * .15f, .35f, 15873);
            // In an observed paused Drift there need not be land underfoot.
            // The exact Native scene, motion and cost bindings remain checked.
            var support = grounded && SurfaceRecoveryTerrain(player, capture.Scene, grounded, proof);
            var patch = SurfaceRecoveryLandingPatch(capture.Scene, capture.Target);
            var budget = new VoyagerLocalActionBudgetFacts
            {
                ExactRemoteBinding = VoyagerOwnedJourneyMatchesCurrentSession(journey, original.SessionId),
                NativePlayerIdle = false, CurrentGameTick = GameMain.gameTick, LastLandedGameTick = journey.LastLandedGameTick,
                SpentGameTicks = _sessions.EffectiveVoyagerSpentGameTicks(journey, journey.SpentGameTicks) ?? -1,
                MaximumGameTicks = _sessions.EffectiveVoyagerMaximumGameTicks(journey), ReturnReserveGameTicks = journey.ReturnReserveGameTicks,
                WorkGameTicks = 1, TailGameTicks = VoyagerLocalWalkReservePolicy.StopGameTicks, NowUtc = now,
                JourneyDeadlineUtc = _sessions.EffectiveVoyagerDeadline(journey), AvailableLoadedEnergyJoules = CalculateAvailableFlightEnergy(player.mecha),
                LockedReturnFloorJoules = journey.ReturnEnergyFloorJoules, NativeWarperCount = CountVoyagerWarpers(player.mecha.warpStorage),
                OriginalExtraWarperReserve = journey.ExtraWarperReserve,
            };
            facts = new VoyagerSameSessionSurfaceSourceFacts
            {
                World = original.Plan.VoyagerLocalBinding!.World, Player = player, Controller = player.controller,
                Rigidbody = player.controller.rigid, Factory = original.Plan.VoyagerLocalFactory,
                NativeSceneBinding = capture.Scene, NativeTransformBinding = capture.Transform, JourneyStore = _flightCheckpoints,
                OriginalActionRecord = original, JourneyId = journey.JourneyId, ExpectedJourneyId = original.Plan.VoyagerLocalJourneyId!,
                JourneySnapshotFingerprint = capture.Route.JourneySnapshotFingerprint,
                DurableEvidenceFingerprint = CanonicalStateHash.Combine("same-session-surface-durable-v1", capture.Route.RouteFingerprint, capture.Journal.Fingerprint),
                OriginalJourneyIssuedAtUtc = journey.IssuedAtUtc, OriginalJourneyDeadlineUtc = journey.DeadlineUtc,
                ProposedRecoveryDeadlineUtc = capture.ProposedRecoveryDeadlineUtc,
                Session = session, OriginalMove = CreateActionSnapshot(original), PlayerState = read.Value,
                RigidbodyVelocity = Snapshot(player.controller.rigid.velocity), RigidbodyAngularVelocity = Snapshot(player.controller.rigid.angularVelocity),
                Target = Snapshot(capture.Target), NativePaused = GameMain.isPaused, ExactNativeReferencesProved = true,
                SceneAndColliderOwnershipProved = capture.Scene.Matches() && mode is null,
                NativeTransformStillMatches = capture.Transform.Matches(player.transform), DurableEvidenceVerified = true,
                CurrentTerrainSupportProved = support, DryLandingPatchProved = patch,
                NoOtherActiveActions = !_actions.ActiveValues.Any(a => !a.Terminal), NativeInputIdle = LocalRawMovementInputIdle() && noOrdersOrWork,
                RenderFrame = Time.frameCount, CapturedAtUtc = now,
                Funding = new VoyagerSurfaceRecoveryFacts
                {
                    GameTick = GameMain.gameTick, NowUtc = now, CoreEnergyJoules = player.mecha.coreEnergy,
                    PerNativeTickEnergyJoules = cost.Proved ? cost.PerNativeTickEnergyJoules : null,
                    StopWallPatienceSeconds = VoyagerActiveMoveGroundGapPolicy.StopWallPatience(Time.fixedDeltaTime) ?? double.NaN,
                    OriginalJourney = budget,
                },
            };
            rejection = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            // A Native observation failure is never a successful source proof.
            rejection = "same_session_surface_native_capture_failed:" + ex.GetType().Name;
            return false;
        }
    }
}
