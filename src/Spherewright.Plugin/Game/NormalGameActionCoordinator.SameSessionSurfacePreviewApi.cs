using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    public GameCallResult<SameSessionSurfacePreviewSnapshot> PreviewSameSessionSurfaceRecoveryOnMainThread(
        string? requestedSessionId, SameSessionSurfacePreviewRequest request)
    {
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null) return GameCallResult<SameSessionSurfacePreviewSnapshot>.Failed(common.Error);
        var session = common.Session!;
        if (session.Revision != request.ExpectedRevision || session.WriteQuarantineActionId != request.OriginalActionId
            || !Guid.TryParse(request.OriginalActionId, out _) || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(request.ExpectedPlayerStateHash)
            || request.Target is null)
            return GameCallResult<SameSessionSurfacePreviewSnapshot>.Failed(BridgeError.Create(BridgeErrorCodes.StaleState,
                "The current revision, exact quarantine, player hash or target is missing or changed.", true,
                "Inspect the same owned session and original unknown action before a fresh read-only preview."));
        var target = ToVector(request.Target);
        if (!TryBeginSameSessionSurfacePreview(request.OriginalActionId, request.ExpectedPlayerStateHash, target,
            out var preview, out var rejection) || preview is null)
            return GameCallResult<SameSessionSurfacePreviewSnapshot>.Failed(BridgeError.Create(BridgeErrorCodes.ActionRejected,
                rejection, true, "Keep the original pause and unknown action; do not submit a Move from an unproved preview."));
        if (preview.Observation.State == VoyagerSameSessionSurfaceObservationState.Rejected)
        {
            rejection = preview.Observation.Rejection ?? "same_session_surface_preview_unproved";
            StopSameSessionSurfacePreviewOnMainThread();
            return GameCallResult<SameSessionSurfacePreviewSnapshot>.Failed(BridgeError.Create(BridgeErrorCodes.ActionRejected,
                rejection, true, "Keep the original pause and unknown action; inspect the failed Native evidence."));
        }
        if (preview.Observation.State == VoyagerSameSessionSurfaceObservationState.Bound
            && !SameSessionSurfacePreviewStillFresh(preview, out rejection))
        {
            StopSameSessionSurfacePreviewOnMainThread();
            return GameCallResult<SameSessionSurfacePreviewSnapshot>.Failed(BridgeError.Create(BridgeErrorCodes.StaleState,
                rejection, true, "Keep the original pause and unknown action; a changed source needs a fresh preview."));
        }
        var binding = preview.Observation.Binding;
        return GameCallResult<SameSessionSurfacePreviewSnapshot>.Succeeded(new SameSessionSurfacePreviewSnapshot
        {
            EvidenceVersion = 1, ReadOnly = true, Executable = false, CommitAllowedNow = false,
            NativeResumePerformed = false, OriginalOutcomeChanged = false,
            State = binding is null ? "awaiting_second_frame" : "source_bound",
            SessionId = session.SessionId, PlanetId = request.PlanetId, Revision = session.Revision, PlayerStateHash = preview.ExpectedPlayerStateHash,
            OriginalActionId = preview.ActionId, SourceGameTick = preview.Capture.Route.SourceGameTick,
            ExpiresAtUtc = preview.ExpiresAtUtc, Target = Snapshot(preview.Capture.Target),
            SourceFingerprint = binding?.Fingerprint,
            MaximumWorkGameTicks = VoyagerSurfaceRecoveryPolicy.MaximumWorkGameTicks,
            MaximumWallSeconds = VoyagerSurfaceRecoveryPolicy.MaximumWallSeconds,
        });
    }
}
