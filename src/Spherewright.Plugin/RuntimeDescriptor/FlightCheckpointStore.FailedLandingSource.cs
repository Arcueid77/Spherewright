using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.RuntimeDescriptor;

internal sealed partial class FlightCheckpointStore
{
    // Capture only. This neither loads a world nor changes the old failed action,
    // checkpoint, accounting, credentials or admission state.
    internal bool TryOpenFailedLandingSource(OwnedWorldResumeTicketStore credentials, string token,
        PrepareOwnedWorldResumeRequest request, out VoyagerFailedLandingSource? evidence,
        out NativeResumeFailureSnapshot? failure)
    {
        evidence = null; failure = null;
        var e = new NativeResumeDecisionEvidence("failed_landing_source_capture", null, DateTimeOffset.UtcNow, true);
        e.NotReached("protected_credential", "complete_fixed_files", "original_context", "source_policy",
            "owner_commit_confirmation", "native_load", "native_contact", "healthy_takeover");
        var leases = new List<IDisposable>();
        try
        {
            if (VoyagerFailedLandingRecoveryPolicy.ValidateRequest(request) is { } invalid)
            { e.Require("request_contract", false); failure = e.Failure(invalid); return false; }
            if (!credentials.TryGetSavedWorldOpenProvenance(token, out var ticket, out var provenance, out _)
                || ticket is null)
            { e.Require("protected_credential", false); failure = e.Failure("failed_landing_credential_unproved"); return false; }
            e.Require("protected_credential", true);
            foreach (var path in new[] { _ticketPath, VoyagerJourneyPath })
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Original context must use regular protected records.");
            var cpLease = new FileStream(_ticketPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            leases.Add(cpLease);
            var jLease = new FileStream(VoyagerJourneyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            leases.Add(jLease);
            var cp = ReadBoundedResumeRecord<FlightCheckpointTicket>(cpLease);
            var j = ReadBoundedResumeRecord<VoyagerJourneyRecord>(jLease);
            if (cp is null || j is null || !IsStructurallyValid(cp) || !ValidVoyagerJourney(j)
                || PluginJson.Serialize(cp) != PluginJson.Serialize(_currentTicket)
                || PluginJson.Serialize(j) != PluginJson.Serialize(_voyagerJourney))
            { e.Require("original_context", false); failure = e.Failure("failed_landing_original_context_changed"); return false; }
            var source = OwnedSaveRecoveryLease.Open(GameSave.SavePath(GameSave.LastExit), ticket.OwnedSaveName);
            leases.Add(source);
            var primary = OwnedSaveRecoveryLease.Open(GameSave.SavePath(ticket.OwnedSaveName), ticket.OwnedSaveName);
            leases.Add(primary);
            var checkpoint = OwnedSaveRecoveryLease.Open(GameSave.SavePath(cp.CheckpointSaveName), ticket.OwnedSaveName);
            leases.Add(checkpoint);
            var journal = credentials.OpenReauthorizationJournalLease(ticket, provenance, savedWorldOpenOnly: true);
            leases.Add(journal);
            e.Require("complete_fixed_files", true);
            var f = new VoyagerFailedLandingSourceFacts
            {
                FixedLastExit = true, CompleteSourceAndPrimary = true,
                ExactOwnedIdentityAndNativeVersion = source.Prefix.MatchesExpectedIdentity && primary.Prefix.MatchesExpectedIdentity
                    && checkpoint.Prefix.MatchesExpectedIdentity && j.OwnedSaveName == ticket.OwnedSaveName
                    && cp.OwnedSaveName == ticket.OwnedSaveName && ticket.GameVersion == _gameVersion
                    && j.GameVersion == _gameVersion && cp.GameVersion == _gameVersion
                    && source.Prefix.GameVersion == _gameVersion && primary.Prefix.GameVersion == _gameVersion
                    && checkpoint.Prefix.GameVersion == _gameVersion,
                Peaceful = source.Prefix.Peaceful && primary.Prefix.Peaceful && checkpoint.Prefix.Peaceful
                    && !source.Prefix.Sandbox && !primary.Prefix.Sandbox && !checkpoint.Prefix.Sandbox,
                ExactOriginalJourneyCheckpointAndJournal = cp.CheckpointId == j.ActiveCheckpointId
                    && cp.VoyagerJourneyId == j.JourneyId && cp.LastAttemptActionId == j.ActiveActionId
                    && j.LastAccountedFlightActionId == j.ActiveActionId && Guid.TryParse(j.ActiveActionId, out _)
                    && cp.OriginPlanetId == j.HomePlanetId && cp.OriginStarId == j.HomeStarId
                    && cp.DestinationStarId == j.DestinationStarId && !cp.ReloadPending
                    && cp.LastAttemptStartedAtGameTick >= cp.SavedGameTick
                    && cp.RecoveryRequiredAtGameTick >= cp.LastAttemptStartedAtGameTick
                    && cp.RecoveryRequiredAtGameTick <= source.Prefix.GameTick
                    && checkpoint.Fingerprint == cp.CheckpointFileFingerprint && checkpoint.Prefix.GameTick == cp.SavedGameTick
                    && ticket.GameplayJournalCheckpoint is { Version: OwnedWorldGameplayJournalCheckpoint.CurrentVersion } jc
                    && !string.IsNullOrEmpty(jc.JournalId) && jc.MinimumDurableThroughSequence >= 0,
                OriginalCheckpointRecoveryRequired = cp.LifecycleState == FlightCheckpointLifecycleStates.RecoveryRequired,
                OriginalCheckpointFlightSucceeded = cp.SuccessfulFlightAtGameTick.HasValue,
                OriginalOutboundFailureContext = j.Phase == "recovery_required" && !j.ReturnLeg && !j.LastLandedGameTick.HasValue
                    && j.FailedLandingAdmissions.LastOrDefault()?.State is not ("pending_native_load" or "adopted" or "outcome_unknown"),
                // The prefix does not certify the saved local planet. This binds
                // the selected destination; Native must independently prove it after Load.
                ExactSelectedRemotePlanet = j.RemotePlanetId > 0 && j.RemotePlanetId != j.HomePlanetId
                    && (cp.DestinationPlanetId == 0 || cp.DestinationPlanetId == j.RemotePlanetId),
                CallerAttestsTerminalFailure = request.PriorActionKnownTerminalFailed == true,
                SourceGameTick = source.Prefix.GameTick, PrimaryGameTick = primary.Prefix.GameTick,
                TicketMinimumGameTick = Math.Max(ticket.MinimumGameTick, ticket.ObservedProgressGameTickFloor ?? 0),
                CheckpointGameTick = cp.SavedGameTick, LastAccountedGameTick = j.LastAccountedGameTick,
                SpentGameTicks = j.SpentGameTicks, MaximumGameTicks = j.MaximumGameTicks,
                ReturnReserveGameTicks = j.ReturnReserveGameTicks, NowUtc = DateTimeOffset.UtcNow,
            };
            var decision = VoyagerFailedLandingRecoveryPolicy.Evaluate(request, f);
            if (!e.Require("source_policy", decision.Proved))
            { failure = e.Failure(decision.BlockerCode!); return false; }
            var proposal = new VoyagerRecoveryWindowProposal(request.RecoveryDeadlineUtc!.Value, request.RecoveryScope,
                request.MaximumChargeGameTicks, request.MaximumChargeWallSeconds);
            evidence = new(j, cp, ticket, provenance, source, primary, journal, proposal, decision, jLease, leases.ToArray());
            leases.Clear();
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException
            || ex is System.Security.SecurityException || ex is Newtonsoft.Json.JsonException || ex is InvalidDataException)
        {
            e.Require("capture_complete", null);
            failure = e.Failure("failed_landing_source_files_unproved");
            failure.Observations["capture_exception_type"] = ex.GetType().Name;
            return false;
        }
        finally { foreach (var lease in leases) lease.Dispose(); }
    }
}

internal sealed class VoyagerFailedLandingSource : IDisposable
{
    private readonly IDisposable[] _leases;
    private readonly FileStream _journeyLease;
    internal VoyagerFailedLandingSource(VoyagerJourneyRecord journey, FlightCheckpointTicket checkpoint,
        OwnedWorldResumeTicket ticket, string credentialProvenance, OwnedSaveRecoveryLease source,
        OwnedSaveRecoveryLease primary, FileStream journal, VoyagerRecoveryWindowProposal proposal,
        VoyagerFailedLandingRecoveryDecision decision, FileStream journeyLease, params IDisposable[] leases)
    {
        JourneyJson = PluginJson.Serialize(journey); CheckpointJson = PluginJson.Serialize(checkpoint);
        Ticket = PluginJson.Deserialize<OwnedWorldResumeTicket>(PluginJson.Serialize(ticket))!;
        CredentialProvenance = credentialProvenance; Source = source; Primary = primary; Journal = journal;
        Proposal = proposal; SavedAccountingAnchorGameTick = decision.SavedAccountingAnchorGameTick!.Value;
        ConservedSpentGameTicks = decision.ConservedSpentGameTicks!.Value;
        Fingerprint = CanonicalStateHash.Combine("failed-landing-source-v1", JourneyJson, CheckpointJson,
            PluginJson.Serialize(ticket.GameplayJournalCheckpoint), credentialProvenance, source.Fingerprint,
            primary.Fingerprint, SavedAccountingAnchorGameTick, ConservedSpentGameTicks, PluginJson.Serialize(proposal));
        _leases = leases; _journeyLease = journeyLease;
    }
    internal string JourneyJson { get; }
    internal string CheckpointJson { get; }
    internal VoyagerJourneyRecord Journey => PluginJson.Deserialize<VoyagerJourneyRecord>(JourneyJson)!;
    internal OwnedWorldResumeTicket Ticket { get; }
    internal string CredentialProvenance { get; }
    internal OwnedSaveRecoveryLease Source { get; }
    internal OwnedSaveRecoveryLease Primary { get; }
    internal FileStream Journal { get; }
    internal VoyagerRecoveryWindowProposal Proposal { get; }
    internal long SavedAccountingAnchorGameTick { get; }
    internal long ConservedSpentGameTicks { get; }
    internal string Fingerprint { get; }
    internal bool IsDisposed { get; private set; }
    internal void ReleaseJourneyLeaseForRegistration() => _journeyLease.Dispose();
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        foreach (var lease in _leases) lease.Dispose();
    }
}
