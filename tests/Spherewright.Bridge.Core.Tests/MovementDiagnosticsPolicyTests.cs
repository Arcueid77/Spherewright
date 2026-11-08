using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class MovementDiagnosticsPolicyTests
{
    [Fact]
    public void RadiusMismatchHasItsOwnHintWithoutInventingAnObstacle()
    {
        var geometry = WalkingRoutePlannerTests.Geometry();
        var result = MovementDiagnosticsPolicy.Capture(geometry, new() { Y = 202 }, 1.5);
        Assert.Equal("arrival_radius_mismatch", result.Hint);
        Assert.Equal(2, result.RadialMismatchMetres);
        Assert.Equal(0, result.TangentialRemainingDistanceMetres);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void ObservedSceneProximityAndPoolCandidatesKeepTheirDifferentEvidenceSources()
    {
        var geometry = WalkingRoutePlannerTests.Geometry();
        geometry.Colliders.Add(new()
        {
            NativeColliderId = 7, ObjectId = 91, ObjectKind = "entity", Shape = "box",
            Centre = new() { Y = 201.5f, Z = .4f }, Extents = new() { X = 1, Y = 1.5f, Z = .2f },
            Rotation = new() { W = 1 }, CurrentSceneProximityObserved = true,
        });
        var result = MovementDiagnosticsPolicy.Capture(geometry, geometry.EffectiveTarget, 1.5);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(91, candidate.ObjectId);
        Assert.Equal("current_scene_proximity", candidate.EvidenceSource);
        Assert.True(candidate.InTargetDirection);
        Assert.Equal("possible_physical_obstruction", result.Hint);
        geometry.Colliders[0].CurrentSceneProximityObserved = false;
        candidate = Assert.Single(MovementDiagnosticsPolicy.Capture(geometry, geometry.EffectiveTarget, 1.5).Candidates);
        Assert.Equal("native_pool_clearance_candidate", candidate.EvidenceSource);
    }

    [Fact]
    public void MissingGeometryRemainsAnExplicitIncompleteObservation()
    {
        var geometry = WalkingRoutePlannerTests.Geometry();
        geometry.Complete = false;
        geometry.Blockers.Add("synthetic-missing-owner");
        var result = MovementDiagnosticsPolicy.Capture(geometry, geometry.EffectiveTarget, 1.5);
        Assert.False(result.GeometryComplete);
        Assert.Equal("geometry_incomplete", result.Hint);
        Assert.Contains("synthetic-missing-owner", result.Blockers);
    }
}
