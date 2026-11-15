using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
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

    [Fact]
    public void NativePreviewEntryOnlyDisclosesAStableSourceAndNeverCreatesACommitToken()
    {
        var oldPaused = GameMain.isPaused; var oldRunning = GameMain.isRunning;
        try
        {
            GameMain.isPaused = true; GameMain.isRunning = true;
            var refs = new VoyagerSameSessionSurfaceSourceTests.References { DeadlineUtc = DateTimeOffset.UtcNow.AddHours(2) }; var frame = 10;
            var coordinator = new NormalGameActionCoordinator();
            coordinator.ConfigureSurfacePreviewForTest(() => VoyagerSameSessionSurfaceSourceTests.Capture(refs, frame, DateTimeOffset.UtcNow));
            var sample = VoyagerSameSessionSurfaceSourceTests.Capture(refs, frame, DateTimeOffset.UtcNow);
            var request = new SameSessionSurfacePreviewRequest { PlanetId = 7, OriginalActionId = refs.ActionId, ExpectedRevision = 4,
                ExpectedPlayerStateHash = sample.PlayerState!.StateHash, Target = new Vector3Snapshot { X = 200, Y = 10 } };
            var first = coordinator.PreviewSameSessionSurfaceRecoveryOnMainThread("synthetic-session", request);
            Assert.True(first.Success); Assert.Equal("awaiting_second_frame", first.Value!.State); Assert.Null(first.Value.SourceFingerprint);
            Assert.True(first.Value.ReadOnly); Assert.False(first.Value.Executable); Assert.False(first.Value.CommitAllowedNow);
            Assert.False(first.Value.NativeResumePerformed); Assert.False(first.Value.OriginalOutcomeChanged);
            Assert.Null(typeof(SameSessionSurfacePreviewSnapshot).GetProperty("PlanToken"));
            frame++; coordinator.AdvanceSurfacePreviewForTest();
            var second = coordinator.PreviewSameSessionSurfaceRecoveryOnMainThread("synthetic-session", request);
            Assert.True(second.Success); Assert.Equal("source_bound", second.Value!.State);
            Assert.True(OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(second.Value.SourceFingerprint));
            Assert.Equal(refs.ActionId, second.Value.OriginalActionId); Assert.Equal("synthetic-session", second.Value.SessionId);
            Assert.Equal(7, second.Value.PlanetId); Assert.Equal(4, second.Value.Revision);
            Assert.Equal(request.ExpectedPlayerStateHash, second.Value.PlayerStateHash); Assert.False(second.Value.CommitAllowedNow);
            coordinator.StopSurfacePreviewForTest(); Assert.Equal(1, coordinator.SurfacePreviewReleasesForTest);
        }
        finally { GameMain.isPaused = oldPaused; GameMain.isRunning = oldRunning; }
    }

    [Theory]
    [InlineData("revision")] [InlineData("action")] [InlineData("hash")] [InlineData("target")]
    public void NativePreviewEntryRefusesWrongRequestBeforeAcquiringAnyLease(string fault)
    {
        var oldPaused = GameMain.isPaused; var oldRunning = GameMain.isRunning;
        try
        {
            GameMain.isPaused = true; GameMain.isRunning = true;
            var refs = new VoyagerSameSessionSurfaceSourceTests.References { DeadlineUtc = DateTimeOffset.UtcNow.AddHours(2) };
            var coordinator = new NormalGameActionCoordinator();
            coordinator.ConfigureSurfacePreviewForTest(() => VoyagerSameSessionSurfaceSourceTests.Capture(refs, 10, DateTimeOffset.UtcNow));
            var sample = VoyagerSameSessionSurfaceSourceTests.Capture(refs, 10, DateTimeOffset.UtcNow);
            var request = new SameSessionSurfacePreviewRequest { PlanetId = 7, OriginalActionId = refs.ActionId, ExpectedRevision = 4,
                ExpectedPlayerStateHash = sample.PlayerState!.StateHash, Target = new Vector3Snapshot { X = 200, Y = 10 } };
            if (fault == "revision") request.ExpectedRevision++;
            if (fault == "action") request.OriginalActionId = Guid.NewGuid().ToString();
            if (fault == "hash") request.ExpectedPlayerStateHash = "invalid";
            if (fault == "target") request.Target = null;
            Assert.False(coordinator.PreviewSameSessionSurfaceRecoveryOnMainThread("synthetic-session", request).Success);
            Assert.Equal(0, coordinator.SurfacePreviewOpensForTest); Assert.Equal(0, coordinator.SurfacePreviewReleasesForTest);
        }
        finally { GameMain.isPaused = oldPaused; GameMain.isRunning = oldRunning; }
    }

    [Fact]
    public void ExpiredOriginalWallReserveRefusesPreviewAndLeavesNativePaused()
    {
        var oldPaused = GameMain.isPaused; var oldRunning = GameMain.isRunning;
        try
        {
            GameMain.isPaused = true; GameMain.isRunning = true;
            var refs = new VoyagerSameSessionSurfaceSourceTests.References { DeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(30) };
            var coordinator = new NormalGameActionCoordinator();
            coordinator.ConfigureSurfacePreviewForTest(() => VoyagerSameSessionSurfaceSourceTests.Capture(refs, 10, DateTimeOffset.UtcNow));
            var sample = VoyagerSameSessionSurfaceSourceTests.Capture(refs, 10, DateTimeOffset.UtcNow);
            var request = new SameSessionSurfacePreviewRequest { PlanetId = 7, OriginalActionId = refs.ActionId, ExpectedRevision = 4,
                ExpectedPlayerStateHash = sample.PlayerState!.StateHash, Target = new Vector3Snapshot { X = 200, Y = 10 } };
            var result = coordinator.PreviewSameSessionSurfaceRecoveryOnMainThread("synthetic-session", request);
            Assert.False(result.Success); Assert.Equal("same_session_surface_original_wall_reserve_unproved", result.Error!.Message);
            Assert.True(GameMain.isPaused); Assert.Equal(1, coordinator.SurfacePreviewReleasesForTest);
        }
        finally { GameMain.isPaused = oldPaused; GameMain.isRunning = oldRunning; }
    }

    [Fact]
    public void ProposedWindowIsDisclosedButNeverAppliedOrExecuted()
    {
        var oldPaused = GameMain.isPaused; var oldRunning = GameMain.isRunning;
        try
        {
            GameMain.isPaused = true; GameMain.isRunning = true;
            var refs = new VoyagerSameSessionSurfaceSourceTests.References { DeadlineUtc = DateTimeOffset.UtcNow.AddHours(-1) }; var frame = 10;
            var coordinator = new NormalGameActionCoordinator();
            coordinator.ConfigureSurfacePreviewForTest(() => VoyagerSameSessionSurfaceSourceTests.Capture(refs, frame, DateTimeOffset.UtcNow));
            var sample = VoyagerSameSessionSurfaceSourceTests.Capture(refs, frame, DateTimeOffset.UtcNow);
            var proposed = DateTimeOffset.UtcNow.AddHours(1);
            var request = new SameSessionSurfacePreviewRequest { PlanetId = 7, OriginalActionId = refs.ActionId, ExpectedRevision = 4,
                ExpectedPlayerStateHash = sample.PlayerState!.StateHash, Target = new Vector3Snapshot { X = 200, Y = 10 },
                ProposedRecoveryDeadlineUtc = proposed };
            var first = coordinator.PreviewSameSessionSurfaceRecoveryOnMainThread("synthetic-session", request);
            Assert.True(first.Success); Assert.Equal("awaiting_second_frame", first.Value!.State);
            frame++; coordinator.AdvanceSurfacePreviewForTest();
            var second = coordinator.PreviewSameSessionSurfaceRecoveryOnMainThread("synthetic-session", request);
            Assert.True(second.Success); Assert.Equal("source_bound", second.Value!.State);
            Assert.Equal(refs.DeadlineUtc, second.Value.OriginalJourneyDeadlineUtc);
            Assert.Equal(refs.DeadlineUtc, second.Value.CurrentJourneyDeadlineUtc);
            Assert.Equal(proposed, second.Value.ProposedRecoveryDeadlineUtc);
            Assert.Equal(0, second.Value.AdditionalVoyagerGameTicks); Assert.False(second.Value.ProposedWindowApplied);
            Assert.False(second.Value.Executable); Assert.False(second.Value.NativeResumePerformed); Assert.True(GameMain.isPaused);
            coordinator.StopSurfacePreviewForTest(); Assert.Equal(1, coordinator.SurfacePreviewReleasesForTest);
        }
        finally { GameMain.isPaused = oldPaused; GameMain.isRunning = oldRunning; }
    }
}
