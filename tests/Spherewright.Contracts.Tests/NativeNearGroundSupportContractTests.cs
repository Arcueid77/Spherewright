using System.Text.Json;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Contracts.Tests;

public sealed class NativeNearGroundSupportContractTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void LegacyPhysicsDoesNotInventSupportOrAZeroDistance()
    {
        var old = JsonSerializer.Deserialize<NativePlayerPhysicsSnapshot>("{\"state\":\"observed\"}", Options)!;
        Assert.Null(old.ResumeNearSupportPreFirstFrame);
        Assert.Null(old.ResumeNearSupportPostUniverse);
        Assert.Null(old.ResumeNearSupportLatest);
        var support = new NativeNearGroundSupportSnapshot();
        Assert.Equal("unavailable", support.State);
        Assert.Null(support.RegisteredColliderCount);
        Assert.Null(support.Copy().RegisteredColliderCount);
        Assert.Null(support.ActualGroundSphereHit);
        Assert.Null(support.CastAttempted);
        Assert.Null(support.CastHit);
        Assert.Null(support.CastDistance);
        Assert.Null(support.NormalDotUp);
        Assert.Null(support.CurrentPoolReference);
        Assert.Null(support.LiveObject);
        Assert.Null(support.ColliderChain);
        Assert.Null(support.FootOverlapCount);
        Assert.Null(support.AllFootHitsCurrentSupportOrTerrain);
    }

    [Fact]
    public void NativeSupportCopyDetachesVectorsAndPreservesEveryObservation()
    {
        var source = new NativeNearGroundSupportSnapshot
        {
            State = "observed", ReasonCode = "synthetic-detail", CapturedAtGameTick = 12,
            RegisteredColliderCount = 19,
            CaptureStage = "synthetic-preflight", ActualGroundSphereHit = false,
            CastAttempted = true, CastHit = true, CastDistance = .24f, CastLayerMask = 1 << 31,
            HitPoint = new() { X = 1, Y = 2, Z = 3 }, HitNormal = new() { Y = 1 }, NormalDotUp = .9f,
            Layer = 10, Shape = "capsule", IsTrigger = false, Enabled = true, ActiveInHierarchy = true,
            SameDefaultPhysicsScene = true, OwnerType = "vegetable", ObjectId = 17,
            CurrentPoolReference = true, LiveObject = true, ColliderChain = true, NativeLiveFlag = false,
            FootOverlapCount = 0, FootOverlapComplete = true, AllFootHitsCurrentSupportOrTerrain = true,
            CurrentSupportFootHit = false, TerrainFootHit = false,
        };
        var copy = source.Copy();
        Assert.Equal(JsonSerializer.Serialize(source, Options), JsonSerializer.Serialize(copy, Options));
        Assert.NotSame(source.HitPoint, copy.HitPoint);
        Assert.NotSame(source.HitNormal, copy.HitNormal);
        source.HitPoint!.X = 90; source.HitNormal!.Y = 0; source.CastDistance = null;
        source.OwnerType = "changed"; source.NativeLiveFlag = true; source.RegisteredColliderCount = null;
        Assert.Equal(1, copy.HitPoint!.X);
        Assert.Equal(1, copy.HitNormal!.Y);
        Assert.Equal(.24f, copy.CastDistance);
        Assert.Equal(1 << 31, copy.CastLayerMask);
        Assert.Equal("vegetable", copy.OwnerType);
        Assert.False(copy.NativeLiveFlag);
        Assert.Equal(19, copy.RegisteredColliderCount);
        Assert.True(copy.LiveObject);
        Assert.False(copy.SafetyCertified);
    }

    [Fact]
    public void CastHitAndNativeLivenessFlagNeverClaimGroundOrSafety()
    {
        var value = JsonSerializer.Deserialize<NativeNearGroundSupportSnapshot>(
            "{\"state\":\"observed\",\"actualGroundSphereHit\":false,\"castHit\":true,\"castDistance\":0.22,"
            + "\"currentPoolReference\":true,\"liveObject\":true,\"nativeLiveFlag\":false,\"safetyCertified\":true}", Options)!;
        Assert.False(value.ActualGroundSphereHit);
        Assert.True(value.CastHit);
        Assert.True(value.LiveObject);
        Assert.False(value.NativeLiveFlag);
        Assert.Null(value.RegisteredColliderCount);
        Assert.False(value.SafetyCertified);
        Assert.False(value.Copy().SafetyCertified);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(4096)]
    public void RegisteredCountRoundTripsWithoutInventingOwnerOrGround(int count)
    {
        var observation = new NativeNearGroundSupportSnapshot { State = "observed", RegisteredColliderCount = count };
        var json = JsonSerializer.Serialize(observation, Options);
        var roundTrip = JsonSerializer.Deserialize<NativeNearGroundSupportSnapshot>(json, Options)!;
        Assert.Equal(count, roundTrip.RegisteredColliderCount);
        Assert.Equal(count, roundTrip.Copy().RegisteredColliderCount);
        Assert.Null(roundTrip.CurrentPoolReference);
        Assert.Null(roundTrip.LiveObject);
        Assert.Null(roundTrip.ActualGroundSphereHit);
        Assert.Null(roundTrip.CastHit);
        Assert.False(roundTrip.SafetyCertified);
    }

    [Fact]
    public void ThreeAdoptionStagesRoundTripAndDoNotAliasTheOriginal()
    {
        var first = new NativeNearGroundSupportSnapshot
        {
            State = "observed", CapturedAtGameTick = 20, CaptureStage = "pre-first-frame",
            ActualGroundSphereHit = false, CastAttempted = true, CastHit = true, CastDistance = .2f,
            HitPoint = new() { Y = 120 }, OwnerType = "vein", ObjectId = 23,
        };
        var post = first.Copy(); post.CaptureStage = "post-universe"; post.CapturedAtGameTick = 21;
        var latest = post.Copy(); latest.CaptureStage = "latest"; latest.ActualGroundSphereHit = true;
        latest.CastAttempted = false; latest.CastHit = null; latest.CastDistance = null;
        var player = new NativePlayerPhysicsSnapshot
        {
            ResumeNearSupportPreFirstFrame = first,
            ResumeNearSupportPostUniverse = post,
            ResumeNearSupportLatest = latest,
        };
        var json = JsonSerializer.Serialize(player, Options);
        var copied = JsonSerializer.Deserialize<NativePlayerPhysicsSnapshot>(json, Options)!;
        Assert.Equal(json, JsonSerializer.Serialize(copied, Options));
        first.HitPoint!.Y = 500;
        Assert.Equal(120, copied.ResumeNearSupportPreFirstFrame!.HitPoint!.Y);
        Assert.Equal(120, post.HitPoint!.Y);
        Assert.Equal(20, copied.ResumeNearSupportPreFirstFrame.CapturedAtGameTick);
        Assert.Equal(21, copied.ResumeNearSupportPostUniverse!.CapturedAtGameTick);
        Assert.True(copied.ResumeNearSupportLatest!.ActualGroundSphereHit);
        Assert.Null(copied.ResumeNearSupportLatest.CastDistance);
        Assert.All(typeof(NativeNearGroundSupportSnapshot).Assembly.GetReferencedAssemblies(),
            a => Assert.DoesNotContain("UnityEngine", a.Name!));
    }

    [Fact]
    public void StrictJsonRejectsNonfiniteQueryGeometry()
    {
        Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(new NativeNearGroundSupportSnapshot { CastDistance = float.NaN }, Options));
        Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(new NativeNearGroundSupportSnapshot { NormalDotUp = float.PositiveInfinity }, Options));
        Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(new NativeNearGroundSupportSnapshot { HitNormal = new() { X = float.NegativeInfinity } }, Options));
        Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(new NativeNearGroundSupportSnapshot { HitPoint = new() { Z = float.NaN } }, Options));
    }
}
