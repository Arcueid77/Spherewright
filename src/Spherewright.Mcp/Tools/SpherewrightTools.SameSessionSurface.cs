using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Mcp.BridgeClient;

namespace Spherewright.Mcp.Tools;

public static partial class SpherewrightTools
{
    [McpServerTool(Name = "spherewright_preview_same_session_surface_recovery", Title = "Observe a paused same-session shore recovery source",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("READ-ONLY two-frame preview for one retained outcome-unknown Voyager Move in the same paused owned session. Binds fresh player/action/session, original journey and Journal evidence, and a finite target within16m. An optional proposed UTC deadline is a hypothetical finite wall window only; it is never applied or authorized and adds no game ticks. awaiting_second_frame is not a completed observation; source_bound is not an action, route-clearance or landing proof. Returns no token, never resumes, clears quarantine, changes the old outcome or saves. A later executable recovery requires its own fresh implementation and admission.")]
    public static async Task<CallToolResult> PreviewSameSessionSurfaceRecoveryAsync(IBridgeClient bridgeClient,
        string sessionId, int planetId, string originalActionId, long expectedRevision, string expectedPlayerStateHash,
        float targetX, float targetY, float targetZ, int stateHashVersion = 1, DateTimeOffset? proposedRecoveryDeadlineUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || planetId <= 0 || expectedRevision <= 0 || stateHashVersion != CanonicalStateHash.Version
            || !Guid.TryParse(originalActionId, out _) || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(expectedPlayerStateHash)
            || !Known(targetX) || !Known(targetY) || !Known(targetZ)
            || (double)targetX * targetX + (double)targetY * targetY + (double)targetZ * targetZ < 1d
            || proposedRecoveryDeadlineUtc is { } proposed && (proposed.Offset != TimeSpan.Zero || proposed <= DateTimeOffset.UtcNow
                || proposed - DateTimeOffset.UtcNow > VoyagerRecoveryWindowPolicy.MaximumConfirmedWindow))
            return ToToolResult(BridgeCallResult<SameSessionSurfacePreviewSnapshot>.Failed(BridgeError.Create(
                BridgeErrorCodes.InvalidRequest, "The exact original action, player hash and finite surface target are required.", false,
                "Inspect the owned session and choose one explicit finite target; no recovery action was accepted.")), string.Empty);
        var request = new SameSessionSurfacePreviewRequest
        {
            PlanetId = planetId, OriginalActionId = originalActionId, ExpectedRevision = expectedRevision,
            ExpectedPlayerStateHash = expectedPlayerStateHash, Target = new Vector3Snapshot { X = targetX, Y = targetY, Z = targetZ },
            StateHashVersion = stateHashVersion, ProposedRecoveryDeadlineUtc = proposedRecoveryDeadlineUtc,
        };
        var result = await bridgeClient.PreviewSameSessionSurfaceRecoveryAsync(sessionId, request, cancellationToken).ConfigureAwait(false);
        if (result.Success && !ValidSameSessionSurfacePreview(result.Value, sessionId, request))
            return ToToolResult(BridgeCallResult<SameSessionSurfacePreviewSnapshot>.Failed(BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady, "The Plugin did not echo a complete tokenless same-session surface observation.", false,
                "Use matching Plugin/MCP bytes; do not infer movement or resume authority from an older or inconsistent response.")), string.Empty);
        return ToToolResult(result, "Read-only same-session surface observation captured; the original quarantine remains active.");
    }

    private static bool ValidSameSessionSurfacePreview(SameSessionSurfacePreviewSnapshot? value, string sessionId, SameSessionSurfacePreviewRequest request)
    {
        if (value is null || value.EvidenceVersion != 2 || value.ReadOnly != true || value.Executable != false || value.CommitAllowedNow != false
            || value.NativeResumePerformed != false || value.OriginalOutcomeChanged != false || value.OriginalActionId != request.OriginalActionId
            || value.ProposedWindowApplied != false || value.AdditionalVoyagerGameTicks != 0
            || value.OriginalJourneyIssuedAtUtc is not { } issued || issued == default || issued.Offset != TimeSpan.Zero
            || value.OriginalJourneyDeadlineUtc is not { } original || original.Offset != TimeSpan.Zero || original <= issued
            || value.CurrentJourneyDeadlineUtc is not { } current || current == default || current.Offset != TimeSpan.Zero
            || value.ProposedRecoveryDeadlineUtc != request.ProposedRecoveryDeadlineUtc
            || value.ProposedRecoveryDeadlineUtc is { } proposed && (proposed.Offset != TimeSpan.Zero || proposed <= original || proposed <= current)
            || value.SessionId != sessionId || value.PlanetId != request.PlanetId || value.Revision != request.ExpectedRevision
            || value.PlayerStateHash != request.ExpectedPlayerStateHash
            || value.SourceGameTick < 0 || value.ExpiresAtUtc == default || value.MaximumWorkGameTicks != VoyagerSurfaceRecoveryPolicy.MaximumWorkGameTicks
            || value.MaximumWallSeconds != VoyagerSurfaceRecoveryPolicy.MaximumWallSeconds || value.Target is not { } target
            || request.Target is not { } requested || !Known(target.X) || !Known(target.Y) || !Known(target.Z)) return false;
        if (value.State == "awaiting_second_frame") { if (value.SourceFingerprint is not null) return false; }
        else if (value.State == "source_bound") { if (!OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(value.SourceFingerprint)) return false; }
        else return false;
        var a = Math.Sqrt((double)target.X * target.X + (double)target.Y * target.Y + (double)target.Z * target.Z);
        var b = Math.Sqrt((double)requested.X * requested.X + (double)requested.Y * requested.Y + (double)requested.Z * requested.Z);
        if (a < 1 || b < 1 || Math.Abs(a - b) > 4.01d) return false;
        var dot = (double)target.X * requested.X + (double)target.Y * requested.Y + (double)target.Z * requested.Z;
        return dot / (a * b) >= .999999d;
    }

    private static bool Known(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
