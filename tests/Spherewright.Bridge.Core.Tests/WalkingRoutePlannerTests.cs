using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class WalkingRoutePlannerTests
{
    [Fact]
    public void EmptyCompleteCorridorHasFiniteReadOnlyRouteAndSurfaceEvidence()
    {
        var geometry = Geometry();
        var route = Plan(geometry);
        Assert.Equal("route_candidate", route.State);
        Assert.False(route.Executable);
        Assert.Single(route.Waypoints);
        Assert.All(route.Segments, segment => Assert.True(double.IsFinite(segment.MinimumClearanceMetres!.Value)));
        var surface = Samples(WalkingRoutePlanner.SurfaceSamples(route));
        WalkingRoutePlanner.CompleteSurfaceObservation(route, surface);
        Assert.Equal("route_observed", route.State);
        Assert.True(route.TerrainCoverageComplete);
    }

    [Theory]
    [InlineData("box")]
    [InlineData("sphere")]
    [InlineData("capsule")]
    public void KnownPrimitiveObstacleProducesARealDetourCandidate(string shape)
    {
        var geometry = Geometry();
        var obstacle = Box(0, 4, 1, 1);
        obstacle.Shape = shape;
        obstacle.Radius = shape == "box" ? 0 : .3;
        if (shape != "box") obstacle.Extents = new() { Y = shape == "capsule" ? 1 : 0 };
        geometry.Colliders.Add(obstacle);
        var route = Plan(geometry);
        Assert.Equal("route_candidate", route.State);
        Assert.True(route.Waypoints.Count > 1);
        Assert.All(route.Segments, segment => Assert.True(segment.MinimumClearanceMetres >= WalkingRoutePlanner.SafetyMarginMetres));
        WalkingRoutePlanner.CompleteSurfaceObservation(route, Samples(WalkingRoutePlanner.SurfaceSamples(route)));
        Assert.Equal("route_observed", route.State);
    }

    [Fact]
    public void ThinObstacleBetweenSurfaceSamplesStillBlocksTheDirectEdge()
    {
        var geometry = Geometry();
        geometry.Colliders.Add(Box(0, 4.125f, .02f, .0001f));
        var route = Plan(geometry);
        Assert.Equal("route_candidate", route.State);
        Assert.True(route.Waypoints.Count > 1);
    }

    [Theory]
    [InlineData(.8f, false)]
    [InlineData(1.6f, true)]
    public void CompoundActorWidthControlsNarrowCorridorAdmission(float gap, bool expectedRoute)
    {
        var geometry = Geometry();
        geometry.Colliders.Add(Box(-gap / 2 - 1, 4, 1, 8));
        geometry.Colliders.Add(Box(gap / 2 + 1, 4, 1, 8));
        var route = Plan(geometry);
        Assert.Equal(expectedRoute, route.State == "route_candidate");
    }

    [Fact]
    public void OverheadGeometryDoesNotBecomeAFalseHorizontalWall()
    {
        var geometry = Geometry();
        var overhead = Box(0, 4, 2, 2);
        overhead.Centre.Y = 207;
        overhead.Extents.Y = 1;
        geometry.Colliders.Add(overhead);
        Assert.Single(Plan(geometry).Waypoints);
    }

    [Fact]
    public void PartialGeometryAndMissingSurfaceNormalStayPartial()
    {
        var geometry = Geometry();
        geometry.Complete = false;
        Assert.Empty(WalkingRoutePlanner.CandidatePoints(geometry));
        Assert.Equal("partial", WalkingRoutePlanner.Plan(geometry, Array.Empty<WalkingTerrainPoint>(), 1.5).State);
        geometry.Complete = true;
        var route = Plan(geometry);
        var samples = Samples(WalkingRoutePlanner.SurfaceSamples(route));
        samples[1].GroundNormalDotUp = null;
        WalkingRoutePlanner.CompleteSurfaceObservation(route, samples);
        Assert.Equal("partial", route.State);
        Assert.False(route.TerrainCoverageComplete);
    }

    [Fact]
    public void WetAndNonfiniteSurfaceNeverBecomeObservedRoute()
    {
        foreach (var nonfinite in new[] { false, true })
        {
            var route = Plan(Geometry());
            var samples = Samples(WalkingRoutePlanner.SurfaceSamples(route));
            if (nonfinite) samples[1].Position!.X = float.NaN;
            else samples[1].Dry = false;
            WalkingRoutePlanner.CompleteSurfaceObservation(route, samples);
            Assert.NotEqual("route_observed", route.State);
        }
    }

    [Fact]
    public void WrongDestinationAndAnyCommitCapabilityFailTheReadOnlyEcho()
    {
        var response = new PreparedNormalAction { WalkingRoutePreview = new() { Geometry = Geometry() } };
        var target = response.WalkingRoutePreview.Geometry!.EffectiveTarget;
        Assert.True(WalkingRoutePlanner.ConfirmsReadOnlyEcho(response, "synthetic-session", 104, target));
        Assert.False(WalkingRoutePlanner.ConfirmsReadOnlyEcho(response, "synthetic-session", 104, new() { X = 8, Y = 200 }));
        response.PlanToken = "synthetic-token";
        Assert.False(WalkingRoutePlanner.ConfirmsReadOnlyEcho(response, "synthetic-session", 104, target));
        response.PlanToken = string.Empty;
        response.CommitAllowedNow = true;
        Assert.False(WalkingRoutePlanner.ConfirmsReadOnlyEcho(response, "synthetic-session", 104, target));
    }

    internal static MovementGeometrySnapshot Geometry() => new()
    {
        State = "observed", Complete = true, SessionId = "synthetic-session", PlanetId = 104,
        GeometryHash = "synthetic-geometry", Origin = new() { Y = 200 },
        EffectiveTarget = Surface(0, 8), RadiusMetres = 16,
        Body = new()
        {
            new() { LowerCentreHeight = .45, UpperCentreHeight = 2.55, Radius = .45 },
            new() { LowerCentreHeight = .28, UpperCentreHeight = .28, Radius = .3 },
        },
    };

    internal static List<WalkingTerrainPoint> Samples(IReadOnlyList<Vector3Snapshot> positions) => positions
        .Select((position, index) => new WalkingTerrainPoint
        {
            Index = index, RequestedPosition = position, Position = position,
            Observed = true, Dry = true, GroundNormalDotUp = 1, GroundRadiusMetres = 200,
        }).ToList();

    private static WalkingRoutePreview Plan(MovementGeometrySnapshot geometry) =>
        WalkingRoutePlanner.Plan(geometry, Samples(WalkingRoutePlanner.CandidatePoints(geometry)), 1.5);

    private static WalkingColliderSnapshot Box(float x, float z, float width, float depth) => new()
    {
        NativeColliderId = 1, ObjectKind = "entity", ObjectId = 100,
        Shape = "box", Centre = new() { X = x, Y = 201.5f, Z = z },
        Extents = new() { X = width, Y = 1.5f, Z = depth }, Rotation = new() { W = 1 },
    };

    private static Vector3Snapshot Surface(float x, float z)
    {
        var length = Math.Sqrt(x * x + 200d * 200 + z * z);
        return new() { X = (float)(x * 200 / length), Y = (float)(200d * 200 / length), Z = (float)(z * 200 / length) };
    }
}
