using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Errors;
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
    public void OnePriorFailureOpensCurrentRouteWithoutExecutingItsExpiredProposalAndPersistsHop()
    {
        using var s = new Scenario(expiredJourney: true);
        var request = s.SeedPriorFailure();
        Assert.True(s.PriorAuthorization!.Proposal.DeadlineUtc < DateTimeOffset.UtcNow);
        var originalJourney = File.ReadAllBytes(Path.Combine(s.Handoff, "voyager-journey.json"));
        var p = s.Prepare(request);
        Assert.True(p.PriorFailedResumeProvenanceUsed); Assert.True(p.PriorActionTerminalFailureAttested);
        Assert.Contains("caller attests", p.ConfirmationPrompt);
        Assert.Contains("not a durable Native action outcome", p.ConfirmationPrompt);
        Assert.Equal(request.RecoveryDeadlineUtc, p.EffectiveRecoveryDeadlineUtc);
        Assert.NotEqual(s.PriorAuthorization.Proposal.DeadlineUtc, p.EffectiveRecoveryDeadlineUtc);
        foreach (var path in s.PriorConsumptionPaths)
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        var result = s.Commit(p);
        Assert.True(result.Success, result.Error?.Message); Assert.Equal(1, DSPGame.LoadCalls);
        var evidence = s.Sessions.RemoteEvidence!;
        Assert.NotEqual(s.PriorAuthorization.EvidenceFingerprint, evidence.Fingerprint);
        Assert.Equal(s.PriorTokenHash, evidence.PriorConsumedResumeTokenHash);
        Assert.Equal(s.PriorTokenHash, s.Sessions.RecoveryAuthorization!.PriorConsumedResumeTokenHash);
        Assert.Equal(request.RecoveryDeadlineUtc, s.Sessions.RecoveryAuthorization.Proposal.DeadlineUtc);
        var persisted = PluginJson.Deserialize<OwnedWorldResumeConsumptionTombstone>(File.ReadAllText(s.CurrentConsumptionPath))!;
        Assert.Equal(s.PriorTokenHash, persisted.RecoveryAuthorization!.PriorConsumedResumeTokenHash);
        foreach (var path in s.PriorConsumptionPaths)
            Assert.ThrowsAny<IOException>(() => { using var _ = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
        Assert.Equal(originalJourney, File.ReadAllBytes(Path.Combine(s.Handoff, "voyager-journey.json")));
    }

    [Theory]
    [InlineData("token_missing")] [InlineData("source_missing")] [InlineData("ack_missing")]
    [InlineData("ack_false")] [InlineData("mode")] [InlineData("source_format")]
    public void PartialOrUnobservedPriorFailureNeverPrepares(string fault)
    {
        using var s = new Scenario(expiredJourney: true); var request = s.SeedPriorFailure();
        switch (fault)
        {
            case "token_missing": request.PriorConsumedResumeToken = ""; break;
            case "source_missing": request.PriorSourceFingerprint = ""; break;
            case "ack_missing": request.PriorActionKnownTerminalFailed = null; break;
            case "ack_false": request.PriorActionKnownTerminalFailed = false; break;
            case "mode": request.RecoveryMode = OwnedWorldResumeModes.VerifiedNewerLastExit; break;
            case "source_format": request.PriorSourceFingerprint = "not-a-path-or-fingerprint"; break;
        }
        Assert.False(s.PrepareResult(request).Success); Assert.Equal(0, DSPGame.LoadCalls);
        Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
    }

    [Theory]
    [InlineData("token")] [InlineData("source")] [InlineData("quarantine")]
    [InlineData("version")] [InlineData("replicas")] [InlineData("auth_missing")]
    [InlineData("already_hopped")] [InlineData("opening_digest")] [InlineData("oversized")]
    [InlineData("missing_replica")] [InlineData("bom_replica")] [InlineData("proposal_missing")]
    [InlineData("digest_missing")] [InlineData("token_hash")]
    public void PriorConsumptionMustBeExactCompleteSameVersionAndNotAHop(string fault)
    {
        using var s = new Scenario(expiredJourney: true); var request = s.SeedPriorFailure();
        switch (fault)
        {
            case "token": request.PriorConsumedResumeToken = "synthetic-unconsumed-token"; break;
            case "source": request.PriorSourceFingerprint = CanonicalStateHash.Combine("different-source"); break;
            case "quarantine": s.RearmQuarantine(Guid.NewGuid().ToString()); break;
            case "version": s.ChangePrior(t => t.GameVersion = "other-native-version"); break;
            case "replicas": File.AppendAllText(s.PriorConsumptionPaths[1], " "); break;
            case "missing_replica": File.Delete(s.PriorConsumptionPaths[1]); break;
            case "bom_replica":
                var bytes = File.ReadAllBytes(s.PriorConsumptionPaths[1]);
                File.WriteAllBytes(s.PriorConsumptionPaths[1], new byte[] { 0xef, 0xbb, 0xbf }.Concat(bytes).ToArray()); break;
            case "auth_missing": s.ChangePrior(t => t.RecoveryAuthorization = null); break;
            case "proposal_missing": s.ChangePrior(t => { var a = s.PriorAuthorization!;
                t.RecoveryAuthorization = new(a.ResumeActionId, a.EvidenceFingerprint, a.ConfirmationDigest, a.ConfirmedAtUtc, null!); }); break;
            case "digest_missing": s.ChangePrior(t => t.RecoveryAuthorization = s.CopyAuthorization(confirmation: "")); break;
            case "token_hash": s.ChangePrior(t => t.ResumeTokenHash = new string('0', 64)); break;
            case "already_hopped": s.ChangePrior(t => t.RecoveryAuthorization = s.CopyAuthorization(priorHash: "already-used")); break;
            case "opening_digest": s.ChangePrior(t => t.RecoveryAuthorization = s.CopyAuthorization(evidence: CanonicalStateHash.Combine("different-opening"))); break;
            case "oversized": foreach (var path in s.PriorConsumptionPaths) File.WriteAllText(path, new string(' ', 65537)); break;
        }
        Assert.False(s.PrepareResult(request).Success); Assert.Equal(0, DSPGame.LoadCalls);
        Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
    }

    [Theory]
    [InlineData("journey")] [InlineData("checkpoint")] [InlineData("journal")]
    [InlineData("primary")] [InlineData("source_tick")] [InlineData("source_minimum")]
    [InlineData("old_proposal")]
    public void OpeningCannotBorrowAChangedComponentOrNewerProgress(string fault)
    {
        using var s = new Scenario(expiredJourney: true); var request = s.SeedPriorFailure();
        switch (fault)
        {
            case "journey":
                var journey = s.Checkpoints.VoyagerJourney!; journey.ReturnEnergyFloorJoules += 1;
                Assert.True(s.Checkpoints.TryPersistVoyagerJourney(journey, out _)); break;
            case "checkpoint":
                var cpPath = Path.Combine(s.Handoff, "flight-checkpoint.json");
                var cp = PluginJson.Deserialize<FlightCheckpointTicket>(File.ReadAllText(cpPath))!;
                cp.PlayerStateHash = "changed"; File.WriteAllText(cpPath, PluginJson.Serialize(cp)); break;
            case "journal": s.RearmQuarantine(s.PriorAuthorization!.ResumeActionId, changedJournal: true); break;
            case "primary": s.WriteSave(Scenario.Identity, 12511); break;
            case "source_tick": s.WriteSave(GameSave.LastExit, 12511); s.RearmQuarantine(s.PriorAuthorization!.ResumeActionId, minimum: 12511); break;
            case "source_minimum": s.RearmQuarantine(s.PriorAuthorization!.ResumeActionId, minimum: 12509); break;
            case "old_proposal":
                var a = s.PriorAuthorization!; var p = a.Proposal;
                s.ChangePrior(t => t.RecoveryAuthorization = s.CopyAuthorization(proposal: new VoyagerRecoveryWindowProposal(
                    p.DeadlineUtc.AddSeconds(1), p.Scope, p.MaximumChargeGameTicks, p.MaximumChargeWallSeconds))); break;
        }
        Assert.False(s.PrepareResult(request).Success); Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void FreshCommitReopensPriorProofAndCannotUseItsPrepareSnapshot()
    {
        using var s = new Scenario(expiredJourney: true); var request = s.SeedPriorFailure(); var p = s.Prepare(request);
        s.ChangePrior(t => t.RecoveryAuthorization = s.CopyAuthorization(confirmation: CanonicalStateHash.Combine("other-confirmation")));
        var result = s.Commit(p);
        Assert.False(result.Success); Assert.Equal(BridgeErrorCodes.StaleState, result.Error!.Code);
        Assert.Equal(0, DSPGame.LoadCalls); Assert.True(s.Tickets.TryGetActiveTicket(s.Token, out _, out _));
        foreach (var path in s.PriorConsumptionPaths)
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Fact]
    public void RemovingPriorWitnessCannotReuseThePreparedDigestOrOriginalProvenance()
    {
        using var s = new Scenario(expiredJourney: true); var request = s.SeedPriorFailure(); var p = s.Prepare(request);
        Assert.False(s.PrepareResult(WindowRequest()).Success);
        var result = s.Commit(new CommitOwnedWorldResumeRequest { PlanToken = p.PlanToken,
            IdempotencyKey = Guid.NewGuid().ToString(), UserConfirmedInConversation = true,
            ConfirmationDigest = CanonicalStateHash.Combine("different-disclosure") });
        Assert.False(result.Success); Assert.Equal(0, DSPGame.LoadCalls);
    }

    [Fact]
    public void LegacyAuthorizationBytesAndV1EvidenceRemainUnchanged()
    {
        var confirmed = DateTimeOffset.Parse("2025-01-01T00:00:00+00:00");
        var p = new VoyagerRecoveryWindowProposal(confirmed.AddHours(1), VoyagerRecoveryWindowProposal.TaskScope, 120, 30);
        var a = new VoyagerRecoveryAuthorization("synthetic-action", "synthetic-evidence", "synthetic-confirmation", confirmed, p);
        var originalShape = new { a.ResumeActionId, a.EvidenceFingerprint, a.ConfirmationDigest, a.ConfirmedAtUtc, a.Proposal };
        Assert.Equal(PluginJson.Serialize(originalShape), PluginJson.Serialize(a));
        Assert.DoesNotContain("priorConsumedResumeTokenHash", PluginJson.Serialize(a));
        using var s = new Scenario(); var request = WindowRequest();
        var prepared = s.Prepare(request); Assert.False(prepared.PriorFailedResumeProvenanceUsed);
        Assert.False(prepared.PriorActionTerminalFailureAttested);
        Assert.True(s.Commit(prepared).Success);
        var e = s.Sessions.RemoteEvidence!;
        Assert.Equal(CanonicalStateHash.Combine("voyager-remote-last-exit-v1", e.JourneyJson, e.CheckpointJson,
            e.SourceFingerprint, e.PrimaryFingerprint, e.SourceGameTick, e.PrimaryGameTick, e.JournalJson,
            PluginJson.Serialize(e.RecoveryProposal)), e.Fingerprint);
    }

    [Fact]
    public void ExactJsonNumberLexemesAreCommittedRatherThanCrossRuntimeNormalized()
    {
        const string oldRuntime = "{\"syntheticDouble\":0.10000000000000001}";
        const string desktopRuntime = "{\"syntheticDouble\":0.1}";
        Assert.Equal((double)JObject.Parse(oldRuntime)["syntheticDouble"]!, (double)JObject.Parse(desktopRuntime)["syntheticDouble"]!);
        Assert.NotEqual(VoyagerRemoteResumeEvidence.LegacyFingerprint(oldRuntime, "{}", "s", "p", 1, 0, "{}", null),
            VoyagerRemoteResumeEvidence.LegacyFingerprint(desktopRuntime, "{}", "s", "p", 1, 0, "{}", null));
    }

    private sealed partial class Scenario
    {
        internal string PriorToken { get; private set; } = "";
        internal string PriorTokenHash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(PriorToken))).ToLowerInvariant();
        internal string[] PriorConsumptionPaths => new[] { Path.Combine(Runtime, $"owned-world-resume-consumed-{PriorTokenHash}.json"),
            Path.Combine(Handoff, $"owned-world-resume-consumed-{PriorTokenHash}.json") };
        internal string CurrentConsumptionPath => Path.Combine(Runtime, "owned-world-resume-consumed-"
            + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Token))).ToLowerInvariant() + ".json");
        internal VoyagerRecoveryAuthorization? PriorAuthorization { get; private set; }
        internal PrepareOwnedWorldResumeRequest SeedPriorFailure()
        {
            var ticket = PluginJson.Deserialize<OwnedWorldResumeTicket>(File.ReadAllText(RuntimeTicket))!;
            var proposal = new VoyagerRecoveryWindowProposal(DateTimeOffset.UtcNow.AddMinutes(-15),
                VoyagerRecoveryWindowProposal.TaskScope, 9000, 240);
            using var source = OwnedSaveRecoveryLease.Open(GameSave.SavePath(GameSave.LastExit), Identity);
            using var primary = OwnedSaveRecoveryLease.Open(GameSave.SavePath(Identity), Identity);
            var opening = VoyagerRemoteResumeEvidence.LegacyFingerprint(PluginJson.Serialize(Checkpoints.VoyagerJourney),
                File.ReadAllText(Path.Combine(Handoff, "flight-checkpoint.json")), source.Fingerprint, primary.Fingerprint,
                source.Prefix.GameTick, primary.Prefix.GameTick, PluginJson.Serialize(ticket.GameplayJournalCheckpoint), proposal);
            PriorAuthorization = new VoyagerRecoveryAuthorization(Guid.NewGuid().ToString(), opening,
                CanonicalStateHash.Combine("synthetic-prior-confirmation"), DateTimeOffset.UtcNow.AddMinutes(-30), proposal);
            PriorToken = Token;
            Assert.True(Tickets.TryCaptureRemoteCredential(Token, out var credential, out _));
            Assert.True(Tickets.TryConsumeRemoteCredential(Token, credential, PriorAuthorization));
            RearmQuarantine(PriorAuthorization.ResumeActionId);
            var request = WindowRequest(); request.PriorConsumedResumeToken = PriorToken;
            request.PriorSourceFingerprint = source.Fingerprint; request.PriorActionKnownTerminalFailed = true;
            return request;
        }
        internal void RearmQuarantine(string action, bool changedJournal = false, long minimum = 12510)
        {
            var ticketPath = File.Exists(RuntimeTicket) ? RuntimeTicket : null;
            var checkpoint = ticketPath is not null
                ? PluginJson.Deserialize<OwnedWorldResumeTicket>(File.ReadAllText(ticketPath))!.GameplayJournalCheckpoint!
                : new OwnedWorldGameplayJournalCheckpoint { Version = 1, JournalId = GameplayJournalIdentity.HashOwnedSaveIdentity(Identity),
                    TrackingMode = GameplayJournalTrackingModes.AttachedExistingSave, TrackingStartedAtGameTick = 12345,
                    HistoricalCoverageComplete = true, MinimumDurableThroughSequence = 1 };
            if (changedJournal) checkpoint.MinimumDurableThroughSequence = 0;
            Tickets.ArmFromQuarantinedOwnedSession(Identity, "synthetic-failed-resume-session", 201, minimum, action, checkpoint);
            Token = Tickets.CurrentResumeToken!;
        }
        internal VoyagerRecoveryAuthorization CopyAuthorization(string? priorHash = null, string? evidence = null,
            string? confirmation = null, VoyagerRecoveryWindowProposal? proposal = null)
        {
            var a = PriorAuthorization!;
            return new(a.ResumeActionId, evidence ?? a.EvidenceFingerprint, confirmation ?? a.ConfirmationDigest,
                a.ConfirmedAtUtc, proposal ?? a.Proposal, priorHash);
        }
        internal void ChangePrior(Action<OwnedWorldResumeConsumptionTombstone> change)
        {
            var t = PluginJson.Deserialize<OwnedWorldResumeConsumptionTombstone>(File.ReadAllText(PriorConsumptionPaths[0]))!;
            change(t); foreach (var path in PriorConsumptionPaths) File.WriteAllText(path, PluginJson.Serialize(t));
        }
    }
}
