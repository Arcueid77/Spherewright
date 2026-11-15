using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Journals;
using Spherewright.Plugin.Game;

namespace Spherewright.Plugin.RuntimeDescriptor;

internal sealed partial class OwnedWorldResumeTicketStore
{
    // Captures the live journal itself, not a fabricated resume ticket. The
    // checkpoint must be copied by a trusted native caller from its current,
    // quiescent manager; it grants neither a Load nor a simulation Resume.
    internal SameSessionSurfaceJournalEvidence OpenLiveSurfaceJournalEvidence(string ownedSaveName,
        string gameVersion, OwnedWorldGameplayJournalCheckpoint checkpoint)
    {
        if (string.IsNullOrWhiteSpace(ownedSaveName) || gameVersion != _gameVersion || checkpoint is null)
            throw new InvalidDataException("same_session_surface_live_journal_identity_unproved");
        var identity = GameplayJournalIdentity.HashOwnedSaveIdentity(ownedSaveName);
        var path = Path.Combine(_runtimeDirectory, "journals", $"gameplay-{identity}.json");
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("same_session_surface_live_journal_reparse_refused");
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (stream.Length < 1 || stream.Length > 16L * 1024 * 1024)
                throw new InvalidDataException("same_session_surface_live_journal_size_unproved");
            using (var input = new StreamReader(stream, new UTF8Encoding(false, true), true, 4096, leaveOpen: true))
            using (var reader = new JsonTextReader(input) { MaxDepth = 64, DateParseHandling = DateParseHandling.None })
            {
                JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new InvalidDataException("same_session_surface_live_journal_trailing_content");
            }
            if (!TryValidateGameplayJournalContinuity(ownedSaveName, gameVersion, checkpoint, true, out var logicalHash, out _))
                throw new InvalidDataException("same_session_surface_live_journal_continuity_unproved");
            var rawHash = FlightCheckpointStore.HashResumeRecord(stream);
            var fingerprint = CanonicalStateHash.Combine("same-session-surface-journal-v1", identity, gameVersion,
                checkpoint.Version, checkpoint.JournalId, checkpoint.TrackingMode, checkpoint.HistoricalCoverageComplete,
                checkpoint.TrackingStartedAtGameTick, checkpoint.MinimumDurableThroughSequence, logicalHash, rawHash);
            return new SameSessionSurfaceJournalEvidence(stream, fingerprint);
        }
        catch { stream.Dispose(); throw; }
    }
}

internal sealed class SameSessionSurfaceJournalEvidence : IDisposable
{
    private readonly FileStream _lease;
    internal SameSessionSurfaceJournalEvidence(FileStream lease, string fingerprint)
    { _lease = lease; Fingerprint = fingerprint; }
    internal string Fingerprint { get; }
    internal bool IsDisposed { get; private set; }
    public void Dispose()
    { if (IsDisposed) return; IsDisposed = true; _lease.Dispose(); }
}
