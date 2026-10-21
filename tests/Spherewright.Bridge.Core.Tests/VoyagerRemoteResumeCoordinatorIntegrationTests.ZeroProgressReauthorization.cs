using System.Security.Cryptography;
using System.Text;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Journals;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Fact]
    public void NewAuthorityBindsCurrentContextAndOnlyImmediatePairWithoutOpeningOldCommitments()
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedZeroProgressReauthorization();
        var originalJourney = File.ReadAllBytes(Path.Combine(s.Handoff, "voyager-journey.json"));
        var p = s.Prepare(r);
        Assert.True(p.FailedRemoteLastExitReauthorizationUsed);
        Assert.Equal(r.ExpectedRemoteRecoveryContextFingerprint, p.RemoteRecoveryContextFingerprint);
        Assert.False(p.PriorFailedResumeProvenanceUsed); Assert.False(p.PriorFailedResumeV2ProvenanceUsed);
        Assert.False(p.PriorFailedResumeV3ProvenanceUsed); Assert.True(p.PriorActionTerminalFailureAttested);
        Assert.True(p.UserConfirmationRequired); Assert.False(p.CommitAllowedNow);
        Assert.Contains("not an opening", p.ConfirmationPrompt);
        Assert.Contains("not a Native outcome certificate", p.ConfirmationPrompt);
        Assert.Equal(12501, p.CandidateGameTick); Assert.Equal(s.OriginalDeadline, p.OriginalJourneyDeadlineUtc);
        foreach (var path in s.ZeroProgressEvidencePaths)
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        DSPGame.BeforeLoad = () => Assert.False(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
        var accepted = s.Commit(p); Assert.True(accepted.Success, accepted.Error?.Message);
        Assert.Equal(1, DSPGame.LoadCalls); Assert.Equal(GameSave.LastExit, DSPGame.LoadedName);
        Assert.False(s.Sessions.Reauthorizing); Assert.False(s.Sessions.FixedAutosave);
        Assert.Equal(OwnedWorldResumeSourceKind.LastExit, s.Sessions.ResumeSource);
        Assert.True(s.Sessions.RemoteLoaderObserverPrepared); Assert.True(s.Sessions.RemoteLoaderObserverBound);
        Assert.True(s.Sessions.RemoteEvidence!.FailedRemoteLastExitReauthorization);
        Assert.Null(s.Sessions.RemoteEvidence.PriorConsumedResumeTokenHash);
        var authorization = s.Sessions.RecoveryAuthorization!;
        Assert.Equal(accepted.Value!.ActionId, authorization.ResumeActionId);
        Assert.Equal(r.RecoveryDeadlineUtc, authorization.Proposal.DeadlineUtc);
        Assert.Null(authorization.PriorConsumedResumeTokenHash);
        var tombstone = PluginJson.Deserialize<OwnedWorldResumeConsumptionTombstone>(File.ReadAllText(s.CurrentConsumptionPath))!;
        Assert.Equal(authorization.EvidenceFingerprint, tombstone.RecoveryAuthorization!.EvidenceFingerprint);
        Assert.StartsWith("sha256:", authorization.EvidenceFingerprint);
        Assert.Equal(originalJourney, File.ReadAllBytes(Path.Combine(s.Handoff, "voyager-journey.json")));
        foreach (var path in s.ProtectedEvidencePaths.Concat(s.PriorConsumptionPaths))
            Assert.ThrowsAny<IOException>(() => { using var _ = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
        s.Sessions.Dispose();
        foreach (var path in s.ProtectedEvidencePaths.Concat(s.PriorConsumptionPaths))
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Theory]
    [InlineData("context")] [InlineData("journal_text")] [InlineData("journal_prefix")] [InlineData("newer_journal")]
    [InlineData("source_after_min")] [InlineData("accounted")] [InlineData("cp_success")] [InlineData("newer_primary")]
    [InlineData("source_version")] [InlineData("identity")] [InlineData("wrong_q")] [InlineData("wrong_token")]
    [InlineData("pair_raw")] [InlineData("pair_missing")] [InlineData("pair_partial")] [InlineData("pair_oversize")]
    [InlineData("no_auth")] [InlineData("auth_version")] [InlineData("reverse_time")] [InlineData("future_consumed")]
    [InlineData("cap_ticks")] [InlineData("cap_wall")] [InlineData("expired_ticket")]
    [InlineData("active_pair_raw")] [InlineData("legacy_default")]
    public void NewModeRejectsUnprovedCurrentFactsWithoutAnyNativeDispatch(string fault)
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedZeroProgressReauthorization();
        switch (fault)
        {
            case "context": r.ExpectedRemoteRecoveryContextFingerprint = CanonicalStateHash.Combine("different-owner-context"); break;
            case "journal_text": File.AppendAllText(s.JournalPath, " "); break;
            case "journal_prefix": s.RearmQuarantine(s.PriorAuthorization!.ResumeActionId, changedJournal: true, minimum: 12501); break;
            case "newer_journal":
                var j = PluginJson.Deserialize<GameplayJournalDocument>(File.ReadAllText(s.JournalPath))!;
                j.Entries.Add(new() { Sequence = 2 }); File.WriteAllText(s.JournalPath, PluginJson.Serialize(j)); break;
            case "source_after_min": s.WriteSave(GameSave.LastExit, 12502); r.ExpectedRemoteRecoveryContextFingerprint = s.CurrentOwnerContext(); break;
            case "accounted": var journey = s.Checkpoints.VoyagerJourney!; journey.LastAccountedGameTick++; Assert.True(s.Checkpoints.TryPersistVoyagerJourney(journey, out _)); r.ExpectedRemoteRecoveryContextFingerprint = s.CurrentOwnerContext(); break;
            case "cp_success":
                var cpPath = Path.Combine(s.Handoff, "flight-checkpoint.json");
                var cpRecord = PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(cpPath))!;
                cpRecord.SuccessfulFlightAtGameTick++; File.WriteAllText(cpPath, PluginJson.Serialize(cpRecord));
                r.ExpectedRemoteRecoveryContextFingerprint = s.CurrentOwnerContext(); break;
            case "newer_primary": s.WriteSave(Scenario.Identity, 12502); r.ExpectedRemoteRecoveryContextFingerprint = s.CurrentOwnerContext(); break;
            case "source_version": s.ChangePrior(t => t.GameVersion = "unsupported"); break;
            case "identity": s.ChangePrior(t => t.RecoveryAuthorization = s.CopyAuthorization(evidence: "not-canonical")); break;
            case "wrong_q": s.RearmQuarantine(Guid.NewGuid().ToString(), minimum: 12501); break;
            case "wrong_token": r.PriorConsumedResumeToken = "different-synthetic-token"; break;
            case "pair_raw": File.AppendAllText(s.PriorConsumptionPaths[1], " "); break;
            case "pair_missing": File.Delete(s.PriorConsumptionPaths[1]); break;
            case "pair_partial": File.WriteAllText(s.PriorConsumptionPaths[0], "{"); break;
            case "pair_oversize": File.WriteAllText(s.PriorConsumptionPaths[0], new string('x', 65537)); break;
            case "no_auth": s.ChangePrior(t => t.RecoveryAuthorization = null); break;
            case "auth_version": s.ChangePrior(t => t.Version = 2); break;
            case "reverse_time": s.ChangePrior(t => t.ConsumedAtUtc = s.PriorAuthorization!.ConfirmedAtUtc.AddSeconds(-1)); break;
            case "future_consumed": s.ChangePrior(t => t.ConsumedAtUtc = DateTimeOffset.UtcNow.AddHours(1)); break;
            case "cap_ticks": r.MaximumChargeGameTicks++; break;
            case "cap_wall": r.MaximumChargeWallSeconds++; break;
            case "expired_ticket": s.ReplaceCurrentTicket(t => t.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1)); break;
            case "active_pair_raw": File.AppendAllText(s.HandoffTicket, " "); break;
            case "legacy_default": r.RecoveryMode = OwnedWorldResumeModes.Default; break;
        }
        Assert.False(s.PrepareResult(r).Success); Assert.Equal(0, DSPGame.LoadCalls);
        foreach (var path in s.ZeroProgressEvidencePaths.Where(File.Exists))
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Theory]
    [InlineData(null)] [InlineData("budget")] [InlineData("accounted")]
    public void CurrentNativeMinimumAfterLocalWorkStillRequiresOriginalAccountingAndReturnBudget(string? fault)
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedZeroProgressReauthorization();
        var journey = s.Checkpoints.VoyagerJourney!;
        var sourceTick = journey.LastLandedGameTick!.Value + 600;
        if (fault == "budget")
            sourceTick = journey.LastLandedGameTick.Value + journey.MaximumGameTicks - journey.SpentGameTicks
                - journey.ReturnReserveGameTicks - 1 - VoyagerLocalWalkReservePolicy.StopGameTicks + 1;
        if (fault == "accounted")
        {
            journey.LastAccountedGameTick++;
            Assert.True(s.Checkpoints.TryPersistVoyagerJourney(journey, out _));
        }
        s.WriteSave(GameSave.LastExit, sourceTick);
        s.RearmQuarantine(s.PriorAuthorization!.ResumeActionId, minimum: sourceTick);
        r.ExpectedRemoteRecoveryContextFingerprint = s.CurrentOwnerContext();
        var originalJourney = File.ReadAllBytes(Path.Combine(s.Handoff, "voyager-journey.json"));
        var originalCheckpoint = File.ReadAllBytes(Path.Combine(s.Handoff, "flight-checkpoint.json"));

        var prepared = s.PrepareResult(r);
        if (fault is not null)
        {
            Assert.False(prepared.Success); Assert.Equal(0, DSPGame.LoadCalls);
            foreach (var path in s.ZeroProgressEvidencePaths)
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            return;
        }
        Assert.True(prepared.Success, prepared.Error?.Message);
        var p = prepared.Value!;
        Assert.Equal(sourceTick, p.CandidateGameTick); Assert.Equal(sourceTick, p.MinimumGameTick);
        Assert.Equal(s.OriginalDeadline, p.OriginalJourneyDeadlineUtc);
        Assert.Equal(r.RecoveryDeadlineUtc, p.EffectiveRecoveryDeadlineUtc);
        Assert.True(p.FailedRemoteLastExitReauthorizationUsed); Assert.True(p.PriorActionTerminalFailureAttested);
        Assert.False(p.PriorFailedResumeProvenanceUsed); Assert.False(p.PriorFailedResumeV2ProvenanceUsed);
        Assert.False(p.PriorFailedResumeV3ProvenanceUsed);
        DSPGame.BeforeLoad = () => Assert.False(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
        var accepted = s.Commit(p); Assert.True(accepted.Success, accepted.Error?.Message);
        Assert.Equal(1, DSPGame.LoadCalls); Assert.Equal(GameSave.LastExit, DSPGame.LoadedName);
        Assert.Equal(originalJourney, File.ReadAllBytes(Path.Combine(s.Handoff, "voyager-journey.json")));
        Assert.Equal(originalCheckpoint, File.ReadAllBytes(Path.Combine(s.Handoff, "flight-checkpoint.json")));
    }

    [Theory]
    [InlineData("source")] [InlineData("journal")] [InlineData("prior_pair")]
    [InlineData("active_pair")] [InlineData("both_active_bom")] [InlineData("consume")] [InlineData("confirmation")]
    public void FreshCommitReopensEverythingAndRefusesAnyChangedOrUnconsumableDisclosure(string fault)
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedZeroProgressReauthorization(); var p = s.Prepare(r);
        switch (fault)
        {
            case "source": File.AppendAllText(GameSave.SavePath(GameSave.LastExit), "changed-payload"); break;
            case "journal": File.AppendAllText(s.JournalPath, " "); break;
            case "prior_pair": s.ChangePrior(t => t.ConsumedAtUtc = t.ConsumedAtUtc.AddTicks(-1)); break;
            case "active_pair": File.AppendAllText(s.HandoffTicket, " "); break;
            case "both_active_bom":
                foreach (var path in new[] { s.RuntimeTicket, s.HandoffTicket })
                    File.WriteAllBytes(path, new byte[] { 0xef, 0xbb, 0xbf }.Concat(File.ReadAllBytes(path)).ToArray());
                break;
            case "consume":
                File.WriteAllText(Path.Combine(s.Runtime, ".test-only-fail-secure-write"), "synthetic failure");
                File.WriteAllText(Path.Combine(s.Handoff, ".test-only-fail-secure-write"), "synthetic failure"); break;
        }
        Assert.False(s.Commit(p, confirmed: fault != "confirmation").Success); Assert.Equal(0, DSPGame.LoadCalls);
        foreach (var path in s.ZeroProgressEvidencePaths)
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Fact]
    public void RawCredentialBoundaryCannotBorrowALegacyTextFingerprintAfterBothReplicasChange()
    {
        using var s = new Scenario(expiredJourney: true); s.SeedZeroProgressReauthorization();
        Assert.True(s.Tickets.TryOpenRemoteCredential(s.Token, out var protectedPair, out _));
        var textFingerprint = protectedPair!.Fingerprint;
        var rawFingerprint = protectedPair.RawFingerprint;
        protectedPair.Dispose(); // Model a change immediately at the consumption boundary.
        foreach (var path in new[] { s.RuntimeTicket, s.HandoffTicket })
            File.WriteAllBytes(path, new byte[] { 0xef, 0xbb, 0xbf }.Concat(File.ReadAllBytes(path)).ToArray());
        Assert.True(s.Tickets.TryCaptureRemoteCredential(s.Token, out var sameTextFingerprint, out _));
        Assert.Equal(textFingerprint, sameTextFingerprint);
        using var boundarySnapshot = new RemoteResumeCredentialLease(textFingerprint, rawFingerprint);
        Assert.False(s.Tickets.TryConsumeRemoteCredential(s.Token, textFingerprint, protectedCredential: boundarySnapshot));
        Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out _, out _)); Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void FreshModeConsumptionCannotBeRecastAsAnOpenedLegacyDefaultAuthorization()
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedZeroProgressReauthorization();
        var consumedToken = s.Token; var accepted = s.Commit(s.Prepare(r)); Assert.True(accepted.Success);
        var sourceWitness = s.Sessions.RemoteEvidence!.SourceFingerprint;
        s.Sessions.Dispose(); s.RearmQuarantine(accepted.Value!.ActionId, minimum: 12501);
        var legacy = WindowRequest(); legacy.PriorConsumedResumeToken = consumedToken;
        legacy.PriorSourceFingerprint = sourceWitness; legacy.PriorActionKnownTerminalFailed = true;
        var result = s.PrepareResult(legacy); Assert.False(result.Success);
        Assert.Contains("commitment_opening_failed", result.Error!.Message); Assert.Equal(1, DSPGame.LoadCalls);
    }

    [Fact]
    public void EnteredNativeLoadFailureRemainsTheOriginalAcceptedUnknownAction()
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedZeroProgressReauthorization(); var p = s.Prepare(r);
        DSPGame.BeforeLoad = () => throw new InvalidOperationException("synthetic loader may already exist");
        var accepted = s.Commit(p); Assert.True(accepted.Success); Assert.True(accepted.Value!.Accepted);
        Assert.Equal(NormalActionStates.OutcomeUnknown, accepted.Value.State);
        Assert.True(s.Result(accepted.Value.ActionId, out var original)); Assert.False(original!.Terminal);
        Assert.False(original.Succeeded); Assert.Equal(NormalActionStates.OutcomeUnknown, original.State);
        Assert.False(s.PrepareResult(r).Success); Assert.False(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void SuccessRequiresTheNormalSaveAndANewCredentialForTheActualAdoptedSession(bool matchingSession)
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedZeroProgressReauthorization();
        var accepted = s.Commit(s.Prepare(r)); Assert.True(accepted.Success);
        s.Sessions.Snapshot.OwnedBySpherewright = true; s.Sessions.Snapshot.LocalPlanetId = 201;
        s.Sessions.Snapshot.SessionId = "synthetic-real-adopted-session"; s.Sessions.Snapshot.GameTick = 12502;
        s.Sessions.Snapshot.OwnedSaveState = OwnedSaveStates.Saved; s.Sessions.Snapshot.LastOwnedSaveGameTick = 12502;
        // Model the original strict-admission lease release, then normal primary
        // save and new credential. This is not a real Unity/Native adoption test.
        s.Sessions.Dispose(); s.WriteSave(Scenario.Identity, 12502);
        s.Tickets.ArmFromHealthySavedOwnedSession(Scenario.Identity,
            matchingSession ? s.Sessions.Snapshot.SessionId : "different-synthetic-session", 201, 12502,
            new OwnedWorldGameplayJournalCheckpoint { Version = 1, JournalId = GameplayJournalIdentity.HashOwnedSaveIdentity(Scenario.Identity),
                TrackingMode = GameplayJournalTrackingModes.AttachedExistingSave, HistoricalCoverageComplete = true,
                TrackingStartedAtGameTick = 12345, MinimumDurableThroughSequence = 1 });
        Assert.True(s.Result(accepted.Value!.ActionId, out var result));
        Assert.True(result!.Terminal); Assert.Equal(matchingSession, result.Succeeded);
    }

    [Fact]
    public void FirstFrameDetailReachesTheOriginalFailedActionAndCannotBeReplacedByLaterSessionState()
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedZeroProgressReauthorization();
        var accepted = s.Commit(s.Prepare(r)); Assert.True(accepted.Success);
        const string reason = "The exact remote LastExit continuation could not enter its strict idle guard. "
            + "[remote_resume_first_frame_universe_unproved] First failed premise: player_universal_position_changed.";
        // The real coordinator consumes the session error; Native/session
        // substitutes do not prove the Host or actual Save/load callbacks.
        s.Sessions.ResumeAdoptionError = reason;
        Assert.True(s.Result(accepted.Value!.ActionId, out var failed));
        Assert.True(failed!.Terminal); Assert.False(failed.Succeeded); Assert.Equal(reason, failed.Message);
        Assert.Equal(NormalActionStates.ActionFailed, failed.State); Assert.Equal(1, DSPGame.LoadCalls);
        s.Sessions.ResumeAdoptionError = "later_cancel";
        s.Sessions.Snapshot.OwnedSaveState = OwnedSaveStates.Saved;
        Assert.True(s.Result(accepted.Value.ActionId, out var unchanged));
        Assert.Equal(reason, unchanged!.Message); Assert.Equal(NormalActionStates.ActionFailed, unchanged.State);
        Assert.False(s.PrepareResult(r).Success); Assert.Equal(1, DSPGame.LoadCalls);
    }

    [Fact]
    public void AReportedTerminalFailureNeverBecomesSuccessWhenACredentialArrivesLater()
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedZeroProgressReauthorization();
        var accepted = s.Commit(s.Prepare(r)); Assert.True(accepted.Success);
        var state = s.Sessions.Snapshot;
        state.OwnedBySpherewright = true; state.LocalPlanetId = 201; state.SessionId = "synthetic-adopted-session";
        state.GameTick = 12502; state.OwnedSaveState = OwnedSaveStates.Saved; state.LastOwnedSaveGameTick = 12502;
        Assert.True(s.Result(accepted.Value!.ActionId, out var failed)); Assert.True(failed!.Terminal); Assert.False(failed.Succeeded);
        var originalMessage = failed.Message;
        // The public result is a copy; neither a caller mutation nor a later
        // credential can rewrite the original known terminal outcome.
        failed.Succeeded = true; failed.Message = "caller mutation";
        s.Sessions.Dispose(); s.WriteSave(Scenario.Identity, 12502);
        s.Tickets.ArmFromHealthySavedOwnedSession(Scenario.Identity, state.SessionId, 201, 12502,
            new OwnedWorldGameplayJournalCheckpoint { Version = 1, JournalId = GameplayJournalIdentity.HashOwnedSaveIdentity(Scenario.Identity),
                TrackingMode = GameplayJournalTrackingModes.AttachedExistingSave, HistoricalCoverageComplete = true,
                TrackingStartedAtGameTick = 12345, MinimumDurableThroughSequence = 1 });
        Assert.True(s.Result(accepted.Value.ActionId, out var same)); Assert.True(same!.Terminal); Assert.False(same.Succeeded);
        Assert.Equal(originalMessage, same.Message);
    }

    private sealed partial class Scenario
    {
        internal IEnumerable<string> ZeroProgressEvidencePaths => ProtectedEvidencePaths.Concat(PriorConsumptionPaths)
            .Concat(new[] { RuntimeTicket, HandoffTicket });
        internal PrepareOwnedWorldResumeRequest SeedZeroProgressReauthorization()
        {
            SeedPriorFailure();
            // Explicitly marked immediate record with no ancestor file. This
            // fresh mode authenticates that pair, never pretends to open it.
            ChangePrior(t => t.RecoveryAuthorization = CopyAuthorization(priorHash: new string('c', 64)));
            WriteSave(GameSave.LastExit, 12501); RearmQuarantine(PriorAuthorization!.ResumeActionId, minimum: 12501);
            var r = WindowRequest(); r.RecoveryMode = OwnedWorldResumeModes.ReauthorizeFailedRemoteLastExit;
            r.PriorConsumedResumeToken = PriorToken; r.PriorActionKnownTerminalFailed = true;
            r.ExpectedRemoteRecoveryContextFingerprint = CurrentOwnerContext();
            return r;
        }
        internal string CurrentOwnerContext()
        {
            var ticket = PluginJson.Deserialize<OwnedWorldResumeTicket>(File.ReadAllText(RuntimeTicket))!;
            using var source = OwnedSaveRecoveryLease.Open(GameSave.SavePath(GameSave.LastExit), Identity);
            using var primary = OwnedSaveRecoveryLease.Open(GameSave.SavePath(Identity), Identity);
            using var cp = OwnedSaveRecoveryLease.Open(GameSave.SavePath(CheckpointSaveName), Identity);
            var journalHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(JournalPath)))).ToLowerInvariant();
            return VoyagerFailedRemoteReauthorizationPolicy.ContextFingerprint(PluginJson.Serialize(Checkpoints.VoyagerJourney),
                PluginJson.Serialize(PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(Path.Combine(Handoff, "flight-checkpoint.json")))),
                cp.Fingerprint, primary.Fingerprint, source.Prefix.GameTick, primary.Prefix.GameTick,
                PluginJson.Serialize(ticket.GameplayJournalCheckpoint), journalHash);
        }
        internal void ReplaceCurrentTicket(Action<OwnedWorldResumeTicket> change)
        {
            var t = PluginJson.Deserialize<OwnedWorldResumeTicket>(File.ReadAllText(RuntimeTicket))!; change(t);
            foreach (var path in new[] { RuntimeTicket, HandoffTicket }) File.WriteAllText(path, PluginJson.Serialize(t));
        }
    }
}
