using System.Security.Cryptography;
using System.Text;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Bridge.Core.Safety;

// The legacy action kind is an explicit caller claim, never authenticated by a hash.
public sealed class LegacyLocalMoveUnknownCapture
{
    public string SourceProductSha { get; set; } = string.Empty;
    public bool CaptureAttested { get; set; }
    public ActionResultSnapshot? Action { get; set; }
    public SessionState? Session { get; set; }
    public string TicketSha256 { get; set; } = string.Empty;
    public string JourneySha256 { get; set; } = string.Empty;
    public string CheckpointSha256 { get; set; } = string.Empty;
    public string JournalSha256 { get; set; } = string.Empty;
}

public sealed class LegacyLocalMoveUnknownBinding
{
    public string OwnedSaveName { get; set; } = string.Empty;
    public string SourceSessionId { get; set; } = string.Empty;
    public string NativeVersion { get; set; } = string.Empty;
    public string QuarantineActionId { get; set; } = string.Empty;
    public string SuccessfulFlightActionId { get; set; } = string.Empty;
    public int? RemotePlanetId { get; set; }
    public string CredentialRawFingerprint { get; set; } = string.Empty;
    public string JourneySha256 { get; set; } = string.Empty;
    public string CheckpointSha256 { get; set; } = string.Empty;
    public string JournalSha256 { get; set; } = string.Empty;
}

public static class VoyagerLegacyLocalMoveUnknownPolicy
{
    public const int MaximumUtf8Bytes = 65536;
    public const string LegacySourceSha = "12e6dfcaaae923c844cc3ae0c2b66c185537b93a";
    public const string Disclosure = "Legacy action kind, failure and source-product revision are caller-attested from protected original public captures; Native cannot authenticate those old raw responses or prove the original outcome. The original Move remains outcome_unknown, never known failed or completed. This hash binds bytes, not their truth. Current Native ticket, LastExit, primary, successful checkpoint, Journal and remaining journey are independently checked; fresh first-tick ground/scene/energy admission remains mandatory.";

    public static bool HasEvidence(PrepareOwnedWorldResumeRequest r) =>
        !string.IsNullOrEmpty(r.LegacyLocalMoveUnknownEvidenceJson)
        || !string.IsNullOrEmpty(r.ExpectedLegacyLocalMoveUnknownEvidenceHash);

    public static bool IsHash(string? value) => value is { Length: 64 }
        && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');

    public static string? HashEvidence(string? json)
    {
        if (json is null || json.Length == 0 || json.Length > MaximumUtf8Bytes) return null;
        try
        {
            var bytes = new UTF8Encoding(false, true).GetBytes(json);
            if (bytes.Length > MaximumUtf8Bytes) return null;
            using var algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        catch (EncoderFallbackException) { return null; }
    }

    public static string? ValidateRequest(PrepareOwnedWorldResumeRequest r)
    {
        if (!HasEvidence(r)) return null;
        if (r.RecoveryMode != OwnedWorldResumeModes.Default || OwnedWorldRecoveryPolicy.HasPriorFailedResumeWitness(r)
            || !string.IsNullOrEmpty(r.ExpectedRemoteRecoveryContextFingerprint))
            return "Legacy local-Move-unknown evidence is default LastExit only and cannot borrow prior failed-resume witnesses or another recovery mode.";
        if (!IsHash(r.ExpectedLegacyLocalMoveUnknownEvidenceHash)
            || HashEvidence(r.LegacyLocalMoveUnknownEvidenceJson) != r.ExpectedLegacyLocalMoveUnknownEvidenceHash)
            return "Both legacy evidence inputs must bind the same immutable UTF-8 JSON of at most64KiB by its lowercase SHA256; paths are not accepted.";
        return null;
    }

    public static string? Blocker(LegacyLocalMoveUnknownCapture c, LegacyLocalMoveUnknownBinding b,
        VoyagerRemoteResumeFacts f)
    {
        if (!f.DefaultLastExit || !f.ExactOwnedIdentityAndVersion || !f.Peaceful || !f.FullSourceAndPrimaryEvidence
            || !f.ExactDurableJourneyAndCheckpoint || !f.OriginalJournalCheckpointPresent
            || b.RemotePlanetId is null || b.RemotePlanetId <= 0)
            return "legacy_local_move_native_provenance_unproved";
        var a = c.Action; var s = c.Session;
        if (c.SourceProductSha != LegacySourceSha || !c.CaptureAttested || a is null || s is null
            || a.ActionKind != "move" || a.State != "outcome_unknown" || !a.Terminal || a.Succeeded
            || a.FailureKind != "voyager_local_native_tail_unproved" || a.ReconciledFromOutcomeUnknown
            || !Guid.TryParse(a.ActionId, out _) || !Guid.TryParse(a.IdempotencyKey, out _)
            || string.IsNullOrEmpty(a.BeforeStateHash) || a.ActionId != b.QuarantineActionId
            || a.ActionId == b.SuccessfulFlightActionId || a.SessionId != b.SourceSessionId
            || a.PlanetId != b.RemotePlanetId || !a.StartedAtGameTick.HasValue || !a.CompletedAtGameTick.HasValue
            || a.StartedAtGameTick < f.LastLandedGameTick || a.CompletedAtGameTick < a.StartedAtGameTick)
            return "legacy_local_move_caller_capture_unproved";
        if (!s.GameLoaded || !s.OwnedBySpherewright || s.WritesAllowed || s.WriteHealth != WriteHealthStates.Quarantined
            || s.CurrentSessionLoadedFromFlightCheckpoint || s.WriteQuarantineActionId != a.ActionId
            || s.SessionId != b.SourceSessionId || s.SaveName != b.OwnedSaveName || s.GameVersion != b.NativeVersion
            || s.LocalPlanetId != b.RemotePlanetId || !s.GameTick.HasValue || s.GameTick < a.CompletedAtGameTick
            || s.GameTick < f.TicketMinimumGameTick || f.SourceGameTick < s.GameTick
            || s.LastOwnedSaveGameTick != f.PrimaryGameTick)
            return "legacy_local_move_source_snapshot_mismatch";
        if (!IsHash(c.TicketSha256) || !IsHash(c.JourneySha256) || !IsHash(c.CheckpointSha256) || !IsHash(c.JournalSha256)
            || b.CredentialRawFingerprint != CanonicalStateHash.Combine("remote-active-credential-raw-v1", c.TicketSha256, c.TicketSha256)
            || c.JourneySha256 != b.JourneySha256 || c.CheckpointSha256 != b.CheckpointSha256 || c.JournalSha256 != b.JournalSha256)
            return "legacy_local_move_protected_snapshot_changed";
        return VoyagerRemoteResumePolicy.RemainingJourneyBlocker(f);
    }

    public static bool HasMatchingEcho(PrepareOwnedWorldResumeRequest r, PreparedOwnedWorldResumePlan p) =>
        HasEvidence(r) && ValidateRequest(r) is null && p.Prepared && p.RecoveryMode == OwnedWorldResumeModes.Default
        && p.LegacyLocalMoveUnknownProvenanceUsed && p.LegacyLocalMoveUnknownKindCallerAttested
        && !p.NativeOriginalOutcomeProved && p.LegacyLocalMoveUnknownEvidenceHash == r.ExpectedLegacyLocalMoveUnknownEvidenceHash
        && !p.PriorFailedResumeProvenanceUsed && !p.PriorFailedResumeV2ProvenanceUsed && !p.PriorFailedResumeV3ProvenanceUsed
        && !p.PriorActionTerminalFailureAttested && !p.FailedRemoteLastExitReauthorizationUsed
        && p.ExactEmbeddedIdentityVerified && p.UserConfirmationRequired && !p.CommitAllowedNow
        && p.RecoveryEvidenceVersion >= 1 && p.OriginalJourneyDeadlineUtc.HasValue && !string.IsNullOrEmpty(p.PlanToken)
        && p.CandidateGameTick.HasValue && p.CandidateGameTick == p.MinimumGameTick
        && OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(p.ConfirmationDigest)
        && p.ConfirmationPrompt?.Contains(Disclosure) == true;
}
