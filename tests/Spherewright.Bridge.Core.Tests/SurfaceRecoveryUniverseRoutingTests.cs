using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests
{
    // The real production entry is linked. Only the native/session shell is
    // substituted; this tests callback routing, not Unity contact or loading.
    public sealed class SurfaceRecoveryUniverseRoutingTests
    {
        [Fact]
        public void RealEntryRoutes151ToSurfaceControllerWithoutFabricatingLegacyFirstFrame()
        {
            using var tracker = new GameSessionTracker();
            var policy = new VoyagerSurfaceRecoveryPolicy(100, DateTimeOffset.UtcNow);
            var calls = 0;
            var adoption = tracker.BindUniverseObserverForTest(true, true, _ =>
            { calls++; return policy.ObserveUniverse(GameMain.gameTick, DateTimeOffset.UtcNow, true); });
            GameMain.gameTick = 101;
            Assert.True(tracker.ObserveRemoteResumeUniverseOnMainThread());
            Assert.Equal(1, calls); Assert.Equal(101L, policy.LastUniverseTick);
            Assert.False(adoption.FirstFrame.Armed);
            Assert.False(adoption.FirstFrame.UniverseObserved);
            Assert.False(adoption.FirstFrame.PhysicsObserved);
        }

        [Fact]
        public void SurfaceWorkKeeps151AfterLegacyPhysicsWouldHaveRetiredItsObservation()
        {
            using var tracker = new GameSessionTracker(); var calls = 0;
            var a = tracker.BindUniverseObserverForTest(true, true, _ => { calls++; return null; });
            Assert.Null(a.FirstFrame.Arm(100, true, true, true, true));
            Assert.Null(a.FirstFrame.ObserveUniverse(101, true, true, true));
            Assert.Null(a.FirstFrame.ObservePhysics(101, true));
            a.NativeContactPending = false; GameMain.gameTick = 102;
            Assert.True(tracker.ObserveRemoteResumeUniverseOnMainThread());
            Assert.Equal(1, calls);
        }

        [Fact]
        public void OrdinaryFirstFrameRetainsItsOriginal151AndRetirementSequence()
        {
            using var tracker = new GameSessionTracker(); var calls = 0;
            var a = tracker.BindUniverseObserverForTest(true, false, _ => { calls++; return null; });
            a.NativeContactPending = false;
            Assert.Null(a.FirstFrame.Arm(100, true, true, true, true));
            GameMain.gameTick = 101;
            Assert.True(tracker.ObserveRemoteResumeUniverseOnMainThread());
            Assert.Equal(1, calls);
            Assert.Null(a.FirstFrame.ObserveUniverse(101, true, true, true));
            Assert.Null(a.FirstFrame.ObservePhysics(101, true));
            GameMain.gameTick = 102;
            Assert.True(tracker.ObserveRemoteResumeUniverseOnMainThread());
            Assert.Equal(1, calls);
        }

        [Theory]
        [InlineData(false, true)][InlineData(true, false)]
        public void UnrelatedOrUnarmedOrdinaryRecoveryDoesNotAcquireASurfaceCallback(bool pending, bool surface)
        {
            using var tracker = new GameSessionTracker(); var calls = 0;
            tracker.BindUniverseObserverForTest(pending, surface, _ => { calls++; return null; });
            Assert.True(tracker.ObserveRemoteResumeUniverseOnMainThread());
            Assert.Equal(0, calls);
        }

        [Theory]
        [InlineData("callback")][InlineData("capture")][InlineData("previous_failure")]
        public void FailedSurfaceBindingNeverBecomes151Permission(string fault)
        {
            using var tracker = new GameSessionTracker(); var calls = 0;
            var a = tracker.BindUniverseObserverForTest(true, true, _ => { calls++; return "synthetic_surface_binding_failed"; });
            if (fault == "capture") tracker.UniverseCaptureErrorForTest = "synthetic_source_changed";
            if (fault == "previous_failure") a.FirstFrame.Fail("synthetic_original_failure");
            Assert.False(tracker.ObserveRemoteResumeUniverseOnMainThread());
            Assert.Equal(fault == "callback" ? 1 : 0, calls);
            Assert.NotNull(tracker.ResumeAdoptionError);
        }
    }
}

namespace Spherewright.Plugin.Game
{
    internal sealed partial class GameSessionTracker
    {
        private bool _universeObserverPendingForTest;
        private Func<VoyagerRemoteResumeAdoption, string?>? _remoteResumeUniverseGuard;
        internal string? UniverseCaptureErrorForTest;
        internal VoyagerRemoteResumeAdoption BindUniverseObserverForTest(bool pending, bool surface,
            Func<VoyagerRemoteResumeAdoption, string?> observer)
        {
            _universeObserverPendingForTest = pending;
            _remoteResumeUniverseGuard = observer;
            _remoteResumeAdoption = new() { SurfaceRecoveryPending = surface };
            return _remoteResumeAdoption;
        }
        private string? CaptureRemoteResumeAdoption(out VoyagerRemoteResumeAdoption? adoption)
        { adoption = _remoteResumeAdoption; return UniverseCaptureErrorForTest; }
    }
}
