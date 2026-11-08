using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;

namespace Spherewright.Bridge.Core.Safety;

public static class WalkingRoutePlanner
{
    public const double MaximumGoalDistanceMetres = 16;
    public const double MaximumRouteLengthMetres = 48;
    public const int MaximumNodes = 64;
    public const int MaximumWaypoints = 16;
    public const int MaximumColliders = 512;
    public const double SafetyMarginMetres = .15;
    public const double MaximumSurfaceSpacingMetres = 1;
    private const double CollisionSampleSpacing = .25;

    public static string? ValidateRequest(PrepareMoveRequest request)
    {
        if (!request.PreviewWalkingRoute)
            return request.WalkingPreviewPoints is null && request.ExpectedWalkingGeometryHash is null
                ? null : "Walking observation arguments require previewWalkingRoute=true.";
        if (request.AllowPassiveDrift) return "Walking route previews require an idle grounded Walk source.";
        if (request.WalkingPreviewPoints is null)
            return request.ExpectedWalkingGeometryHash is null ? null : "Terrain sampling requires explicit points.";
        if (request.WalkingPreviewPoints.Count < 1 || request.WalkingPreviewPoints.Count > MaximumNodes
            || request.WalkingPreviewPoints.Any(point => !MovementGeometryMath.ValidPoint(point))
            || string.IsNullOrEmpty(request.ExpectedWalkingGeometryHash))
            return "Walking terrain sampling requires1..64 finite points and the current geometry hash.";
        return null;
    }

    public static bool ConfirmsReadOnlyEcho(PreparedNormalAction? response, string sessionId, int planetId,
        Vector3Snapshot? requestedTarget = null)
    {
        var geometry = response?.WalkingRoutePreview?.Geometry;
        return response is not null && !response.CommitAllowedNow && string.IsNullOrEmpty(response.PlanToken)
            && response.WalkingRoutePreview is { EvidenceVersion: 1 }
            && geometry is { EvidenceVersion: 1 } && geometry.SessionId == sessionId && geometry.PlanetId == planetId
            && (requestedTarget is null || MovementGeometryMath.ValidPoint(requestedTarget)
                && MovementGeometryMath.ValidPoint(geometry.Origin) && MovementGeometryMath.ValidPoint(geometry.EffectiveTarget)
                && new MovementVector(requestedTarget).Length > 1
                && Math.Abs(new MovementVector(geometry.Origin).Length - new MovementVector(geometry.EffectiveTarget).Length) < .001
                && (new MovementVector(requestedTarget).Unit - new MovementVector(geometry.EffectiveTarget).Unit).Length
                    * new MovementVector(geometry.Origin).Length <= .01);
    }

    public static List<Vector3Snapshot> CandidatePoints(MovementGeometrySnapshot geometry, double arrivalTolerance = 1.5)
    {
        if (!ValidGeometry(geometry)) return new();
        var origin = new MovementVector(geometry.Origin);
        var target = new MovementVector(geometry.EffectiveTarget);
        var up = origin.Unit;
        var forward = (target - up * MovementVector.Dot(target, up) - origin
            + up * MovementVector.Dot(origin, up)).Unit;
        if (forward.Length < .5) forward = MovementVector.Cross(up, new MovementVector(0, 0, 1)).Unit;
        if (forward.Length < .5) forward = MovementVector.Cross(up, new MovementVector(1, 0, 0)).Unit;
        var side = MovementVector.Cross(up, forward).Unit;
        var result = new List<Vector3Snapshot> { geometry.Origin, geometry.EffectiveTarget };
        var length = (target - origin).Length;
        for (var distance = 4d; distance < length; distance += 4)
            Add(origin + forward * distance);
        var padding = geometry.Body.Max(shape => shape.Radius) + SafetyMarginMetres + .4 + arrivalTolerance;
        foreach (var collider in geometry.Colliders.OrderBy(value =>
            (new MovementVector(value.Centre) - (origin + target) / 2).LengthSquared))
        {
            if (result.Count + 4 > MaximumNodes) break;
            var centre = new MovementVector(collider.Centre) - origin;
            var boundsX = ProjectedExtent(collider, side) + padding;
            var boundsZ = ProjectedExtent(collider, forward) + padding;
            var x = MovementVector.Dot(centre, side);
            var z = MovementVector.Dot(centre, forward);
            foreach (var dx in new[] { -boundsX, boundsX })
                foreach (var dz in new[] { -boundsZ, boundsZ })
                    Add(origin + side * (x + dx) + forward * (z + dz));
        }
        return result;

        void Add(MovementVector position)
        {
            position = position.Unit * origin.Length;
            if ((position - origin).Length > MaximumGoalDistanceMetres
                || result.Any(value => (new MovementVector(value) - position).Length < .2)) return;
            result.Add(position.Snapshot());
        }
    }

    public static WalkingRoutePreview Plan(MovementGeometrySnapshot geometry, IReadOnlyList<WalkingTerrainPoint> points,
        double arrivalTolerance)
    {
        var result = new WalkingRoutePreview { Geometry = geometry, SafetyMarginMetres = SafetyMarginMetres };
        if (!ValidGeometry(geometry))
        {
            result.State = "partial";
            result.Blockers.AddRange(geometry.Blockers);
            return result;
        }
        if (points.Count < 2 || points.Count > MaximumNodes
            || points.Where((point, index) => point is null || point.Index != index
                || !MovementGeometryMath.ValidPoint(point.RequestedPosition)).Any()
            || (new MovementVector(points[0].RequestedPosition) - new MovementVector(geometry.Origin)).Length > .01
            || (new MovementVector(points[1].RequestedPosition) - new MovementVector(geometry.EffectiveTarget)).Length > .01)
        {
            result.Blockers.Add("walking_candidate_samples_mismatch");
            return result;
        }
        result.TerrainPoints = points.ToList();
        var nodes = points.Where(point => point.Observed && point.Dry && MovementGeometryMath.ValidPoint(point.Position)
            && point.GroundNormalDotUp.HasValue && MovementGeometryMath.Finite(point.GroundNormalDotUp.Value)
            && point.GroundNormalDotUp >= .5).ToDictionary(point => point.Index);
        if (!nodes.ContainsKey(0) || !nodes.ContainsKey(1))
        {
            result.State = "partial";
            result.Blockers.Add("walking_start_or_goal_surface_unavailable");
            return result;
        }
        var costs = nodes.Keys.ToDictionary(index => index, _ => double.PositiveInfinity);
        var previous = new Dictionary<int, int>();
        var visited = new HashSet<int>();
        var edges = new Dictionary<(int, int), double>();
        costs[0] = 0;
        while (visited.Count < nodes.Count)
        {
            var current = nodes.Keys.Where(index => !visited.Contains(index)).OrderBy(index => costs[index]).First();
            if (double.IsPositiveInfinity(costs[current]) || current == 1) break;
            visited.Add(current);
            foreach (var next in nodes.Keys.Where(index => !visited.Contains(index)))
            {
                var start = nodes[current].Position!;
                var end = nodes[next].Position!;
                if (Math.Abs(new MovementVector(start).Length - new MovementVector(end).Length) > Math.Max(.1, arrivalTolerance - .2))
                    continue;
                var key = current < next ? (current, next) : (next, current);
                if (!edges.TryGetValue(key, out var clearance))
                {
                    clearance = SegmentClearance(geometry, start, end);
                    edges[key] = clearance;
                }
                var startAllowance = current == 0 ? 0 : arrivalTolerance;
                if (clearance < SafetyMarginMetres + startAllowance) continue;
                var cost = costs[current] + (new MovementVector(start) - new MovementVector(end)).Length;
                if (cost >= costs[next] || cost > MaximumRouteLengthMetres) continue;
                costs[next] = cost;
                previous[next] = current;
            }
        }
        if (!previous.ContainsKey(1))
        {
            result.State = "search_exhausted";
            result.Blockers.Add("no_route_in_bounded_candidate_graph");
            return result;
        }
        var path = new List<int> { 1 };
        while (path[path.Count - 1] != 0)
        {
            path.Add(previous[path[path.Count - 1]]);
            if (path.Count > MaximumWaypoints + 1)
            {
                result.State = "search_exhausted";
                result.Blockers.Add("walking_waypoint_limit");
                return result;
            }
        }
        path.Reverse();
        result.Waypoints = path.Skip(1).Select(index => nodes[index].Position!).ToList();
        for (var index = 1; index < path.Count; index++)
        {
            var start = nodes[path[index - 1]].Position!;
            var end = nodes[path[index]].Position!;
            result.Segments.Add(new WalkingRouteSegment
            {
                Start = start, End = end, MinimumClearanceMetres = SegmentClearance(geometry, start, end),
                StartPositionAllowanceMetres = index == 1 ? 0 : arrivalTolerance,
                MaximumSampleSpacingMetres = CollisionSampleSpacing,
            });
        }
        result.LengthMetres = costs[1];
        result.State = "route_candidate";
        return result;
    }

    public static List<Vector3Snapshot> SurfaceSamples(WalkingRoutePreview preview)
    {
        var result = new List<Vector3Snapshot>();
        foreach (var segment in preview.Segments)
        {
            var start = new MovementVector(segment.Start);
            var end = new MovementVector(segment.End);
            var count = Math.Max(1, (int)Math.Ceiling((end - start).Length / MaximumSurfaceSpacingMetres));
            if (result.Count == 0) result.Add(segment.Start);
            for (var index = 1; index <= count; index++) result.Add(Interpolate(start, end, (double)index / count).Snapshot());
        }
        return result.Count <= MaximumNodes ? result : new();
    }

    public static void CompleteSurfaceObservation(WalkingRoutePreview preview, IReadOnlyList<WalkingTerrainPoint> samples)
    {
        var expected = SurfaceSamples(preview);
        if (preview.Geometry is null || !ValidGeometry(preview.Geometry) || samples.Count != expected.Count || samples.Count < 2
            || samples.Where((point, index) => point.Index != index || !point.Observed || !point.Dry
                || !MovementGeometryMath.ValidPoint(point.Position) || !point.GroundNormalDotUp.HasValue
                || !MovementGeometryMath.Finite(point.GroundNormalDotUp.Value) || point.GroundNormalDotUp < .5
                || (new MovementVector(point.RequestedPosition) - new MovementVector(expected[index])).Length > .01).Any())
        {
            preview.State = "partial";
            preview.Blockers.Add("walking_route_surface_incomplete_or_wet");
            return;
        }
        var minimum = 4d;
        var allowances = preview.Segments.SelectMany(segment => Enumerable.Repeat(segment.StartPositionAllowanceMetres,
            Math.Max(1, (int)Math.Ceiling((new MovementVector(segment.End) - new MovementVector(segment.Start)).Length
                / MaximumSurfaceSpacingMetres)))).ToArray();
        for (var index = 1; index < samples.Count; index++)
        {
            var a = samples[index - 1].Position!;
            var b = samples[index].Position!;
            var length = (new MovementVector(a) - new MovementVector(b)).Length;
            var height = Math.Abs(new MovementVector(a).Length - new MovementVector(b).Length);
            if (height > Math.Max(.1, length * .5))
            {
                preview.State = "blocked";
                preview.Blockers.Add("walking_surface_gradient_exceeds_preview_subset");
                return;
            }
            var clearance = SegmentClearance(preview.Geometry, a, b);
            minimum = Math.Min(minimum, clearance - allowances[index - 1]);
        }
        if (minimum < SafetyMarginMetres)
        {
            preview.State = "blocked";
            preview.Blockers.Add("walking_ground_adjustment_reduces_clearance");
            return;
        }
        preview.TerrainPoints = samples.ToList();
        preview.TerrainCoverageComplete = true;
        preview.State = "route_observed";
    }

    internal static bool ValidGeometry(MovementGeometrySnapshot geometry) => geometry.Complete && geometry.State == "observed"
        && MovementGeometryMath.ValidPoint(geometry.Origin) && MovementGeometryMath.ValidPoint(geometry.EffectiveTarget)
        && new MovementVector(geometry.Origin).Length >= 120
        && new MovementVector(geometry.Origin).Length <= 6100
        && (new MovementVector(geometry.Origin) - new MovementVector(geometry.EffectiveTarget)).Length <= MaximumGoalDistanceMetres + .05
        && MovementGeometryMath.Finite(geometry.RadiusMetres) && geometry.RadiusMetres > 0
        && geometry.RadiusMetres <= MaximumGoalDistanceMetres
        && geometry.Body is { Count: > 0 and <= 16 } && geometry.Colliders is not null && geometry.Colliders.Count <= MaximumColliders
        && geometry.Body.All(shape => shape is not null && MovementGeometryMath.Finite(shape.Radius) && shape.Radius > 0 && shape.Radius <= 4
            && MovementGeometryMath.Finite(shape.LowerCentreHeight) && MovementGeometryMath.Finite(shape.UpperCentreHeight)
            && shape.LowerCentreHeight <= shape.UpperCentreHeight && Math.Abs(shape.LowerCentreHeight) <= 8 && Math.Abs(shape.UpperCentreHeight) <= 8)
        && geometry.Colliders.All(MovementGeometryMath.ValidCollider);

    internal static double SegmentClearance(MovementGeometrySnapshot geometry, Vector3Snapshot source, Vector3Snapshot destination)
    {
        var start = new MovementVector(source);
        var end = new MovementVector(destination);
        var dot = MovementGeometryMath.Clamp(MovementVector.Dot(start.Unit, end.Unit), -1, 1);
        var lowerNorm = Math.Sqrt((1 + dot) / 2);
        if (lowerNorm < .9) return double.NegativeInfinity;
        var directionRate = (end.Unit - start.Unit).Length / lowerNorm;
        var speedBound = Math.Abs(end.Length - start.Length) + Math.Max(end.Length, start.Length) * directionRate;
        var count = Math.Max(1, (int)Math.Ceiling(speedBound / CollisionSampleSpacing));
        if (count > 256) return double.NegativeInfinity;
        var bodyHeight = geometry.Body.Max(shape => Math.Max(Math.Abs(shape.LowerCentreHeight), Math.Abs(shape.UpperCentreHeight)));
        var betweenSamples = (speedBound + bodyHeight * directionRate) / (2 * count) + .0001;
        var candidates = geometry.Colliders.Where(collider =>
        {
            var bound = collider.Shape == "sphere" ? collider.Radius
                : new MovementVector(collider.Extents).Length + collider.Radius;
            return MovementGeometryMath.SegmentPointDistance(start, end, new MovementVector(collider.Centre))
                <= bound + bodyHeight + 4;
        }).ToArray();
        var minimum = 4d;
        for (var index = 0; index <= count; index++)
        {
            var foot = Interpolate(start, end, (double)index / count);
            foreach (var collider in candidates)
                minimum = Math.Min(minimum, MovementGeometryMath.BodyClearance(geometry, foot, collider));
        }
        return minimum - betweenSamples;
    }

    private static MovementVector Interpolate(MovementVector start, MovementVector end, double t) =>
        (start.Unit * (1 - t) + end.Unit * t).Unit * (start.Length * (1 - t) + end.Length * t);

    private static double ProjectedExtent(WalkingColliderSnapshot collider, MovementVector axis)
    {
        var ext = new MovementVector(collider.Extents);
        if (collider.Shape == "sphere") return collider.Radius;
        if (collider.Shape == "capsule") return Math.Abs(MovementVector.Dot(axis, ext)) + collider.Radius;
        return Math.Abs(MovementVector.Dot(axis, MovementGeometryMath.Rotate(new(1, 0, 0), collider.Rotation))) * ext.X
            + Math.Abs(MovementVector.Dot(axis, MovementGeometryMath.Rotate(new(0, 1, 0), collider.Rotation))) * ext.Y
            + Math.Abs(MovementVector.Dot(axis, MovementGeometryMath.Rotate(new(0, 0, 1), collider.Rotation))) * ext.Z;
    }
}
