using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

[Collection("Owned resume native substitutes")]
public sealed class SameSessionSurfacePreviewLifecycleTests
{
    [Fact]
    public void TwoFramesBindOneReadOnlyPreviewAndReleaseItsCaptureOnce()
    {
        var oldPaused = GameMain.isPaused; var oldRunning = GameMain.isRunning;
        try
        {
            GameMain.isPaused = true; GameMain.isRunning = true;
            var refs = new VoyagerSameSessionSurfaceSourceTests.References { DeadlineUtc = DateTimeOffset.UtcNow.AddHours(2) }; var frame = 10;
            var coordinator = new NormalGameActionCoordinator();
            coordinator.ConfigureSurfacePreviewForTest(() => VoyagerSameSessionSurfaceSourceTests.Capture(refs, frame, DateTimeOffset.UtcNow));
            Assert.True(coordinator.BeginSurfacePreviewForTest("original", out var rejection), rejection);
            coordinator.AdvanceSurfacePreviewForTest();
            Assert.Equal(VoyagerSameSessionSurfaceObservationState.AwaitingSecondFrame, coordinator.SurfacePreviewStateForTest);
            frame++;
            coordinator.AdvanceSurfacePreviewForTest();
            Assert.Equal(VoyagerSameSessionSurfaceObservationState.Bound, coordinator.SurfacePreviewStateForTest);
            Assert.True(coordinator.FreshSurfacePreviewForTest(out rejection), rejection);
            Assert.Equal(0, coordinator.SurfacePreviewReleasesForTest);
            coordinator.StopSurfacePreviewForTest(); coordinator.StopSurfacePreviewForTest();
            Assert.Equal(1, coordinator.SurfacePreviewReleasesForTest);
        }
        finally { GameMain.isPaused = oldPaused; GameMain.isRunning = oldRunning; }
    }

    [Theory]
    [InlineData("unpaused")] [InlineData("native_observation_lost")] [InlineData("changed_target")]
    public void LostNativeEvidenceInvalidatesAndReleasesTheReadOnlyPreview(string fault)
    {
        var oldPaused = GameMain.isPaused; var oldRunning = GameMain.isRunning;
        try
        {
            GameMain.isPaused = true; GameMain.isRunning = true;
            var refs = new VoyagerSameSessionSurfaceSourceTests.References { DeadlineUtc = DateTimeOffset.UtcNow.AddHours(2) }; var frame = 10; var lost = false;
            var coordinator = new NormalGameActionCoordinator();
            coordinator.ConfigureSurfacePreviewForTest(() =>
            {
                if (lost) return null;
                var sample = VoyagerSameSessionSurfaceSourceTests.Capture(refs, frame, DateTimeOffset.UtcNow);
                if (fault == "changed_target" && frame > 10) sample.Target!.Y = 11;
                return sample;
            });
            Assert.True(coordinator.BeginSurfacePreviewForTest("original", out var rejection), rejection);
            if (fault == "unpaused") GameMain.isPaused = false;
            if (fault == "native_observation_lost") lost = true;
            frame++; coordinator.AdvanceSurfacePreviewForTest();
            Assert.Equal(VoyagerSameSessionSurfaceObservationState.Rejected, coordinator.SurfacePreviewStateForTest);
            Assert.NotNull(coordinator.SurfacePreviewRejectionForTest); Assert.Equal(1, coordinator.SurfacePreviewReleasesForTest);
            Assert.False(coordinator.FreshSurfacePreviewForTest(out _));
        }
        finally { GameMain.isPaused = oldPaused; GameMain.isRunning = oldRunning; }
    }

    [Fact]
    public void ReleaseFailureDoesNotEscapeThePumpAndBlocksNewPreviews()
    {
        var oldPaused = GameMain.isPaused; var oldRunning = GameMain.isRunning;
        try
        {
            GameMain.isPaused = true; GameMain.isRunning = true;
            var refs = new VoyagerSameSessionSurfaceSourceTests.References { DeadlineUtc = DateTimeOffset.UtcNow.AddHours(2) };
            var coordinator = new NormalGameActionCoordinator();
            coordinator.ConfigureSurfacePreviewForTest(() => VoyagerSameSessionSurfaceSourceTests.Capture(refs, 10, DateTimeOffset.UtcNow), releaseFails: true);
            Assert.True(coordinator.BeginSurfacePreviewForTest("original", out var rejection), rejection);
            GameMain.isPaused = false; coordinator.AdvanceSurfacePreviewForTest();
            Assert.Equal(1, coordinator.SurfacePreviewReleasesForTest);
            GameMain.isPaused = true;
            Assert.False(coordinator.BeginSurfacePreviewForTest("another-original", out rejection));
            Assert.StartsWith("same_session_surface_preview_release_failed:", rejection);
            Assert.Equal(1, coordinator.SurfacePreviewReleasesForTest);
        }
        finally { GameMain.isPaused = oldPaused; GameMain.isRunning = oldRunning; }
    }

    [Fact]
    public void FirstCaptureFailureReportsAnyLeaseReleaseFailureImmediately()
    {
        var oldPaused = GameMain.isPaused; var oldRunning = GameMain.isRunning;
        try
        {
            GameMain.isPaused = true; GameMain.isRunning = true;
            var coordinator = new NormalGameActionCoordinator();
            coordinator.ConfigureSurfacePreviewForTest(() => null, releaseFails: true);
            Assert.False(coordinator.BeginSurfacePreviewForTest("original", out var rejection));
            Assert.StartsWith("same_session_surface_preview_release_failed:", rejection);
            Assert.Equal(1, coordinator.SurfacePreviewReleasesForTest);
            Assert.False(coordinator.BeginSurfacePreviewForTest("another-original", out rejection));
            Assert.StartsWith("same_session_surface_preview_release_failed:", rejection);
        }
        finally { GameMain.isPaused = oldPaused; GameMain.isRunning = oldRunning; }
    }
}
