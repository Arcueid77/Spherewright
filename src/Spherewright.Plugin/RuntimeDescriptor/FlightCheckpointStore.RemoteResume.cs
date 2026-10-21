using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.RuntimeDescriptor;

internal sealed partial class FlightCheckpointStore
{
    public bool TryOpenRemoteResumeEvidence(OwnedWorldResumeTicket ticket, OwnedSaveRecoveryLease source,
        OwnedSaveRecoveryLease primary, out VoyagerRemoteResumeEvidence? evidence, out string rejection,
        VoyagerRecoveryWindowProposal? recoveryProposal = null, PriorFailedResumeProof? priorFailedProof = null,
        string priorSourceFingerprint = "", bool priorActionKnownTerminalFailed = false, string priorLegacySourceFingerprint = "",
        string priorIntermediateSourceFingerprint = "", bool failedRemoteReauthorization = false,
        string expectedRemoteContext = "", string validatedJournalHash = "",
        string legacyLocalMoveEvidenceJson = "", string legacyLocalMoveEvidenceHash = "", string legacyCredentialRawFingerprint = "")
    {
        evidence = null;
        rejection = string.Empty;
        FileStream? ticketLease = null; FileStream? journeyLease = null; OwnedSaveRecoveryLease? checkpointLease = null;
        try
        {
            if ((failedRemoteReauthorization || !string.IsNullOrEmpty(legacyLocalMoveEvidenceJson))
                && ((File.GetAttributes(_ticketPath) & FileAttributes.ReparsePoint) != 0
                || (File.GetAttributes(VoyagerJourneyPath) & FileAttributes.ReparsePoint) != 0))
                throw new InvalidDataException("The current original context must use regular protected records.");
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
            if (priorFailedProof is not null && !failedRemoteReauthorization)
            {
                if (!priorActionKnownTerminalFailed || originalActionMatches
                    || source.Prefix.GameTick != ticket.MinimumGameTick
                    || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(priorSourceFingerprint)
                    || priorFailedProof.Authorization.ResumeActionId != ticket.QuarantineActionId)
                { rejection = "remote_resume_prior_failed_zero_progress_unproved"; return false; }
                // Same Unity serializer/runtime as the original commitment. Do not accept
                // caller-supplied JSON, paths or desktop-normalized floating-point lexemes.
                string opening;
                if (priorFailedProof.LegacyAncestor is { LegacyAncestor: { } oldest } intermediate)
                {
                    if (!OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(priorLegacySourceFingerprint)
                        || !OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(priorIntermediateSourceFingerprint)
                        || oldest.LegacyAncestor is not null || oldest.Authorization.PriorConsumedResumeTokenHash is not null
                        || intermediate.Authorization.PriorConsumedResumeTokenHash != oldest.TokenHash
                        || priorFailedProof.Authorization.PriorConsumedResumeTokenHash != intermediate.TokenHash)
                    { rejection = "remote_resume_fixed_v3_ancestor_unproved"; return false; }
                    var legacyOpening = VoyagerRemoteResumeEvidence.LegacyFingerprint(journeyJson, checkpointJson,
                        priorLegacySourceFingerprint, primary.Fingerprint, source.Prefix.GameTick, primary.Prefix.GameTick,
                        journalJson, oldest.Authorization.Proposal);
                    if (legacyOpening != oldest.Authorization.EvidenceFingerprint)
                    { rejection = "remote_resume_fixed_v3_legacy_opening_failed"; return false; }
                    var middleCore = VoyagerRemoteResumeEvidence.LegacyFingerprint(journeyJson, checkpointJson,
                        priorIntermediateSourceFingerprint, primary.Fingerprint, source.Prefix.GameTick, primary.Prefix.GameTick,
                        journalJson, intermediate.Authorization.Proposal);
                    var middleOpening = CanonicalStateHash.Combine("voyager-remote-last-exit-prior-failed-v1", middleCore,
                        oldest.Fingerprint, priorLegacySourceFingerprint, oldest.Authorization.ResumeActionId, true);
                    if (middleOpening != intermediate.Authorization.EvidenceFingerprint)
                    { rejection = "remote_resume_fixed_v3_intermediate_opening_failed"; return false; }
                    var newestCore = VoyagerRemoteResumeEvidence.LegacyFingerprint(journeyJson, checkpointJson,
                        priorSourceFingerprint, primary.Fingerprint, source.Prefix.GameTick, primary.Prefix.GameTick,
                        journalJson, priorFailedProof.Authorization.Proposal);
                    opening = CanonicalStateHash.Combine("voyager-remote-last-exit-prior-failed-v2", newestCore,
                        intermediate.Fingerprint, oldest.Fingerprint, priorIntermediateSourceFingerprint,
                        priorLegacySourceFingerprint, intermediate.Authorization.ResumeActionId, true);
                }
                else if (priorFailedProof.LegacyAncestor is { } ancestor)
                {
                    if (!OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(priorLegacySourceFingerprint)
                        || !string.IsNullOrEmpty(priorIntermediateSourceFingerprint)
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
                        || !string.IsNullOrEmpty(priorLegacySourceFingerprint) || !string.IsNullOrEmpty(priorIntermediateSourceFingerprint))
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
            var currentContext = failedRemoteReauthorization
                ? VoyagerFailedRemoteReauthorizationPolicy.ContextFingerprint(journeyJson, checkpointJson,
                    checkpointLease.Fingerprint, primary.Fingerprint, source.Prefix.GameTick, primary.Prefix.GameTick,
                    journalJson, validatedJournalHash) : string.Empty;
            if (!string.IsNullOrEmpty(legacyLocalMoveEvidenceJson))
            {
                if (priorFailedProof is not null || failedRemoteReauthorization
                    || VoyagerLegacyLocalMoveUnknownPolicy.HashEvidence(legacyLocalMoveEvidenceJson) != legacyLocalMoveEvidenceHash)
                { rejection = "legacy_local_move_evidence_hash_or_mode_mismatch"; return false; }
                var capture = ParseLegacyLocalMoveCapture(legacyLocalMoveEvidenceJson);
                rejection = VoyagerLegacyLocalMoveUnknownPolicy.Blocker(capture, new LegacyLocalMoveUnknownBinding
                {
                    OwnedSaveName = ticket.OwnedSaveName, SourceSessionId = ticket.SourceSessionId,
                    NativeVersion = ticket.GameVersion, QuarantineActionId = ticket.QuarantineActionId,
                    SuccessfulFlightActionId = journey.ActiveActionId, RemotePlanetId = journey.RemotePlanetId,
                    CredentialRawFingerprint = legacyCredentialRawFingerprint,
                    JourneySha256 = HashResumeRecord(journeyLease), CheckpointSha256 = HashResumeRecord(ticketLease),
                    JournalSha256 = validatedJournalHash,
                }, facts) ?? string.Empty;
                if (rejection.Length != 0) return false;
                evidence = VoyagerRemoteResumeEvidence.CreateLegacyLocalMoveUnknown(journeyJson, checkpointJson,
                    source.Fingerprint, primary.Fingerprint, source.Prefix.GameTick, primary.Prefix.GameTick,
                    ticket.GameplayJournalCheckpoint!, recoveryProposal, legacyLocalMoveEvidenceJson,
                    legacyLocalMoveEvidenceHash, ticket.QuarantineActionId, ticket.SourceSessionId,
                    ticketLease, journeyLease, checkpointLease);
                ticketLease = null; journeyLease = null; checkpointLease = null;
                return true;
            }
            rejection = failedRemoteReauthorization
                ? VoyagerFailedRemoteReauthorizationPolicy.Blocker(facts, expectedRemoteContext, currentContext,
                    priorFailedProof is not null && priorFailedProof.LegacyAncestor is null
                        && priorFailedProof.Authorization.ResumeActionId == ticket.QuarantineActionId
                        && priorFailedProof.ConsumedAtUtc <= ticket.IssuedAtUtc
                        && OwnedWorldRecoveryPolicy.IsCanonicalFingerprint("sha256:" + validatedJournalHash),
                    priorActionKnownTerminalFailed, checkpoint.SuccessfulFlightAtGameTick,
                    priorFailedProof?.Authorization.Proposal) ?? string.Empty
                : VoyagerRemoteResumePolicy.Blocker(facts) ?? string.Empty;
            if (rejection.Length != 0) return false;
            evidence = failedRemoteReauthorization
                ? VoyagerRemoteResumeEvidence.CreateFailedRemoteReauthorization(journeyJson, checkpointJson,
                    source.Fingerprint, primary.Fingerprint, source.Prefix.GameTick, primary.Prefix.GameTick,
                    ticket.GameplayJournalCheckpoint!, recoveryProposal!, priorFailedProof!, currentContext,
                    ticket.QuarantineActionId, ticketLease, journeyLease, checkpointLease)
                : new VoyagerRemoteResumeEvidence(journeyJson, checkpointJson,
                source.Fingerprint, primary.Fingerprint, source.Prefix.GameTick, primary.Prefix.GameTick,
                ticket.GameplayJournalCheckpoint!, recoveryProposal, priorFailedProof, priorSourceFingerprint,
                ticket.QuarantineActionId, priorLegacySourceFingerprint, priorIntermediateSourceFingerprint, ticketLease, journeyLease, checkpointLease);
            ticketLease = null; journeyLease = null; checkpointLease = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException
            || exception is Newtonsoft.Json.JsonException || exception is ArgumentException)
        { rejection = "remote_resume_original_evidence_unavailable:" + exception.GetType().Name; return false; }
        finally { ticketLease?.Dispose(); journeyLease?.Dispose(); checkpointLease?.Dispose(); }
    }

    internal static string HashResumeRecord(FileStream stream)
    {
        var position = stream.Position;
        try
        {
            stream.Position = 0;
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        finally { stream.Position = position; }
    }

    private static JObject StrictLegacyObject(string json)
    {
        using var input = new StringReader(json);
        using var reader = new JsonTextReader(input) { MaxDepth = 32, DateParseHandling = DateParseHandling.None };
        var result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (reader.Read()) throw new InvalidDataException("Trailing legacy evidence content.");
        return result;
    }

    private static LegacyLocalMoveUnknownCapture ParseLegacyLocalMoveCapture(string json)
    {
        var root = StrictLegacyObject(json);
        var fields = new[] { "sourceProductSha", "captureAttested", "actionResponseJson", "sessionResponseJson",
            "ticketSha256", "journeySha256", "checkpointSha256", "journalSha256" };
        if (root.Count != fields.Length || fields.Any(k => root[k] is null)
            || fields.Where(k => k != "captureAttested").Any(k => root[k]!.Type != JTokenType.String)
            || root["captureAttested"]!.Type != JTokenType.Boolean)
            throw new InvalidDataException("The exact bounded legacy carrier schema is required.");
        var actionResponse = StrictLegacyObject((string)root["actionResponseJson"]!);
        var sessionResponse = StrictLegacyObject((string)root["sessionResponseJson"]!);
        if (actionResponse["success"]?.Type != JTokenType.Boolean || (bool)actionResponse["success"]! != true
            || sessionResponse["success"]?.Type != JTokenType.Boolean || (bool)sessionResponse["success"]! != true
            || actionResponse["result"] is not JObject action || sessionResponse["result"] is not JObject session
            || new[] { "terminal", "succeeded", "reconciledFromOutcomeUnknown" }.Any(k => action[k]?.Type != JTokenType.Boolean)
            || new[] { "startedAtGameTick", "completedAtGameTick", "planetId" }.Any(k => action[k]?.Type != JTokenType.Integer)
            || new[] { "gameLoaded", "ownedBySpherewright", "writesAllowed", "currentSessionLoadedFromFlightCheckpoint" }
                .Any(k => session[k]?.Type != JTokenType.Boolean)
            || new[] { "gameTick", "lastOwnedSaveGameTick", "localPlanetId" }.Any(k => session[k]?.Type != JTokenType.Integer))
            throw new InvalidDataException("The legacy public captures omit required typed observations.");
        return new LegacyLocalMoveUnknownCapture
        {
            SourceProductSha = (string)root["sourceProductSha"]!, CaptureAttested = (bool)root["captureAttested"]!,
            Action = PluginJson.Deserialize<ActionResultSnapshot>(action.ToString(Formatting.None)),
            Session = PluginJson.Deserialize<SessionState>(session.ToString(Formatting.None)),
            TicketSha256 = (string)root["ticketSha256"]!, JourneySha256 = (string)root["journeySha256"]!,
            CheckpointSha256 = (string)root["checkpointSha256"]!, JournalSha256 = (string)root["journalSha256"]!,
        };
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
        : this(journeyJson, checkpointJson, sourceFingerprint, primaryFingerprint, sourceGameTick, primaryGameTick,
            journal, recoveryProposal, priorFailedProof, priorSourceFingerprint, quarantineActionId, priorLegacySourceFingerprint, "", leases) { }

    public VoyagerRemoteResumeEvidence(string journeyJson, string checkpointJson, string sourceFingerprint,
        string primaryFingerprint, long sourceGameTick, long primaryGameTick, OwnedWorldGameplayJournalCheckpoint journal,
        VoyagerRecoveryWindowProposal? recoveryProposal, PriorFailedResumeProof? priorFailedProof,
        string priorSourceFingerprint, string quarantineActionId, string priorLegacySourceFingerprint,
        string priorIntermediateSourceFingerprint, params IDisposable[] leases)
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
        PriorV3ProvenanceUsed = priorFailedProof?.LegacyAncestor?.LegacyAncestor is not null;
        if (PriorV3ProvenanceUsed)
            Fingerprint = CanonicalStateHash.Combine("voyager-remote-last-exit-prior-failed-v3", currentFingerprint,
                priorFailedProof!.Fingerprint, priorFailedProof.LegacyAncestor!.Fingerprint,
                priorFailedProof.LegacyAncestor.LegacyAncestor!.Fingerprint, priorSourceFingerprint,
                priorIntermediateSourceFingerprint, priorLegacySourceFingerprint, quarantineActionId, true);
    }
    internal static string LegacyFingerprint(string journeyJson, string checkpointJson, string sourceFingerprint,
        string primaryFingerprint, long sourceGameTick, long primaryGameTick, string journalJson,
        VoyagerRecoveryWindowProposal? proposal) =>
        CanonicalStateHash.Combine("voyager-remote-last-exit-v1", journeyJson, checkpointJson, sourceFingerprint,
            primaryFingerprint, sourceGameTick, primaryGameTick, journalJson, PluginJson.Serialize(proposal));
    public string? PriorConsumedResumeTokenHash { get; }
    public bool PriorV2ProvenanceUsed { get; }
    public bool PriorV3ProvenanceUsed { get; }
    public string JourneyJson { get; }
    public string CheckpointJson { get; }
    public string SourceFingerprint { get; }
    public string PrimaryFingerprint { get; }
    public long SourceGameTick { get; }
    public long PrimaryGameTick { get; }
    public string JournalJson { get; }
    public string Fingerprint { get; private set; }
    public bool FailedRemoteLastExitReauthorization { get; private set; }
    public string RemoteRecoveryContextFingerprint { get; private set; } = string.Empty;
    public string LegacyLocalMoveUnknownEvidenceJson { get; private set; } = string.Empty;
    public string LegacyLocalMoveUnknownEvidenceHash { get; private set; } = string.Empty;
    internal static VoyagerRemoteResumeEvidence CreateLegacyLocalMoveUnknown(string journeyJson, string checkpointJson,
        string sourceFingerprint, string primaryFingerprint, long sourceTick, long primaryTick,
        OwnedWorldGameplayJournalCheckpoint journal, VoyagerRecoveryWindowProposal? proposal,
        string rawEvidence, string evidenceHash, string quarantineActionId, string sourceSessionId, params IDisposable[] leases)
    {
        var result = new VoyagerRemoteResumeEvidence(journeyJson, checkpointJson, sourceFingerprint, primaryFingerprint,
            sourceTick, primaryTick, journal, proposal, leases);
        result.LegacyLocalMoveUnknownEvidenceJson = rawEvidence;
        result.LegacyLocalMoveUnknownEvidenceHash = evidenceHash;
        result.Fingerprint = CanonicalStateHash.Combine("voyager-remote-last-exit-legacy-local-move-unknown-v1",
            result.Fingerprint, evidenceHash, quarantineActionId, sourceSessionId, false);
        return result;
    }
    internal static VoyagerRemoteResumeEvidence CreateFailedRemoteReauthorization(string journeyJson,
        string checkpointJson, string sourceFingerprint, string primaryFingerprint, long sourceTick, long primaryTick,
        OwnedWorldGameplayJournalCheckpoint journal, VoyagerRecoveryWindowProposal proposal,
        PriorFailedResumeProof immediate, string contextFingerprint, string quarantineActionId, params IDisposable[] leases)
    {
        var result = new VoyagerRemoteResumeEvidence(journeyJson, checkpointJson, sourceFingerprint, primaryFingerprint,
            sourceTick, primaryTick, journal, proposal, leases.Concat(new IDisposable[] { immediate }).ToArray());
        result.FailedRemoteLastExitReauthorization = true;
        result.RemoteRecoveryContextFingerprint = contextFingerprint;
        result.Fingerprint = CanonicalStateHash.Combine("voyager-failed-remote-last-exit-reauthorization-v1",
            result.Fingerprint, contextFingerprint, immediate.Fingerprint, quarantineActionId, true);
        return result;
    }
    public VoyagerRecoveryWindowProposal? RecoveryProposal { get; }
    public VoyagerJourneyRecord Journey => PluginJson.Deserialize<VoyagerJourneyRecord>(JourneyJson)!;
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        foreach (var lease in _leases) lease.Dispose();
    }
}
