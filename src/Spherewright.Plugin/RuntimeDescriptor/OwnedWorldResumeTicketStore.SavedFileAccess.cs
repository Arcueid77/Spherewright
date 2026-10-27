using System.Diagnostics;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.RuntimeDescriptor;

internal sealed partial class OwnedWorldResumeTicketStore
{
    // This entry is called only by NEW finite remote authorization. Consumed
    // execution remains consumed. It can recover saved-file identity, never
    // the old plan, action, window or permission to dispatch Native work.
    // A later consumed file-only parent is still only identity. Every parent
    // retains its one derivation, exact unchanged boundary and all tombstones.
    internal bool TryGetFreshRemoteFileAccessProvenance(string token, out OwnedWorldResumeTicket? ticket,
        out string fingerprint, out string rejection)
    {
        if (TryGetSavedWorldOpenProvenance(token, out ticket, out fingerprint, out rejection)) return true;
        ticket = null; fingerprint = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 256) return false;
            var hash = HashToken(token); var handoff = Path.GetDirectoryName(_handoffTicketPath)!;
            var attempt = ReadExactFileAccessPair<OwnedWorldReauthorizationAttempt>(
                GetReauthorizationAttemptPath(_runtimeDirectory, hash), GetReauthorizationAttemptPath(handoff, hash));
            var consumed = ReadExactFileAccessPair<OwnedWorldResumeConsumptionTombstone>(
                GetTombstonePath(_runtimeDirectory, hash), GetTombstonePath(handoff, hash));
            if (attempt?.Ticket is not { } previous || consumed?.RecoveryAuthorization is not { } oldAuthority
                || attempt.Version != 1 || consumed.Version != 1 || consumed.ResumeTokenHash != hash
                || attempt.State != "load_may_have_started_reconciliation_required"
                || !FixedTimeEquals(previous.ResumeToken, token) || previous.Version != TicketVersion
                || previous.GameVersion != _gameVersion || consumed.GameVersion != _gameVersion
                || previous.MinimumGameTick < 0 || previous.GameplayJournalCheckpoint is null
                || !Guid.TryParse(attempt.ActionId, out _) || oldAuthority.ResumeActionId != attempt.ActionId
                || attempt.ConfirmationDigest != oldAuthority.ConfirmationDigest
                || oldAuthority.Proposal.Scope != VoyagerRecoveryWindowProposal.TaskScope
                || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(oldAuthority.EvidenceFingerprint)
                || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(attempt.ProvenanceFingerprint)
                || attempt.RecordedAtUtc == default || consumed.ConsumedAtUtc < attempt.RecordedAtUtc
                || consumed.ConsumedAtUtc > DateTimeOffset.UtcNow
                || string.IsNullOrWhiteSpace(previous.OwnedSaveName) || previous.OwnedSaveName is "." or ".."
                || Path.GetFileName(previous.OwnedSaveName) != previous.OwnedSaveName
                || previous.OwnedSaveName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || !TryValidateGameplayJournalContinuity(previous, true, out var journalHash, out rejection)) return false;
            if (previous.SourceProcessId != Process.GetCurrentProcess().Id)
            {
                try
                {
                    using var process = Process.GetProcessById(previous.SourceProcessId);
                    if (!process.HasExited && process.ProcessName.Equals("DSPGAME", StringComparison.OrdinalIgnoreCase)) return false;
                }
                catch (ArgumentException) { }
            }
            using var source = OwnedSaveRecoveryLease.Open(GameSave.SavePath(GameSave.LastExit), previous.OwnedSaveName);
            using var primary = OwnedSaveRecoveryLease.Open(GameSave.SavePath(previous.OwnedSaveName), previous.OwnedSaveName);
            if (!source.Prefix.MatchesExpectedIdentity || !primary.Prefix.MatchesExpectedIdentity
                || !source.Prefix.Peaceful || !primary.Prefix.Peaceful
                || source.Prefix.GameVersion != _gameVersion || primary.Prefix.GameVersion != _gameVersion
                || source.Prefix.GameTick != previous.MinimumGameTick || primary.Prefix.GameTick > source.Prefix.GameTick)
                return false;
            var name = "owned-world-resume-file-access-" + hash + ".json";
            var paths = new[] { Path.Combine(_runtimeDirectory, name), Path.Combine(handoff, name) };
            var derivation = ReadExactFileAccessPair<SavedFileAccessDerivation>(paths[0], paths[1]);
            if (derivation is not null)
            {
                var current = ReadExactFileAccessPair<OwnedWorldResumeTicket>(_ticketPath, _handoffTicketPath);
                if (derivation.ParentConsumedTokenHash != hash || derivation.SourceFingerprint != source.Fingerprint
                    || derivation.PrimaryFingerprint != primary.Fingerprint || derivation.JournalHash != journalHash
                    || current?.SavedWorldOpenOnly != true || current.OwnedSaveName != previous.OwnedSaveName
                    || current.MinimumGameTick != previous.MinimumGameTick || HashToken(current.ResumeToken) != derivation.FileAccessTokenHash)
                    return false;
                return TryGetSavedWorldOpenProvenance(current.ResumeToken, out ticket, out fingerprint, out rejection);
            }
            // An incomplete derivation or any newer active generation is not
            // overwritten or automatically repaired. Original records are never edited.
            if (paths.Any(File.Exists) || File.Exists(_ticketPath) || File.Exists(_handoffTicketPath)) return false;
            var now = DateTimeOffset.UtcNow;
            var fresh = PluginJson.Deserialize<OwnedWorldResumeTicket>(PluginJson.Serialize(previous))!;
            fresh.ResumeToken = CreateToken(); fresh.SavedWorldOpenOnly = true;
            fresh.ObservedProgressGameTickFloor = Math.Max(previous.MinimumGameTick, previous.ObservedProgressGameTickFloor ?? previous.MinimumGameTick);
            fresh.IssuedAtUtc = now; fresh.ExpiresAtUtc = now.AddHours(24);
            derivation = new SavedFileAccessDerivation { ParentConsumedTokenHash = hash,
                FileAccessTokenHash = HashToken(fresh.ResumeToken), SourceFingerprint = source.Fingerprint,
                PrimaryFingerprint = primary.Fingerprint, JournalHash = journalHash, RecordedAtUtc = now };
            if (!TryPersistAtPath(paths[0], _runtimeDirectory, derivation, "saved file identity derivation")
                || !TryPersistAtPath(paths[1], handoff, derivation, "handoff saved file identity derivation")) return false;
            Persist(fresh); _currentTicket = fresh; _currentTicketPath = null;
            return TryGetSavedWorldOpenProvenance(fresh.ResumeToken, out ticket, out fingerprint, out rejection);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
            || error is InvalidDataException || error is ArgumentException || error is Newtonsoft.Json.JsonException)
        { rejection = "Current complete saved-file identity could not be reconciled; no old execution authority is restored."; return false; }
    }

    private static T? ReadExactFileAccessPair<T>(string first, string second) where T : class
    {
        if (!File.Exists(first) || !File.Exists(second) || new FileInfo(first).Length > 65536 || new FileInfo(second).Length > 65536) return null;
        var a = File.ReadAllText(first); var b = File.ReadAllText(second);
        return string.Equals(a, b, StringComparison.Ordinal) ? PluginJson.Deserialize<T>(a) : null;
    }

    private sealed class SavedFileAccessDerivation
    {
        public string ParentConsumedTokenHash { get; set; } = "";
        public string FileAccessTokenHash { get; set; } = "";
        public string SourceFingerprint { get; set; } = "";
        public string PrimaryFingerprint { get; set; } = "";
        public string JournalHash { get; set; } = "";
        public DateTimeOffset RecordedAtUtc { get; set; }
    }
}
