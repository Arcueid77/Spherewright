using Newtonsoft.Json;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeTerrainFootHitObservationTests
{
    [Fact]
    public void CopiedCurrentAndAdoptionDetailsDoNotAliasListsOrWorldPose()
    {
        var source = Terrain();
        source.FootHits = new() { SurfaceHit(), new() { State = "observed", Type = "sphere", SameCurrentOcean = true,
            IsCurrentSurface = false, SameCurrentSphere = false, UnknownOwner = false, SharedMeshPresent = null } };
        source.FootHitDetailsComplete = true;
        var first = source.Copy(); var second = source.Copy();
        Assert.NotSame(source.FootHits, first.FootHits);
        Assert.NotSame(source.FootHits![0], first.FootHits![0]);
        Assert.NotSame(first.FootHits[0], second.FootHits![0]);
        first.FootHits[0].WorldPosition!.X = 17;
        first.FootHits[0].WorldRotation!.W = .5f;
        first.FootHits[0].WorldLossyScale!.Y = 3;
        first.FootHits[0].UniqueSurfaceIndex = 9;
        first.FootHits.RemoveAt(1);
        Assert.Equal(2, source.FootHits.Count);
        Assert.Equal(0, source.FootHits[0].WorldPosition!.X);
        Assert.Equal(1, source.FootHits[0].WorldRotation!.W);
        Assert.Equal(1, source.FootHits[0].WorldLossyScale!.Y);
        Assert.Equal(0, source.FootHits[0].UniqueSurfaceIndex);
        Assert.True(second.FootHits[1].SameCurrentOcean);
        Assert.Null(second.FootHits[1].SharedMeshPresent);
        Assert.Null(second.FootHits[1].UniqueSurfaceIndex);
        Assert.False(first.SafetyCertified);
    }

    [Fact]
    public void FootDiagnosticsDoNotChangePlayerOrPlayerActionHashButRealInventoryDoes()
    {
        var player = new PlayerStateSnapshot { SessionId = "synthetic-session", PlanetId = 1, IsAlive = true,
            Inventory = new() { new() { ItemId = 1001, Count = 2 } } };
        var before = CanonicalStateHash.Player(player); var action = CanonicalStateHash.PlayerAction(player);
        var terrain = Terrain(); terrain.FootHits = new() { SurfaceHit() }; terrain.FootHitDetailsComplete = true;
        player.NativePhysics = new() { Terrain = terrain, ResumeTerrainPreFirstFrame = terrain.Copy(),
            ResumeTerrainPostUniverse = terrain.Copy() };
        terrain.FootHits[0].UnknownOwner = true; terrain.FootHits[0].State = "unavailable";
        terrain.FootHits[0].ReasonType = "owner_outside_current_simulator"; terrain.FootHitDetailsComplete = false;
        Assert.Equal(before, CanonicalStateHash.Player(player)); Assert.Equal(action, CanonicalStateHash.PlayerAction(player));
        player.Inventory[0].Count++;
        Assert.NotEqual(before, CanonicalStateHash.Player(player)); Assert.NotEqual(action, CanonicalStateHash.PlayerAction(player));
    }

    [Theory]
    [InlineData("mixed")] [InlineData("foreign")] [InlineData("ocean")] [InlineData("partial")]
    [InlineData("truncated")] [InlineData("overflow")] [InlineData("unavailable")] [InlineData("nonfinite")]
    public void NewObservedDetailsCannotPromoteRejectedOriginalAggregate(string fault)
    {
        var terrain = Terrain();
        switch (fault)
        {
            case "mixed": terrain.FootOverlapCount = 2; terrain.FootOverlapsOwnedSurfaceOnly = false; break;
            case "foreign": terrain.FootOverlapsOwnedSurfaceOnly = false; break;
            case "ocean": terrain.FootOverlapsOwnedSurfaceOnly = false; break;
            case "partial": terrain.FootOverlapComplete = null; break;
            case "truncated": terrain.Truncated = true; break;
            case "overflow": terrain.FootOverlapCount = 257; terrain.FootOverlapComplete = false; break;
            case "unavailable": terrain.State = "unavailable"; break;
            case "nonfinite": terrain.SurfaceColliders![0].WorldPosition!.X = float.NaN; break;
        }
        var expected = NativeTerrainObservationPolicy.FootBlocker(terrain);
        Assert.NotNull(expected);
        terrain.FootHits = new() { SurfaceHit() }; terrain.FootHitDetailsComplete = true;
        if (fault is "mixed" or "ocean") terrain.FootHits.Add(new() { State = "observed", Type = "sphere",
            SameCurrentOcean = true, IsCurrentSurface = false, SameCurrentSphere = false, UnknownOwner = false });
        if (fault == "foreign") terrain.FootHits[0].UnknownOwner = true;
        Assert.Equal(expected, NativeTerrainObservationPolicy.FootBlocker(terrain));
        Assert.False(terrain.SafetyCertified);
    }

    [Fact]
    public void DetailsCannotInventMissingAggregateProofOrBecomeAnExtraAdmissionGate()
    {
        var terrain = Terrain();
        Assert.Null(terrain.FootHits); Assert.Null(terrain.FootHitDetailsComplete);
        Assert.Null(NativeTerrainObservationPolicy.FootBlocker(terrain));
        terrain.FootHitDetailsComplete = false;
        terrain.FootHits = new() { new() { ReasonType = "non_unique_current_surface", IsCurrentSurface = true } };
        Assert.Null(NativeTerrainObservationPolicy.FootBlocker(terrain));
        terrain.FootOverlapCount = null; terrain.FootOverlapComplete = null; terrain.FootOverlapsOwnedSurfaceOnly = null;
        terrain.FootHits = new() { SurfaceHit() }; terrain.FootHitDetailsComplete = true;
        Assert.NotNull(NativeTerrainObservationPolicy.FootBlocker(terrain));
    }

    [Fact]
    public void OldPayloadKeepsUnobservedDetailsUnknown()
    {
        var old = JsonConvert.DeserializeObject<NativePlanetTerrainPhysicsSnapshot>(
            "{\"state\":\"observed\",\"footOverlapCount\":2,\"footOverlapComplete\":true,\"footOverlapsOwnedSurfaceOnly\":false}")!;
        Assert.Null(old.FootHits); Assert.Null(old.FootHitDetailsComplete);
        Assert.False(old.SafetyCertified); Assert.NotNull(NativeTerrainObservationPolicy.FootBlocker(old));
    }

    private static NativeTerrainFootHitPhysicsSnapshot SurfaceHit() => new()
    {
        State = "observed", Type = "mesh", UniqueSurfaceIndex = 0, IsCurrentSurface = true,
        SameCurrentOcean = false, SameCurrentSphere = false, UnknownOwner = false, Enabled = true,
        ActiveInHierarchy = true, Layer = 31, IsTrigger = false, SharedMeshPresent = true, SameDefaultPhysicsScene = true,
        WorldPosition = new(), WorldRotation = Identity(), WorldLossyScale = One(),
    };
    private static NativePlanetTerrainPhysicsSnapshot Terrain() => new()
    {
        State = "observed", Complete = true, GuideComplete = true, RootLayer = 31, BodyLayer = 31,
        RootWorldPosition = new(), RootWorldRotation = Identity(), RootWorldLossyScale = One(),
        BodyWorldPosition = new(), BodyWorldRotation = Identity(), BodyWorldLossyScale = One(),
        NativeGroundSphereHit = false, FootOverlapComplete = true, FootOverlapsOwnedSurfaceOnly = true, FootOverlapCount = 1,
        SurfaceColliders = new() { new() { Type = "mesh", Enabled = true, ActiveInHierarchy = true, Layer = 31,
            IsTrigger = false, SharedMeshPresent = true, SameDefaultPhysicsScene = true,
            WorldPosition = new(), WorldRotation = Identity(), WorldLossyScale = One() } },
    };
    private static Vector3Snapshot One() => new() { X = 1, Y = 1, Z = 1 };
    private static QuaternionSnapshot Identity() => new() { W = 1 };
}
