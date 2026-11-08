using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Players;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameCallResult<PreparedNormalAction> PrepareWalkingGeometry(PrepareMoveRequest request,
        PlayerStateSnapshot playerState)
    {
        var player = GameMain.mainPlayer;
        var target = ToVector(request.Target);
        if (!IsFinite(target.x) || !IsFinite(target.y) || !IsFinite(target.z) || target.sqrMagnitude < 1
            || Math.Abs(target.magnitude - player.position.magnitude) > 8)
            return InvalidPlan("Walking preview target must be finite and on the current planet surface.");
        target = target.normalized * player.position.magnitude;
        if (Vector3.Distance(player.position, target) > WalkingRoutePlanner.MaximumGoalDistanceMetres)
            return InvalidPlan("Walking preview goal must be within16 metres of the current player.");
        var geometry = _reader.CaptureMovementGeometry(player, target, WalkingRoutePlanner.MaximumGoalDistanceMetres,
            playerState.StateHash, requireIdleWalk: true);
        if (request.ExpectedWalkingGeometryHash is not null && geometry.GeometryHash != request.ExpectedWalkingGeometryHash)
            return StalePlan("The exact walking actor, native physics geometry or factory layout changed between observations.");
        var preview = new WalkingRoutePreview
        {
            Geometry = geometry, State = geometry.Complete ? "geometry_observed" : "partial",
            SafetyMarginMetres = WalkingRoutePlanner.SafetyMarginMetres,
            Blockers = geometry.Blockers.ToList(),
        };
        if (request.WalkingPreviewPoints is not null && geometry.Complete)
        {
            preview.TerrainPoints = _reader.CaptureWalkingTerrainPoints(player, geometry, request.WalkingPreviewPoints);
            preview.State = "terrain_observed";
        }
        return GameCallResult<PreparedNormalAction>.Succeeded(new PreparedNormalAction
        {
            Prepared = true, ActionKind = NormalActionKinds.Move, CommitAllowedNow = false,
            WalkingRoutePreview = preview, ExpectedStateHash = geometry.GeometryHash,
            CompletionCondition = "Read-only bounded walking geometry and surface observations. No plan token, order or execution permission.",
        });
    }

    private void CaptureMovementFailureDiagnostics(ActionRecord action)
    {
        if (action.MovementDiagnostics is not null) return;
        var player = GameMain.mainPlayer;
        if (player is null || !_sessions.IsCurrentSessionOwned || _sessions.SessionId != action.SessionId
            || player.planetId != action.PlanetId)
        {
            action.MovementDiagnostics = new MovementDiagnosticsSnapshot
            {
                CapturedAtGameTick = GameMain.gameTick,
                Blockers = new() { "movement_diagnostic_binding_unavailable" },
            };
            return;
        }
        try
        {
            var target = action.HarvestApproach ?? action.Plan.TargetPosition;
            var geometry = _reader.CaptureMovementGeometry(player, target, 4, string.Empty, requireIdleWalk: false);
            action.MovementDiagnostics = MovementDiagnosticsPolicy.Capture(geometry, Snapshot(target),
                action.ActionKind == NormalActionKinds.Move ? action.Plan.ArrivalTolerance : null);
        }
        catch (Exception error)
        {
            action.MovementDiagnostics = new MovementDiagnosticsSnapshot
            {
                CapturedAtGameTick = GameMain.gameTick,
                Blockers = new() { "movement_diagnostic_" + error.GetType().Name },
            };
        }
    }
}
