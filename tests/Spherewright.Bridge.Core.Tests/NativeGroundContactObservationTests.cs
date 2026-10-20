using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class NativeGroundContactObservationTests
{
    [Fact]
    public void NewGroundDetailsRemainOutsideBothV1PlayerHashes()
    {
        var player = new PlayerStateSnapshot(); var full = CanonicalStateHash.Player(player); var action = CanonicalStateHash.PlayerAction(player);
        var observation = new NativePlayerGroundContactSnapshot { CheckSphereHit = false, SphereCastHit = true, GroundDistance = .1f };
        player.NativePhysics = new NativePlayerPhysicsSnapshot { AutoSyncTransforms = false, GroundContact = observation,
            ResumeGroundContactPreSync = observation.Copy(), ResumeGroundContactPostSync = observation.Copy() };
        Assert.Equal(full, CanonicalStateHash.Player(player)); Assert.Equal(action, CanonicalStateHash.PlayerAction(player));
        observation.GroundDistance = 19; player.NativePhysics.ResumeGroundContactPostSync!.State = "unavailable";
        Assert.Equal(full, CanonicalStateHash.Player(player)); Assert.Equal(action, CanonicalStateHash.PlayerAction(player));
    }
    private static void Context(NativePlayerGroundContactSnapshot r)
    { r.AutoSyncTransforms = false; r.QueriesHitTriggers = true; r.CachedWalkIsGrounded = false; r.CachedWalkGroundDistance = 20f; }

    [Fact]
    public void SphereHitDoesNotCastAndNeverClaimsCachedGroundingOrSafety()
    {
        var r = NativeGroundContactObservationPolicy.Capture(1, "test", Context, () => true,
            () => throw new InvalidOperationException("must not cast"));
        Assert.Equal("observed", r.State); Assert.True(r.CheckSphereHit); Assert.False(r.SphereCastAttempted);
        Assert.Null(r.SphereCastHit); Assert.Equal(0f, r.GroundDistance); Assert.False(r.SafetyCertified);
        Assert.False(r.CachedWalkIsGrounded); Assert.Equal(20f, r.CachedWalkGroundDistance);
    }
    [Fact]
    public void CastMissIsUnknownDistanceNotTheNativeTwentyMetreSentinel()
    {
        var r = NativeGroundContactObservationPolicy.Capture(1, "test", Context, () => false, () => (false, 0f, null));
        Assert.Equal("observed", r.State); Assert.True(r.SphereCastAttempted); Assert.False(r.SphereCastHit);
        Assert.Null(r.GroundDistance); Assert.Equal(20f, r.SearchDistance); Assert.Null(r.HitCollider);
    }
    [Fact]
    public void CastHitCopiesFiniteDistanceAndDetachedMetadata()
    {
        var r = NativeGroundContactObservationPolicy.Capture(1, "before", Context, () => false,
            () => (true, .125f, new NativePlayerGroundColliderSnapshot { Type = "mesh", Layer = 13 }));
        Assert.Equal(.125f, r.GroundDistance); var copy = r.Copy(); copy.HitCollider!.Layer = 0;
        Assert.Equal(13, r.HitCollider!.Layer); Assert.Equal("before", copy.CaptureStage);
    }
    [Theory]
    [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)] [InlineData(-1f)] [InlineData(20.01f)]
    public void InvalidDistanceNeverBecomesZeroOrCertified(float d)
    {
        var r = NativeGroundContactObservationPolicy.Capture(1, "test", Context, () => false,
            () => (true, d, new NativePlayerGroundColliderSnapshot { Type = "mesh" }));
        Assert.Equal("unavailable", r.State); Assert.Null(r.GroundDistance); Assert.False(r.SafetyCertified);
    }
    [Fact]
    public void MissingContextOrExceptionRemainsUnknownAndDoesNotLeakExceptionText()
    {
        var calls = 0;
        var missing = NativeGroundContactObservationPolicy.Capture(1, "test", _ => { }, () => { calls++; return false; }, () => (false, 0f, null));
        Assert.Equal(0, calls); Assert.Null(missing.CheckSphereHit); Assert.Equal("unavailable", missing.State);
        var failed = NativeGroundContactObservationPolicy.Capture(1, "test", Context,
            () => throw new InvalidOperationException("private diagnostic text"), () => (false, 0f, null));
        Assert.Null(failed.CheckSphereHit); Assert.Equal("native_ground_query_failed:InvalidOperationException", failed.ReasonCode);
    }
    [Theory]
    [InlineData(false, "remote_idle_ground_contact_unproved")]
    [InlineData(true, "remote_idle_scene_unproved")]
    [InlineData(true, null)]
    public void SyncRequiresTheExactLoadBoundaryAndSoleGroundBlocker(bool exact, string? blocker)
    {
        var calls = 0; var once = new NativeLoadedTransformSyncAttempt();
        Assert.NotNull(once.TrySynchronize(blocker, exact, () => true, () => calls++, () => true));
        Assert.Equal(0, calls); Assert.False(once.Attempted);
    }
    [Fact]
    public void AttemptIsRecordedBeforeTheCallAndAThrowCannotBeRetried()
    {
        var once = new NativeLoadedTransformSyncAttempt(); var calls = 0;
        var reason = once.TrySynchronize("remote_idle_ground_contact_unproved", true, () => true,
            () => { Assert.True(once.Attempted); calls++; throw new IOException("private"); }, () => true);
        Assert.Equal("remote_resume_ground_sync_failed:IOException", reason);
        Assert.NotNull(once.TrySynchronize("remote_idle_ground_contact_unproved", true, () => true, () => calls++, () => true));
        Assert.Equal(1, calls);
    }
    [Theory]
    [InlineData(false, true, 0)] [InlineData(true, false, 1)]
    public void ChangedStateOrProofFailsClosed(bool before, bool after, int expectedCalls)
    {
        var calls = 0; var once = new NativeLoadedTransformSyncAttempt();
        Assert.NotNull(once.TrySynchronize("remote_idle_ground_contact_unproved", true, () => before, () => calls++, () => after));
        Assert.Equal(expectedCalls, calls);
    }
}
