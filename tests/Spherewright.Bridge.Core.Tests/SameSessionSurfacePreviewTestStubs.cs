using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;
using UnityEngine;

namespace Spherewright.Plugin.Game;

// Test-only Native substitutes. The production preview lifecycle is linked;
// no Unity physics, real file lease, game Resume or Move is executed here.
internal sealed partial class NormalGameActionCoordinator
{
    private sealed class SameSessionSurfaceCapture : IDisposable
    {
        private readonly Action _release;
        internal SameSessionSurfaceCapture(Action release, Vector3 target) { _release = release; Target = target; }
        public Vector3 Target { get; }
        public TestSurfaceRoute Route { get; } = new();
        public void Dispose() => _release();
    }
    private sealed class TestSurfaceRoute { public long SourceGameTick => 200; }

    private Func<VoyagerSameSessionSurfaceSourceFacts?>? _surfacePreviewSampleForTest;
    private bool _surfacePreviewReleaseFailsForTest;
    internal int SurfacePreviewOpensForTest { get; private set; }
    internal int SurfacePreviewReleasesForTest { get; private set; }
    internal void ConfigureSurfacePreviewForTest(Func<VoyagerSameSessionSurfaceSourceFacts?> sample, bool releaseFails = false)
    { _surfacePreviewSampleForTest = sample; _surfacePreviewReleaseFailsForTest = releaseFails; }
    private bool TryOpenSameSessionSurfaceCapture(string actionId, string expectedPlayerStateHash, Vector3 requestedTarget,
        out SameSessionSurfaceCapture? capture, out string rejection)
    {
        SurfacePreviewOpensForTest++;
        capture = new SameSessionSurfaceCapture(() =>
        {
            SurfacePreviewReleasesForTest++;
            if (_surfacePreviewReleaseFailsForTest) throw new IOException("synthetic-release-failure");
        }, requestedTarget);
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

    private sealed class CommonPrepareResult
    {
        public SessionState? Session { get; init; }
        public BridgeError? Error { get; init; }
    }
    private CommonPrepareResult ValidatePrepareCommon(string? sessionId, int planetId, int stateHashVersion)
    {
        var session = _surfacePreviewSampleForTest?.Invoke()?.Session;
        return session is not null && session.SessionId == sessionId && session.LocalPlanetId == planetId && stateHashVersion == 1
            ? new CommonPrepareResult { Session = session }
            : new CommonPrepareResult { Error = BridgeError.Create(BridgeErrorCodes.StaleState, "synthetic-context-refused", false, "inspect") };
    }
    private static Vector3Snapshot Snapshot(Vector3 value) => new() { X = value.x, Y = value.y, Z = value.z };
}
