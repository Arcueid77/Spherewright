using System.Security.Cryptography;
using System.Text;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    [Fact]
    public void FixedV2OpensBothCommitmentsRevalidatesAndTransfersAllFourLeases()
    {
        using var s = new Scenario(expiredJourney: true); var request = s.SeedFixedV2();
        var prepared = s.Prepare(request);
        Assert.True(prepared.PriorFailedResumeProvenanceUsed); Assert.True(prepared.PriorFailedResumeV2ProvenanceUsed);
        foreach (var path in s.FixedV2Paths.Concat(s.PriorConsumptionPaths))
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        var result = s.Commit(prepared);
        Assert.True(result.Success, result.Error?.Message); Assert.Equal(1, DSPGame.LoadCalls);
        Assert.NotEqual(s.FixedV2Authorization!.EvidenceFingerprint, s.Sessions.RemoteEvidence!.Fingerprint);
        Assert.Equal(s.FixedV2Hash, s.Sessions.RecoveryAuthorization!.PriorConsumedResumeTokenHash);
        foreach (var path in s.FixedV2Paths.Concat(s.PriorConsumptionPaths))
            Assert.ThrowsAny<IOException>(() => { using var _ = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
        // The successor's immediate ancestor is already marked, so this fixed
        // two-record-format opening cannot become a generic ancestry walker.
        s.Sessions.RemoteEvidence.Dispose();
        Assert.False(s.Tickets.TryOpenPriorFailedResumeProof(s.Token, result.Value!.ActionId, out _, out _, true));
        foreach (var path in s.FixedV2Paths.Concat(s.PriorConsumptionPaths))
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Theory]
    [InlineData("legacy_witness")] [InlineData("source_witness")] [InlineData("missing_fourth")]
    [InlineData("false_ack")] [InlineData("unknown_ack")] [InlineData("ancestor_marker")]
    [InlineData("ancestor_bytes")] [InlineData("immediate_bytes")] [InlineData("immediate_auth")]
    [InlineData("ancestor_digest")] [InlineData("immediate_digest")] [InlineData("reverse_time")]
    [InlineData("newer_primary")] [InlineData("advanced_source")]
    public void FixedV2CannotOpenChangedOrIncompleteEvidence(string fault)
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedFixedV2();
        switch (fault)
        {
            case "legacy_witness": r.PriorLegacySourceFingerprint = CanonicalStateHash.Combine("other-legacy"); break;
            case "source_witness": r.PriorSourceFingerprint = CanonicalStateHash.Combine("other-source"); break;
            case "missing_fourth": r.PriorLegacySourceFingerprint = ""; break;
            case "false_ack": r.PriorActionKnownTerminalFailed = false; break;
            case "unknown_ack": r.PriorActionKnownTerminalFailed = null; break;
            case "ancestor_marker": s.ChangePrior(t => t.RecoveryAuthorization = s.CopyAuthorization(priorHash: new string('a', 64))); break;
            case "ancestor_bytes": File.AppendAllText(s.PriorConsumptionPaths[1], " "); break;
            case "immediate_bytes": File.AppendAllText(s.FixedV2Paths[1], " "); break;
            case "immediate_auth": s.ChangeFixedV2(t => t.RecoveryAuthorization = null); break;
            case "ancestor_digest": s.ChangePrior(t => t.RecoveryAuthorization = s.CopyAuthorization(evidence: CanonicalStateHash.Combine("bad"))); break;
            case "immediate_digest": s.ChangeFixedV2(t => { var a = t.RecoveryAuthorization!;
                t.RecoveryAuthorization = new(a.ResumeActionId, CanonicalStateHash.Combine("bad"), a.ConfirmationDigest,
                    a.ConfirmedAtUtc, a.Proposal, a.PriorConsumedResumeTokenHash); }); break;
            case "reverse_time": s.ChangePrior(t => t.ConsumedAtUtc = s.FixedV2Authorization!.ConfirmedAtUtc.AddSeconds(1)); break;
            case "newer_primary": s.WriteSave(Scenario.Identity, 12511); break;
            case "advanced_source": s.WriteSave(GameSave.LastExit, 12511); s.RearmQuarantine(s.FixedV2Authorization!.ResumeActionId, minimum: 12511); break;
        }
        Assert.False(s.PrepareResult(r).Success); Assert.Equal(0, DSPGame.LoadCalls);
        foreach (var path in s.FixedV2Paths.Concat(s.PriorConsumptionPaths))
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Fact]
    public void FixedV2CommitDoesNotTrustPreparedFilesOrAnotherWitnessDigest()
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedFixedV2(); var p = s.Prepare(r);
        s.ChangeFixedV2(t => t.ConsumedAtUtc = t.ConsumedAtUtc.AddTicks(1));
        Assert.False(s.Commit(p).Success); Assert.Equal(0, DSPGame.LoadCalls);
        foreach (var path in s.FixedV2Paths.Concat(s.PriorConsumptionPaths))
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    private sealed partial class Scenario
    {
        internal string FixedV2Token { get; private set; } = "";
        internal string FixedV2Hash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(FixedV2Token))).ToLowerInvariant();
        internal string[] FixedV2Paths => new[] { Path.Combine(Runtime, $"owned-world-resume-consumed-{FixedV2Hash}.json"),
            Path.Combine(Handoff, $"owned-world-resume-consumed-{FixedV2Hash}.json") };
        internal VoyagerRecoveryAuthorization? FixedV2Authorization { get; private set; }
        internal PrepareOwnedWorldResumeRequest SeedFixedV2()
        {
            var legacyRequest = SeedPriorFailure();
            var ticket = PluginJson.Deserialize<OwnedWorldResumeTicket>(File.ReadAllText(RuntimeTicket))!;
            var proposal = new VoyagerRecoveryWindowProposal(DateTimeOffset.UtcNow.AddMinutes(25), VoyagerRecoveryWindowProposal.TaskScope, 9000, 240);
            using var source = OwnedSaveRecoveryLease.Open(GameSave.SavePath(GameSave.LastExit), Identity);
            using var primary = OwnedSaveRecoveryLease.Open(GameSave.SavePath(Identity), Identity);
            Assert.True(Tickets.TryOpenPriorFailedResumeProof(PriorToken, ticket.QuarantineActionId, out var prior, out _));
            Assert.True(Checkpoints.TryOpenRemoteResumeEvidence(ticket, source, primary, out var evidence, out _, proposal,
                prior, legacyRequest.PriorSourceFingerprint, true));
            using (evidence)
            {
                FixedV2Authorization = new(Guid.NewGuid().ToString(), evidence!.Fingerprint,
                    CanonicalStateHash.Combine("synthetic-fixed-v2-confirmation"), DateTimeOffset.UtcNow, proposal, PriorTokenHash);
                FixedV2Token = Token;
                Assert.True(Tickets.TryCaptureRemoteCredential(Token, out var credential, out _));
                Assert.True(Tickets.TryConsumeRemoteCredential(Token, credential, FixedV2Authorization));
            }
            RearmQuarantine(FixedV2Authorization.ResumeActionId);
            var r = WindowRequest(); r.PriorConsumedResumeToken = FixedV2Token;
            r.PriorSourceFingerprint = source.Fingerprint; r.PriorLegacySourceFingerprint = legacyRequest.PriorSourceFingerprint;
            r.PriorActionKnownTerminalFailed = true; return r;
        }
        internal void ChangeFixedV2(Action<OwnedWorldResumeConsumptionTombstone> change)
        {
            var t = PluginJson.Deserialize<OwnedWorldResumeConsumptionTombstone>(File.ReadAllText(FixedV2Paths[0]))!;
            change(t); foreach (var path in FixedV2Paths) File.WriteAllText(path, PluginJson.Serialize(t));
        }
    }
}
