using Spherewright.Bridge.Core.Safety;
using UnityEngine;

namespace Spherewright.Plugin.Game;

// Test-only Native substitutes. The production preview lifecycle is linked;
// no Unity physics, real file lease, game Resume or Move is executed here.
internal sealed partial class NormalGameActionCoordinator
{
    private sealed class SameSessionSurfaceCapture : IDisposable
    {
        private readonly Action _release;
        internal SameSessionSurfaceCapture(Action release) => _release = release;
        public void Dispose() => _release();
    }

    private Func<VoyagerSameSessionSurfaceSourceFacts?>? _surfacePreviewSampleForTest;
    private bool _surfacePreviewReleaseFailsForTest;
    internal int SurfacePreviewReleasesForTest { get; private set; }
    internal void ConfigureSurfacePreviewForTest(Func<VoyagerSameSessionSurfaceSourceFacts?> sample, bool releaseFails = false)
    { _surfacePreviewSampleForTest = sample; _surfacePreviewReleaseFailsForTest = releaseFails; }
    private bool TryOpenSameSessionSurfaceCapture(string actionId, string expectedPlayerStateHash, Vector3 requestedTarget,
        out SameSessionSurfaceCapture? capture, out string rejection)
    {
        capture = new SameSessionSurfaceCapture(() =>
        {
            SurfacePreviewReleasesForTest++;
            if (_surfacePreviewReleaseFailsForTest) throw new IOException("synthetic-release-failure");
        });
        rejection = string.Empty; return true;
    }
    private bool TryCaptureSameSessionSurfaceSource(SameSessionSurfaceCapture capture,
        out VoyagerSameSessionSurfaceSourceFacts? facts, out string rejection)
    {
        facts = _surfacePreviewSampleForTest?.Invoke();
        rejection = facts is null ? "synthetic-native-observation-lost" : string.Empty;
        return facts is not null;
    }
    internal bool BeginSurfacePreviewForTest(string action, out string rejection) =>
        TryBeginSameSessionSurfacePreview(action, "synthetic-player-hash", new Vector3(200, 10, 0), out _, out rejection);
    internal VoyagerSameSessionSurfaceObservationState? SurfacePreviewStateForTest => _sameSessionSurfacePreview?.Observation.State;
    internal string? SurfacePreviewRejectionForTest => _sameSessionSurfacePreview?.Observation.Rejection;
    internal void AdvanceSurfacePreviewForTest() => UpdateSameSessionSurfacePreviewOnMainThread();
    internal bool FreshSurfacePreviewForTest(out string rejection) => _sameSessionSurfacePreview is { } preview
        ? SameSessionSurfacePreviewStillFresh(preview, out rejection) : NoPreview(out rejection);
    private static bool NoPreview(out string rejection) { rejection = "no-preview"; return false; }
    internal void StopSurfacePreviewForTest() => StopSameSessionSurfacePreviewOnMainThread();
}
