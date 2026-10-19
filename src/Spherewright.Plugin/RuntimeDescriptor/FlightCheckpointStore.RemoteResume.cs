using System.Text;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.RuntimeDescriptor;

internal sealed partial class FlightCheckpointStore
{
    public bool TryOpenRemoteResumeEvidence(OwnedWorldResumeTicket ticket, OwnedSaveRecoveryLease source,
        OwnedSaveRecoveryLease primary, out VoyagerRemoteResumeEvidence? evidence, out string rejection,
        VoyagerRecoveryWindowProposal? recoveryProposal = null)
    {
        evidence = null;
        rejection = string.Empty;
        FileStream? ticketLease = null; FileStream? journeyLease = null; OwnedSaveRecoveryLease? checkpointLease = null;
        try
        {
            ticketLease = new FileStream(_ticketPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            journeyLease = new FileStream(VoyagerJourneyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var checkpoint = ReadBoundedResumeRecord<FlightCheckpointTicket>(ticketLease);
            var journey = ReadBoundedResumeRecord<VoyagerJourneyRecord>(journeyLease);
            if (checkpoint is null || journey is null || !IsStructurallyValid(checkpoint) || !ValidVoyagerJourney(journey)
                || PluginJson.Serialize(checkpoint) != PluginJson.Serialize(_currentTicket)
                || PluginJson.Serialize(journey) != PluginJson.Serialize(_voyagerJourney))
                throw new InvalidDataException("The original protected journey/checkpoint changed.");
            checkpointLease = OwnedSaveRecoveryLease.Open(GameSave.SavePath(checkpoint.CheckpointSaveName), ticket.OwnedSaveName);
            var facts = new VoyagerRemoteResumeFacts
            {
                DefaultLastExit = true,
                ExactOwnedIdentityAndVersion = source.Prefix.MatchesExpectedIdentity && primary.Prefix.MatchesExpectedIdentity
                    && checkpoint.OwnedSaveName == ticket.OwnedSaveName && journey.OwnedSaveName == ticket.OwnedSaveName
                    && source.Prefix.GameVersion == ticket.GameVersion && primary.Prefix.GameVersion == ticket.GameVersion
                    && checkpoint.GameVersion == ticket.GameVersion && journey.GameVersion == ticket.GameVersion && ticket.GameVersion == _gameVersion,
                Peaceful = source.Prefix.Peaceful && primary.Prefix.Peaceful,
                FullSourceAndPrimaryEvidence = !string.IsNullOrEmpty(source.Fingerprint) && !string.IsNullOrEmpty(primary.Fingerprint),
                ExactDurableJourneyAndCheckpoint = checkpoint.CheckpointId == journey.ActiveCheckpointId
                    && checkpoint.VoyagerJourneyId == journey.JourneyId && checkpoint.LastAttemptActionId == journey.ActiveActionId
                    && journey.LastAccountedFlightActionId == journey.ActiveActionId
                    && journey.LastLandedGameTick >= checkpoint.SuccessfulFlightAtGameTick
                    && checkpoint.OriginPlanetId == journey.HomePlanetId && checkpoint.OriginStarId == journey.HomeStarId
                    && checkpoint.DestinationStarId == journey.DestinationStarId && journey.RemotePlanetId == ticket.ExpectedPlanetId
                    && checkpointLease.Fingerprint == checkpoint.CheckpointFileFingerprint
                    && checkpointLease.Prefix.GameTick == checkpoint.SavedGameTick,
                QuarantineMatchesOriginalAction = !string.IsNullOrEmpty(ticket.QuarantineActionId)
                    && ticket.QuarantineActionId == journey.ActiveActionId,
                OriginalJournalCheckpointPresent = ticket.GameplayJournalCheckpoint is { Version: OwnedWorldGameplayJournalCheckpoint.CurrentVersion } journal
                    && !string.IsNullOrEmpty(journal.JournalId) && journal.MinimumDurableThroughSequence >= 0,
                RemoteLandedOutbound = journey.Phase == "remote_landed" && !journey.ReturnLeg,
                CheckpointFlightSucceededWithoutReload = checkpoint.LifecycleState == FlightCheckpointLifecycleStates.FlightSucceeded
                    && checkpoint.SuccessfulFlightAtGameTick.HasValue && !checkpoint.ReloadPending
                    && string.IsNullOrEmpty(checkpoint.ReloadActionId) && !checkpoint.RetiredAtGameTick.HasValue,
                SourceGameTick = source.Prefix.GameTick, PrimaryGameTick = primary.Prefix.GameTick,
                TicketMinimumGameTick = ticket.MinimumGameTick, LastAccountedGameTick = journey.LastAccountedGameTick,
                LastLandedGameTick = journey.LastLandedGameTick, SpentGameTicks = journey.SpentGameTicks,
                MaximumGameTicks = journey.MaximumGameTicks, ReturnReserveGameTicks = journey.ReturnReserveGameTicks,
                NowUtc = DateTimeOffset.UtcNow, OriginalDeadlineUtc = journey.DeadlineUtc,
                OriginalIssuedAtUtc = journey.IssuedAtUtc, RecoveryProposal = recoveryProposal,
            };
            rejection = VoyagerRemoteResumePolicy.Blocker(facts) ?? string.Empty;
            if (rejection.Length != 0) return false;
            evidence = new VoyagerRemoteResumeEvidence(PluginJson.Serialize(journey), PluginJson.Serialize(checkpoint),
                source.Fingerprint, primary.Fingerprint, source.Prefix.GameTick, primary.Prefix.GameTick,
                ticket.GameplayJournalCheckpoint!, recoveryProposal, ticketLease, journeyLease, checkpointLease);
            ticketLease = null; journeyLease = null; checkpointLease = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
            || exception is Newtonsoft.Json.JsonException || exception is ArgumentException)
        { rejection = "remote_resume_original_evidence_unavailable:" + exception.GetType().Name; return false; }
        finally { ticketLease?.Dispose(); journeyLease?.Dispose(); checkpointLease?.Dispose(); }
    }

    public bool RemoteResumeEvidenceStillMatches(VoyagerRemoteResumeEvidence evidence) =>
        PluginJson.Serialize(_voyagerJourney) == evidence.JourneyJson
        && PluginJson.Serialize(_currentTicket) == evidence.CheckpointJson;

    private static T? ReadBoundedResumeRecord<T>(FileStream stream)
    {
        if (stream.Length < 1 || stream.Length > 65536) throw new InvalidDataException("Protected resume proof exceeds its finite bound.");
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
        return PluginJson.Deserialize<T>(reader.ReadToEnd());
    }
}

internal sealed class VoyagerRemoteResumeEvidence : IDisposable
{
    private readonly IDisposable[] _leases;
    public bool IsDisposed { get; private set; }
    public VoyagerRemoteResumeEvidence(string journeyJson, string checkpointJson, string sourceFingerprint,
        string primaryFingerprint, long sourceGameTick, long primaryGameTick, OwnedWorldGameplayJournalCheckpoint journal,
        VoyagerRecoveryWindowProposal? recoveryProposal, params IDisposable[] leases)
    {
        JourneyJson = journeyJson; CheckpointJson = checkpointJson; SourceFingerprint = sourceFingerprint;
        PrimaryFingerprint = primaryFingerprint; SourceGameTick = sourceGameTick; PrimaryGameTick = primaryGameTick;
        JournalJson = PluginJson.Serialize(journal); _leases = leases; RecoveryProposal = recoveryProposal;
        Fingerprint = CanonicalStateHash.Combine("voyager-remote-last-exit-v1", journeyJson, checkpointJson,
            sourceFingerprint, primaryFingerprint, sourceGameTick, primaryGameTick, JournalJson, PluginJson.Serialize(recoveryProposal));
    }
    public string JourneyJson { get; }
    public string CheckpointJson { get; }
    public string SourceFingerprint { get; }
    public string PrimaryFingerprint { get; }
    public long SourceGameTick { get; }
    public long PrimaryGameTick { get; }
    public string JournalJson { get; }
    public string Fingerprint { get; }
    public VoyagerRecoveryWindowProposal? RecoveryProposal { get; }
    public VoyagerJourneyRecord Journey => PluginJson.Deserialize<VoyagerJourneyRecord>(JourneyJson)!;
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        foreach (var lease in _leases) lease.Dispose();
    }
}
