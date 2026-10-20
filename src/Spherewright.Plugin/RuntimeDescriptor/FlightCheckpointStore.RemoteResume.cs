using System.Text;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.RuntimeDescriptor;

internal sealed partial class FlightCheckpointStore
{
    public bool TryOpenRemoteResumeEvidence(OwnedWorldResumeTicket ticket, OwnedSaveRecoveryLease source,
        OwnedSaveRecoveryLease primary, out VoyagerRemoteResumeEvidence? evidence, out string rejection,
        VoyagerRecoveryWindowProposal? recoveryProposal = null, PriorFailedResumeProof? priorFailedProof = null,
        string priorSourceFingerprint = "", bool priorActionKnownTerminalFailed = false, string priorLegacySourceFingerprint = "")
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
            var journeyJson = PluginJson.Serialize(journey);
            var checkpointJson = PluginJson.Serialize(checkpoint);
            var journalJson = PluginJson.Serialize(ticket.GameplayJournalCheckpoint);
            var originalActionMatches = !string.IsNullOrEmpty(ticket.QuarantineActionId)
                && ticket.QuarantineActionId == journey.ActiveActionId;
            if (priorFailedProof is not null)
            {
                if (!priorActionKnownTerminalFailed || originalActionMatches
                    || source.Prefix.GameTick != ticket.MinimumGameTick
                    || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(priorSourceFingerprint)
                    || priorFailedProof.Authorization.ResumeActionId != ticket.QuarantineActionId)
                { rejection = "remote_resume_prior_failed_zero_progress_unproved"; return false; }
                // Same Unity serializer/runtime as the original commitment. Do not accept
                // caller-supplied JSON, paths or desktop-normalized floating-point lexemes.
                string opening;
                if (priorFailedProof.LegacyAncestor is { } ancestor)
                {
                    if (!OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(priorLegacySourceFingerprint)
                        || ancestor.Authorization.PriorConsumedResumeTokenHash is not null
                        || priorFailedProof.Authorization.PriorConsumedResumeTokenHash != ancestor.TokenHash)
                    { rejection = "remote_resume_fixed_v2_ancestor_unproved"; return false; }
                    var legacyOpening = VoyagerRemoteResumeEvidence.LegacyFingerprint(journeyJson, checkpointJson,
                        priorLegacySourceFingerprint, primary.Fingerprint, source.Prefix.GameTick, primary.Prefix.GameTick,
                        journalJson, ancestor.Authorization.Proposal);
                    if (legacyOpening != ancestor.Authorization.EvidenceFingerprint)
                    { rejection = "remote_resume_fixed_v2_legacy_opening_failed"; return false; }
                    var previousCore = VoyagerRemoteResumeEvidence.LegacyFingerprint(journeyJson, checkpointJson,
                        priorSourceFingerprint, primary.Fingerprint, source.Prefix.GameTick, primary.Prefix.GameTick,
                        journalJson, priorFailedProof.Authorization.Proposal);
                    opening = CanonicalStateHash.Combine("voyager-remote-last-exit-prior-failed-v1", previousCore,
                        ancestor.Fingerprint, priorLegacySourceFingerprint, ancestor.Authorization.ResumeActionId, true);
                }
                else
                {
                    if (priorFailedProof.Authorization.PriorConsumedResumeTokenHash is not null
                        || !string.IsNullOrEmpty(priorLegacySourceFingerprint))
                    { rejection = "remote_resume_prior_failed_legacy_required"; return false; }
                    opening = VoyagerRemoteResumeEvidence.LegacyFingerprint(journeyJson, checkpointJson,
                        priorSourceFingerprint, primary.Fingerprint, source.Prefix.GameTick, primary.Prefix.GameTick,
                        journalJson, priorFailedProof.Authorization.Proposal);
                }
                if (!string.Equals(opening, priorFailedProof.Authorization.EvidenceFingerprint, StringComparison.Ordinal))
                { rejection = "remote_resume_prior_commitment_opening_failed"; return false; }
                originalActionMatches = true; // Exact current route/CP/Journal/primary commitment opened.
            }
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
                QuarantineMatchesOriginalAction = originalActionMatches,
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
            evidence = new VoyagerRemoteResumeEvidence(journeyJson, checkpointJson,
                source.Fingerprint, primary.Fingerprint, source.Prefix.GameTick, primary.Prefix.GameTick,
                ticket.GameplayJournalCheckpoint!, recoveryProposal, priorFailedProof, priorSourceFingerprint,
                ticket.QuarantineActionId, priorLegacySourceFingerprint, ticketLease, journeyLease, checkpointLease);
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
        : this(journeyJson, checkpointJson, sourceFingerprint, primaryFingerprint, sourceGameTick, primaryGameTick,
            journal, recoveryProposal, null, "", "", "", leases) { }

    public VoyagerRemoteResumeEvidence(string journeyJson, string checkpointJson, string sourceFingerprint,
        string primaryFingerprint, long sourceGameTick, long primaryGameTick, OwnedWorldGameplayJournalCheckpoint journal,
        VoyagerRecoveryWindowProposal? recoveryProposal, PriorFailedResumeProof? priorFailedProof,
        string priorSourceFingerprint, string quarantineActionId, string priorLegacySourceFingerprint, params IDisposable[] leases)
    {
        JourneyJson = journeyJson; CheckpointJson = checkpointJson; SourceFingerprint = sourceFingerprint;
        PrimaryFingerprint = primaryFingerprint; SourceGameTick = sourceGameTick; PrimaryGameTick = primaryGameTick;
        JournalJson = PluginJson.Serialize(journal); RecoveryProposal = recoveryProposal;
        _leases = priorFailedProof is null ? leases : leases.Concat(new IDisposable[] { priorFailedProof }).ToArray();
        PriorConsumedResumeTokenHash = priorFailedProof?.TokenHash;
        var currentFingerprint = LegacyFingerprint(journeyJson, checkpointJson, sourceFingerprint, primaryFingerprint,
            sourceGameTick, primaryGameTick, JournalJson, recoveryProposal);
        Fingerprint = priorFailedProof is null ? currentFingerprint
            : CanonicalStateHash.Combine("voyager-remote-last-exit-prior-failed-v1", currentFingerprint,
                priorFailedProof.Fingerprint, priorSourceFingerprint, quarantineActionId, true);
        PriorV2ProvenanceUsed = priorFailedProof?.LegacyAncestor is not null;
        if (PriorV2ProvenanceUsed)
            Fingerprint = CanonicalStateHash.Combine("voyager-remote-last-exit-prior-failed-v2", currentFingerprint,
                priorFailedProof!.Fingerprint, priorFailedProof.LegacyAncestor!.Fingerprint,
                priorSourceFingerprint, priorLegacySourceFingerprint, quarantineActionId, true);
    }
    internal static string LegacyFingerprint(string journeyJson, string checkpointJson, string sourceFingerprint,
        string primaryFingerprint, long sourceGameTick, long primaryGameTick, string journalJson,
        VoyagerRecoveryWindowProposal? proposal) =>
        CanonicalStateHash.Combine("voyager-remote-last-exit-v1", journeyJson, checkpointJson, sourceFingerprint,
            primaryFingerprint, sourceGameTick, primaryGameTick, journalJson, PluginJson.Serialize(proposal));
    public string? PriorConsumedResumeTokenHash { get; }
    public bool PriorV2ProvenanceUsed { get; }
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
