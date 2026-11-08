using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Safety;

public static class MovementDiagnosticsPolicy
{
    public static MovementDiagnosticsSnapshot Capture(MovementGeometrySnapshot geometry, Vector3Snapshot target,
        double? arrivalTolerance)
    {
        var position = new MovementVector(geometry.Origin);
        var goal = new MovementVector(target);
        var dot = MovementGeometryMath.Clamp(MovementVector.Dot(position.Unit, goal.Unit), -1, 1);
        var tangent = Math.Acos(dot) * (position.Length + goal.Length) / 2;
        var result = new MovementDiagnosticsSnapshot
        {
            State = geometry.Complete ? "observed" : "partial",
            CapturedAtGameTick = geometry.CapturedAtGameTick, Position = geometry.Origin, EffectiveTarget = target,
            ArrivalToleranceMetres = arrivalTolerance, RemainingDistanceMetres = (position - goal).Length,
            TangentialRemainingDistanceMetres = tangent, RadialMismatchMetres = Math.Abs(position.Length - goal.Length),
            GeometryComplete = geometry.Complete, Blockers = geometry.Blockers.ToList(),
        };
        if (geometry.Body.Count > 0)
        {
            var direction = (goal - position.Unit * MovementVector.Dot(goal, position.Unit)).Unit;
            foreach (var collider in geometry.Colliders.Where(MovementGeometryMath.ValidCollider))
            {
                var clearance = MovementGeometryMath.BodyClearance(geometry, position, collider);
                if (clearance > .2) continue;
                result.Candidates.Add(new MovementObstructionCandidate
                {
                    NativeColliderId = collider.NativeColliderId, ObjectKind = collider.ObjectKind, ObjectId = collider.ObjectId,
                    EvidenceSource = collider.CurrentSceneProximityObserved ? "current_scene_proximity" : "native_pool_clearance_candidate",
                    BodyClearanceMetres = clearance,
                    InTargetDirection = MovementVector.Dot(new MovementVector(collider.Centre) - position, direction) > .05,
                });
            }
            result.Candidates = result.Candidates.OrderBy(candidate => candidate.BodyClearanceMetres).Take(32).ToList();
        }
        result.Hint = arrivalTolerance.HasValue && tangent <= arrivalTolerance && result.RadialMismatchMetres > arrivalTolerance
            ? "arrival_radius_mismatch" : !geometry.Complete ? "geometry_incomplete"
            : result.Candidates.Any(candidate => candidate.InTargetDirection) ? "possible_physical_obstruction" : "no_attributed_obstruction";
        return result;
    }
}
