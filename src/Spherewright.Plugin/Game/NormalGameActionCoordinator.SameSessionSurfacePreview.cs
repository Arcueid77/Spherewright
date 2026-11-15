using Spherewright.Bridge.Core.Safety;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private SameSessionSurfacePreview? _sameSessionSurfacePreview;
    private string? _sameSessionSurfacePreviewReleaseFailure;

    private sealed class SameSessionSurfacePreview : IDisposable
    {
        public SameSessionSurfacePreview(string actionId, string expectedPlayerStateHash, Vector3 requestedTarget,
            SameSessionSurfaceCapture capture, VoyagerSameSessionSurfaceObservation observation, DateTimeOffset firstCaptureUtc)
        { ActionId = actionId; ExpectedPlayerStateHash = expectedPlayerStateHash; RequestedTarget = requestedTarget; Capture = capture;
          Observation = observation; ExpiresAtUtc = firstCaptureUtc.AddSeconds(VoyagerSurfaceRecoveryPolicy.MaximumInitialBindingWallSeconds); }
        public string ActionId { get; }
        public string ExpectedPlayerStateHash { get; }
        public Vector3 RequestedTarget { get; }
        public SameSessionSurfaceCapture Capture { get; }
        public VoyagerSameSessionSurfaceObservation Observation { get; }
        public DateTimeOffset ExpiresAtUtc { get; }
        public bool IsDisposed { get; private set; }
        public bool Matches(string actionId, string expectedPlayerStateHash, Vector3 target) =>
            ActionId == actionId && ExpectedPlayerStateHash == expectedPlayerStateHash && RequestedTarget.Equals(target);
        public void Dispose() { if (IsDisposed) return; IsDisposed = true; Capture.Dispose(); }
    }

    // The future public entry is read-only: this stores a bounded two-frame
    // observation and never creates an action token or resumes simulation.
    private bool TryBeginSameSessionSurfacePreview(string actionId, string expectedPlayerStateHash, Vector3 target,
        out SameSessionSurfacePreview? preview, out string rejection)
    {
        preview = null; rejection = string.Empty;
        UpdateSameSessionSurfacePreviewOnMainThread();
        if (_sameSessionSurfacePreviewReleaseFailure is { } releaseFailure)
        { rejection = releaseFailure; return false; }
        if (_sameSessionSurfacePreview is { } existing && existing.Matches(actionId, expectedPlayerStateHash, target))
        { preview = existing; return true; }
        StopSameSessionSurfacePreviewOnMainThread();
        if (_sameSessionSurfacePreviewReleaseFailure is { } stopFailure)
        { rejection = stopFailure; return false; }
        if (!TryOpenSameSessionSurfaceCapture(actionId, expectedPlayerStateHash, target, out var capture, out rejection) || capture is null)
            return false;
        try
        {
            if (!TryCaptureSameSessionSurfaceSource(capture, out var first, out rejection) || first is null) return false;
            var observation = new VoyagerSameSessionSurfaceObservation();
            if (observation.Observe(first) != VoyagerSameSessionSurfaceObservationState.AwaitingSecondFrame)
            { rejection = observation.Rejection ?? "same_session_surface_first_frame_unproved"; return false; }
            preview = _sameSessionSurfacePreview = new SameSessionSurfacePreview(actionId, expectedPlayerStateHash, target, capture, observation, first.CapturedAtUtc);
            capture = null; // The bounded preview now owns its Native file leases.
            return true;
        }
        finally
        {
            if (capture is not null)
                try { capture.Dispose(); }
                catch (Exception ex)
                {
                    _sameSessionSurfacePreviewReleaseFailure ??= "same_session_surface_preview_release_failed:" + ex.GetType().Name;
                    rejection = _sameSessionSurfacePreviewReleaseFailure;
                }
        }
    }

    private void UpdateSameSessionSurfacePreviewOnMainThread()
    {
        var preview = _sameSessionSurfacePreview;
        if (preview is null || preview.IsDisposed) return;
        try
        {
            if (!GameMain.isRunning || !GameMain.isPaused)
                preview.Observation.Invalidate("same_session_surface_native_pause_ended");
            else if (preview.Observation.Expire(DateTimeOffset.UtcNow) != VoyagerSameSessionSurfaceObservationState.Rejected
                && preview.Observation.State == VoyagerSameSessionSurfaceObservationState.AwaitingSecondFrame)
            {
                if (!TryCaptureSameSessionSurfaceSource(preview.Capture, out var current, out var rejection) || current is null)
                    preview.Observation.Invalidate(rejection);
                else preview.Observation.Observe(current);
            }
            if (preview.Observation.State == VoyagerSameSessionSurfaceObservationState.Rejected) ReleaseSameSessionSurfacePreview(preview);
        }
        catch (Exception ex)
        {
            preview.Observation.Invalidate("same_session_surface_preview_failed:" + ex.GetType().Name);
            ReleaseSameSessionSurfacePreview(preview);
        }
    }

    private bool SameSessionSurfacePreviewStillFresh(SameSessionSurfacePreview preview, out string rejection)
    {
        rejection = "same_session_surface_preview_not_bound";
        if (preview.IsDisposed || preview.Observation.State != VoyagerSameSessionSurfaceObservationState.Bound
            || preview.Observation.Binding is not { } binding) return false;
        if (!TryCaptureSameSessionSurfaceSource(preview.Capture, out var current, out rejection) || current is null
            || !binding.MatchesFresh(current, out rejection))
        { preview.Observation.Invalidate(rejection); ReleaseSameSessionSurfacePreview(preview); return false; }
        return true;
    }

    private void StopSameSessionSurfacePreviewOnMainThread()
    {
        var prior = _sameSessionSurfacePreview; _sameSessionSurfacePreview = null;
        if (prior is not null) ReleaseSameSessionSurfacePreview(prior);
    }

    private void ReleaseSameSessionSurfacePreview(SameSessionSurfacePreview preview)
    {
        try { preview.Dispose(); }
        catch (Exception ex) { _sameSessionSurfacePreviewReleaseFailure ??= "same_session_surface_preview_release_failed:" + ex.GetType().Name; }
    }

}
