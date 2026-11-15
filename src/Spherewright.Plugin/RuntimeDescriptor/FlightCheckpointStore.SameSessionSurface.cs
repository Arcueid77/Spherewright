using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Transport;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Spherewright.Plugin.RuntimeDescriptor;

internal sealed partial class FlightCheckpointStore
{
    // Read-only source evidence for a future same-session surface task. It is
    // never a checkpoint reload, new journey, save, resume, or action grant.
    internal bool TryOpenSameSessionSurfaceEvidence(string sessionId, string ownedSaveName,
        int remotePlanetId, string journeyId, long currentGameTick, int currentProcessId,
        out SameSessionSurfaceJourneyEvidence? evidence, out string rejection)
    {
        evidence = null; rejection = "same_session_surface_journey_unproved";
        FileStream? ticketLease = null, journeyLease = null;
        OwnedSaveRecoveryLease? checkpointLease = null;
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(ownedSaveName)
                || string.IsNullOrWhiteSpace(journeyId) || remotePlanetId <= 0 || currentGameTick < 0 || currentProcessId <= 0
                || (File.GetAttributes(_ticketPath) & FileAttributes.ReparsePoint) != 0
                || (File.GetAttributes(VoyagerJourneyPath) & FileAttributes.ReparsePoint) != 0)
                return false;
            ticketLease = new FileStream(_ticketPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            journeyLease = new FileStream(VoyagerJourneyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            RequireStrictRecord(ticketLease); RequireStrictRecord(journeyLease);
            var checkpoint = ReadBoundedResumeRecord<FlightCheckpointTicket>(ticketLease);
            var journey = ReadBoundedResumeRecord<VoyagerJourneyRecord>(journeyLease);
            if (checkpoint is null || journey is null || !IsStructurallyValid(checkpoint) || !ValidVoyagerJourney(journey)
                || PluginJson.Serialize(checkpoint) != PluginJson.Serialize(_currentTicket)
                || PluginJson.Serialize(journey) != PluginJson.Serialize(_voyagerJourney))
                { rejection = "same_session_surface_records_unproved"; return false; }
            if (checkpoint.LifecycleState != FlightCheckpointLifecycleStates.FlightSucceeded || !checkpoint.NativeLaunchArmed
                || checkpoint.ReloadPending || !string.IsNullOrEmpty(checkpoint.ReloadActionId)
                || checkpoint.RecoveryRequiredAtGameTick.HasValue || checkpoint.RetiredAtGameTick.HasValue || checkpoint.RetiredAtUtc.HasValue)
                { rejection = "same_session_surface_successful_outbound_unproved"; return false; }
            if (checkpoint.SourceProcessId != currentProcessId || checkpoint.SourceBridgeInstanceId != _bridgeInstanceId
                || checkpoint.SourceSessionId != sessionId || checkpoint.OwnedSaveName != ownedSaveName
                || checkpoint.GameVersion != _gameVersion || checkpoint.VoyagerJourneyId != journeyId
                || journey.JourneyId != journeyId || journey.OwnedSaveName != ownedSaveName || journey.GameVersion != _gameVersion)
                { rejection = "same_session_surface_identity_unproved"; return false; }
            if (journey.Phase != "remote_landed" || journey.ReturnLeg || journey.ReturnOnlyScopeActive || journey.SpentGameTicksIsConservative
                || journey.RemotePlanetId != remotePlanetId || checkpoint.DestinationPlanetId != remotePlanetId)
                { rejection = "same_session_surface_remote_phase_unproved"; return false; }
            if (checkpoint.OriginPlanetId != journey.HomePlanetId || checkpoint.OriginStarId != journey.HomeStarId
                || checkpoint.DestinationStarId != journey.DestinationStarId || journey.ActiveCheckpointId != checkpoint.CheckpointId
                || journey.ActiveActionId != checkpoint.LastAttemptActionId || journey.LastAccountedFlightActionId != checkpoint.LastAttemptActionId)
                { rejection = "same_session_surface_original_flight_unproved"; return false; }
            if (!journey.LastLandedGameTick.HasValue || journey.LastLandedGameTick != checkpoint.SuccessfulFlightAtGameTick
                || journey.LastAccountedGameTick != journey.LastLandedGameTick || journey.LastLandedGameTick > currentGameTick
                || checkpoint.SavedGameTick > journey.StartedAtGameTick || journey.StartedAtGameTick > journey.LastLandedGameTick)
                { rejection = "same_session_surface_original_timeline_unproved"; return false; }
            // A successful outbound is intentionally not reload-eligible.
            // Verify its file as immutable evidence without reviving that
            // checkpoint's separate, already-retired LOAD capability.
            var checkpointPath = GameSave.SavePath(checkpoint.CheckpointSaveName);
            if ((File.GetAttributes(checkpointPath) & FileAttributes.ReparsePoint) != 0) return false;
            checkpointLease = OwnedSaveRecoveryLease.Open(checkpointPath, ownedSaveName);
            if (!checkpointLease.Prefix.MatchesExpectedIdentity || !checkpointLease.Prefix.Peaceful || checkpointLease.Prefix.Sandbox
                || checkpointLease.Prefix.GameVersion != _gameVersion || checkpointLease.Prefix.GameTick != checkpoint.SavedGameTick
                || checkpointLease.Fingerprint != checkpoint.CheckpointFileFingerprint)
                { rejection = "same_session_surface_checkpoint_file_unproved"; return false; }
            var journeyJson = PluginJson.Serialize(journey); var checkpointJson = PluginJson.Serialize(checkpoint);
            var journeyBytesHash = HashResumeRecord(journeyLease); var checkpointBytesHash = HashResumeRecord(ticketLease);
            evidence = new SameSessionSurfaceJourneyEvidence(this, journeyJson, checkpointJson, checkpointLease.Fingerprint,
                journeyBytesHash, checkpointBytesHash, journeyId, currentGameTick, ticketLease, journeyLease, checkpointLease);
            ticketLease = null; journeyLease = null; checkpointLease = null;
            rejection = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException
            || ex is InvalidDataException || ex is NotSupportedException || ex is System.Security.SecurityException
            || ex is CryptographicException || ex is Newtonsoft.Json.JsonException)
        { rejection = "same_session_surface_journey_capture_failed:" + ex.GetType().Name; return false; }
        finally { checkpointLease?.Dispose(); journeyLease?.Dispose(); ticketLease?.Dispose(); }
    }

    internal bool SameSessionSurfaceEvidenceStillMatches(SameSessionSurfaceJourneyEvidence evidence) =>
        !evidence.IsDisposed && ReferenceEquals(evidence.Store, this)
        && PluginJson.Serialize(_voyagerJourney) == evidence.JourneyJson
        && PluginJson.Serialize(_currentTicket) == evidence.CheckpointJson;

    private static void RequireStrictRecord(FileStream stream)
    {
        if (stream.Length < 1 || stream.Length > 65536) throw new InvalidDataException("Protected record exceeds its finite bound.");
        var position = stream.Position;
        try
        {
            stream.Position = 0;
            using var input = new StreamReader(stream, new UTF8Encoding(false, true), true, 1024, leaveOpen: true);
            using var reader = new JsonTextReader(input) { MaxDepth = 32, DateParseHandling = DateParseHandling.None };
            JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new InvalidDataException("Trailing protected-record content.");
        }
        finally { stream.Position = position; }
    }
}

// FileShare.Read leases keep the exact original files stable until the caller
// explicitly releases them. The object grants no Native execution by itself.
internal sealed class SameSessionSurfaceJourneyEvidence : IDisposable
{
    private readonly IDisposable[] _leases;
    internal SameSessionSurfaceJourneyEvidence(FlightCheckpointStore store, string journeyJson, string checkpointJson,
        string checkpointFingerprint, string journeyBytesHash, string checkpointBytesHash,
        string journeyId, long sourceGameTick, params IDisposable[] leases)
    {
        Store = store; JourneyJson = journeyJson; CheckpointJson = checkpointJson; JourneyId = journeyId;
        SourceGameTick = sourceGameTick; _leases = leases;
        JourneySnapshotFingerprint = CanonicalStateHash.Combine("same-session-surface-journey-v1", journeyJson);
        RouteFingerprint = CanonicalStateHash.Combine("same-session-surface-route-v1", journeyJson, checkpointJson,
            journeyBytesHash, checkpointBytesHash, checkpointFingerprint);
    }
    internal FlightCheckpointStore Store { get; }
    internal string JourneyJson { get; }
    internal string CheckpointJson { get; }
    internal string JourneyId { get; }
    internal long SourceGameTick { get; }
    internal string JourneySnapshotFingerprint { get; }
    internal string RouteFingerprint { get; }
    internal bool IsDisposed { get; private set; }
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        Exception? failure = null;
        for (var i = _leases.Length - 1; i >= 0; i--)
            try { _leases[i].Dispose(); } catch (Exception ex) { failure ??= ex; }
        if (failure is not null) throw new IOException("same_session_surface_evidence_release_failed", failure);
    }
}
