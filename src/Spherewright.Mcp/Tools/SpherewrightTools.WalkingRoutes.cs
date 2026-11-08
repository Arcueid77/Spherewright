using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Mcp.BridgeClient;

namespace Spherewright.Mcp.Tools;

public static partial class SpherewrightTools
{
    private static async Task<BridgeCallResult<PreparedNormalAction>> ObserveWalkingRouteAsync(IBridgeClient bridge,
        string sessionId, PrepareMoveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var first = Verify(await bridge.PrepareMoveAsync(sessionId, request, cancellationToken).ConfigureAwait(false));
            if (!first.Success || first.Value?.WalkingRoutePreview?.Geometry is not { Complete: true } geometry)
                return first;
            var points = WalkingRoutePlanner.CandidatePoints(geometry, request.ArrivalTolerance);
            if (points.Count < 2)
            {
                first.Value.WalkingRoutePreview.State = "partial";
                first.Value.WalkingRoutePreview.Blockers.Add("walking_candidate_geometry_unavailable");
                return first;
            }
            var second = await Sample(points, geometry.GeometryHash).ConfigureAwait(false);
            if (!second.Success || second.Value?.WalkingRoutePreview?.Geometry is not { } observed)
                return second;
            var candidate = WalkingRoutePlanner.Plan(observed, second.Value.WalkingRoutePreview.TerrainPoints,
                request.ArrivalTolerance);
            second.Value.WalkingRoutePreview = candidate;
            if (candidate.State != "route_candidate") return second;
            var surface = WalkingRoutePlanner.SurfaceSamples(candidate);
            if (surface.Count < 2)
            {
                candidate.State = "partial";
                candidate.Blockers.Add("walking_surface_sample_budget");
                return second;
            }
            var third = await Sample(surface, observed.GeometryHash).ConfigureAwait(false);
            if (!third.Success || third.Value?.WalkingRoutePreview?.Geometry is not { } finalGeometry)
                return third;
            candidate.Geometry = finalGeometry;
            candidate.Blockers.AddRange(finalGeometry.Blockers);
            WalkingRoutePlanner.CompleteSurfaceObservation(candidate, third.Value.WalkingRoutePreview.TerrainPoints);
            third.Value.WalkingRoutePreview = candidate;
            return third;
        }
        catch (Exception error) when (error is ArgumentException || error is InvalidOperationException
            || error is ArithmeticException || error is NullReferenceException)
        {
            return BridgeCallResult<PreparedNormalAction>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The bounded walking observation could not be validated: " + error.GetType().Name,
                false, "Fresh-read the player and use a coherent Plugin/MCP observation cohort."));
        }

        BridgeCallResult<PreparedNormalAction> Verify(BridgeCallResult<PreparedNormalAction> value)
        {
            if (!value.Success || WalkingRoutePlanner.ConfirmsReadOnlyEcho(value.Value, sessionId, request.PlanetId, request.Target))
                return value;
            return BridgeCallResult<PreparedNormalAction>.Failed(BridgeError.Create(BridgeErrorCodes.BridgeNotReady,
                "The Plugin did not confirm a tokenless read-only walking observation.", false,
                "Use matching Plugin/MCP files and a fresh source observation."));
        }

        async Task<BridgeCallResult<PreparedNormalAction>> Sample(List<Vector3Snapshot> points, string geometryHash)
        {
            var next = new PrepareMoveRequest
            {
                PlanetId = request.PlanetId, Target = request.Target, ArrivalTolerance = request.ArrivalTolerance,
                ExpectedPlayerStateHash = request.ExpectedPlayerStateHash, StateHashVersion = request.StateHashVersion,
                PreviewWalkingRoute = true, ExpectedWalkingGeometryHash = geometryHash, WalkingPreviewPoints = points,
            };
            return Verify(await bridge.PrepareMoveAsync(sessionId, next, cancellationToken).ConfigureAwait(false));
        }
    }
}
