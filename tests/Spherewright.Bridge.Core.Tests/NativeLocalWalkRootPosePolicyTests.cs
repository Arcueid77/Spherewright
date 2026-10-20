using Newtonsoft.Json;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeLocalWalkRootPosePolicyTests
{
    private const float NativeResidual = 1f - 24f / 16777216f;

    [Fact]
    public void NativeNearUnitUniformContractionHasAnExactCopiedProof()
    {
        Assert.Equal(NativeResidual, NativeLocalWalkRootPosePolicy.UniformContractionScale(Pose(NativeResidual)));
        // Adjacent values at Native's own Vector3 comparison boundary, not a
        // new epsilon chosen to accept a particular observation.
        Assert.NotNull(NativeLocalWalkRootPosePolicy.UniformContractionScale(Pose(1f - 96f / 16777216f)));
        Assert.Null(NativeLocalWalkRootPosePolicy.UniformContractionScale(Pose(1f - 97f / 16777216f)));
    }

    [Theory]
    [InlineData(1f)] [InlineData(1.0000001f)] [InlineData(.9f)] [InlineData(0f)]
    [InlineData(-1f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public void NewBranchNeverAdmitsUnitExpansionCollapseReflectionOrNonfinite(float s) =>
        Assert.Null(NativeLocalWalkRootPosePolicy.UniformContractionScale(Pose(s)));

    [Theory]
    [InlineData("parent")] [InlineData("parent_unknown")] [InlineData("local_translation")]
    [InlineData("world_translation")] [InlineData("local_rotation")] [InlineData("world_rotation")]
    [InlineData("nonuniform")] [InlineData("world_scale")] [InlineData("matrix_missing")]
    [InlineData("matrix_short")] [InlineData("shear")] [InlineData("matrix_translation")]
    [InlineData("matrix_nonfinite")] [InlineData("matrix_perspective")] [InlineData("matrix_scale")]
    public void ApproximateUnitNeverHidesAChangedHierarchyPoseOrMatrix(string fault)
    {
        var pose = Pose(NativeResidual);
        switch (fault)
        {
            case "parent": pose.RootHasParent = true; break;
            case "parent_unknown": pose.RootHasParent = null; break;
            case "local_translation": pose.RootLocalPosition!.X = float.Epsilon; break;
            case "world_translation": pose.RootWorldPosition!.Z = float.Epsilon; break;
            case "local_rotation": pose.RootLocalRotation!.X = float.Epsilon; break;
            case "world_rotation": pose.RootWorldRotation!.W = -1f; break;
            case "nonuniform": pose.RootLocalScale!.Z = 1f; break;
            case "world_scale": pose.RootWorldLossyScale!.X = 1f; break;
            case "matrix_missing": pose.RootLocalToWorldMatrix = null; break;
            case "matrix_short": pose.RootLocalToWorldMatrix!.RemoveAt(15); break;
            case "shear": pose.RootLocalToWorldMatrix![1] = float.Epsilon; break;
            case "matrix_translation": pose.RootLocalToWorldMatrix![3] = float.Epsilon; break;
            case "matrix_nonfinite": pose.RootLocalToWorldMatrix![5] = float.NaN; break;
            case "matrix_perspective": pose.RootLocalToWorldMatrix![15] = NativeResidual; break;
            case "matrix_scale": pose.RootLocalToWorldMatrix![10] = 1f; break;
        }
        Assert.Null(NativeLocalWalkRootPosePolicy.UniformContractionScale(pose));
    }

    [Fact]
    public void LaterValidScaleIsStillDifferentFromTheCapturedBinding()
    {
        var first = NativeLocalWalkRootPosePolicy.UniformContractionScale(Pose(NativeResidual));
        var later = NativeLocalWalkRootPosePolicy.UniformContractionScale(Pose(1f - 25f / 16777216f));
        Assert.NotNull(later); Assert.NotEqual(first, later);
    }

    [Fact]
    public void OldPayloadUnknownsCannotBecomePoseProofOrExecutionAuthority()
    {
        var terrain = JsonConvert.DeserializeObject<NativePlanetTerrainPhysicsSnapshot>("{}")!;
        Assert.Null(terrain.RootHasParent); Assert.Null(terrain.RootLocalScale);
        Assert.Null(terrain.RootLocalPosition); Assert.Null(terrain.RootLocalRotation); Assert.Null(terrain.RootLocalToWorldMatrix);
        Assert.Null(NativeLocalWalkRootPosePolicy.UniformContractionScale(terrain)); Assert.False(terrain.SafetyCertified);
        Assert.Null(JsonConvert.DeserializeObject<VoyagerFlightReadback>("{}")!.FirstLandingHandoff);
    }

    [Fact]
    public void FirstHandoffAndRootPoseCopiesAreDetachedAndOutsideBothPlayerHashes()
    {
        var source = new VoyagerLandingHandoffReadback { CapturedAtGameTick = 10, Proved = false,
            BlockerCode = "remote_idle_scene_root_pose_unproved", Terrain = Pose(NativeResidual),
            GroundContact = new() { CheckSphereHit = false }, CoreEnergyJoules = 1, LoadedEnergyJoules = 2 };
        var first = source.Copy(); var second = source.Copy();
        first.Terrain!.RootLocalPosition!.X = 2; first.Terrain.RootLocalRotation!.W = 0;
        first.Terrain.RootLocalScale!.X = 3; first.Terrain.RootLocalToWorldMatrix![0] = 4;
        first.GroundContact!.CheckSphereHit = true;
        Assert.Equal(0, source.Terrain!.RootLocalPosition!.X); Assert.Equal(1, second.Terrain!.RootLocalRotation!.W);
        Assert.Equal(NativeResidual, source.Terrain.RootLocalScale!.X);
        Assert.Equal(NativeResidual, second.Terrain.RootLocalToWorldMatrix![0]); Assert.False(source.GroundContact!.CheckSphereHit);
        Assert.False(source.Proved); Assert.Equal(10, source.CapturedAtGameTick); Assert.False(source.Terrain.SafetyCertified);
        var p = new PlayerStateSnapshot(); var full = CanonicalStateHash.Player(p); var action = CanonicalStateHash.PlayerAction(p);
        p.NativePhysics = new() { Terrain = source.Terrain };
        Assert.Equal(full, CanonicalStateHash.Player(p)); Assert.Equal(action, CanonicalStateHash.PlayerAction(p));
        source.Terrain.RootLocalToWorldMatrix![0] = 7;
        Assert.Equal(full, CanonicalStateHash.Player(p)); Assert.Equal(action, CanonicalStateHash.PlayerAction(p));
    }

    private static NativePlanetTerrainPhysicsSnapshot Pose(float s) => new()
    {
        RootHasParent = false, RootLocalPosition = new(), RootWorldPosition = new(),
        RootLocalRotation = new() { W = 1 }, RootWorldRotation = new() { W = 1 },
        RootLocalScale = Scale(s), RootWorldLossyScale = Scale(s),
        RootLocalToWorldMatrix = new() { s, 0, 0, 0, 0, s, 0, 0, 0, 0, s, 0, 0, 0, 0, 1 },
    };
    private static Vector3Snapshot Scale(float s) => new() { X = s, Y = s, Z = s };
}
