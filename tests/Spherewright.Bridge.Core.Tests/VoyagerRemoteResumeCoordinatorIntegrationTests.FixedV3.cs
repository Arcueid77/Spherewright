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
    public void FixedV3OpensThreeCommitmentsAndSixFreshLeasesButCannotBecomeFourthGeneration()
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedFixedV3(); var p = s.Prepare(r);
        Assert.True(p.PriorFailedResumeV3ProvenanceUsed); Assert.True(p.PriorFailedResumeV2ProvenanceUsed);
        Assert.True(p.PriorFailedResumeProvenanceUsed); Assert.True(p.PriorActionTerminalFailureAttested);
        foreach (var path in s.AllFixedV3Paths) using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        var accepted = s.Commit(p); Assert.True(accepted.Success, accepted.Error?.Message); Assert.Equal(1, DSPGame.LoadCalls);
        foreach (var path in s.AllFixedV3Paths)
            Assert.ThrowsAny<IOException>(() => { using var _ = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
        Assert.Equal(s.FixedV3Hash, s.Sessions.RecoveryAuthorization!.PriorConsumedResumeTokenHash);
        s.Sessions.RemoteEvidence!.Dispose();
        Assert.False(s.Tickets.TryOpenPriorFailedResumeProof(s.Token, accepted.Value!.ActionId, out _, out _, true, true));
        foreach (var path in s.AllFixedV3Paths) using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }
    [Theory]
    [InlineData("newest")] [InlineData("middle")] [InlineData("oldest")] [InlineData("missing_middle")]
    [InlineData("partial_oldest")] [InlineData("false_ack")] [InlineData("unknown_ack")] [InlineData("wrong_q")]
    [InlineData("newer_primary")] [InlineData("advanced_source")] [InlineData("oldest_marked")]
    [InlineData("reverse_time")] [InlineData("version")]
    public void FixedV3RejectsChangedWitnessProgressOrUnsupportedPredecessor(string fault)
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedFixedV3();
        switch (fault)
        {
            case "newest": r.PriorSourceFingerprint = CanonicalStateHash.Combine("wrong-newest"); break;
            case "middle": r.PriorIntermediateSourceFingerprint = CanonicalStateHash.Combine("wrong-middle"); break;
            case "oldest": r.PriorLegacySourceFingerprint = CanonicalStateHash.Combine("wrong-oldest"); break;
            case "missing_middle": r.PriorIntermediateSourceFingerprint = ""; break;
            case "partial_oldest": r.PriorLegacySourceFingerprint = ""; break;
            case "false_ack": r.PriorActionKnownTerminalFailed = false; break;
            case "unknown_ack": r.PriorActionKnownTerminalFailed = null; break;
            case "wrong_q": s.RearmQuarantine(Guid.NewGuid().ToString()); break;
            case "newer_primary": s.WriteSave(Scenario.Identity, 12511); break;
            case "advanced_source": s.WriteSave(GameSave.LastExit, 12511); s.RearmQuarantine(s.FixedV3Authorization!.ResumeActionId, minimum: 12511); break;
            case "oldest_marked": s.ChangePrior(t => t.RecoveryAuthorization = s.CopyAuthorization(priorHash: new string('a', 64))); break;
            case "reverse_time": s.ChangeFixedV2(t => t.ConsumedAtUtc = s.FixedV3Authorization!.ConfirmedAtUtc.AddSeconds(1)); break;
            case "version": s.ChangeFixedV3(t => t.GameVersion = "unsupported"); break;
        }
        Assert.False(s.PrepareResult(r).Success); Assert.Equal(0, DSPGame.LoadCalls);
        foreach (var path in s.AllFixedV3Paths) using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void EveryRawReplicaIsBoundAndFailureReleasesAllLeases(int index)
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedFixedV3();
        File.AppendAllText(s.AllFixedV3Paths[index], " ");
        Assert.False(s.PrepareResult(r).Success); Assert.Equal(0, DSPGame.LoadCalls);
        foreach (var path in s.AllFixedV3Paths) using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }
    [Fact]
    public void CommitReopensSixRecordsAndCannotBorrowTheEarlierDigest()
    {
        using var s = new Scenario(expiredJourney: true); var r = s.SeedFixedV3(); var p = s.Prepare(r);
        s.ChangeFixedV3(t => t.ConsumedAtUtc = t.ConsumedAtUtc.AddTicks(1));
        Assert.False(s.Commit(p).Success); Assert.Equal(0, DSPGame.LoadCalls);
        foreach (var path in s.AllFixedV3Paths) using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }
    private sealed partial class Scenario
    {
        internal string FixedV3Token { get; private set; } = "";
        internal string FixedV3Hash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(FixedV3Token))).ToLowerInvariant();
        internal string[] FixedV3Paths => new[] { Path.Combine(Runtime, $"owned-world-resume-consumed-{FixedV3Hash}.json"),
            Path.Combine(Handoff, $"owned-world-resume-consumed-{FixedV3Hash}.json") };
        internal string[] AllFixedV3Paths => FixedV3Paths.Concat(FixedV2Paths).Concat(PriorConsumptionPaths).ToArray();
        internal VoyagerRecoveryAuthorization? FixedV3Authorization { get; private set; }
        internal PrepareOwnedWorldResumeRequest SeedFixedV3()
        {
            var previous = SeedFixedV2(); var ticket = PluginJson.Deserialize<OwnedWorldResumeTicket>(File.ReadAllText(RuntimeTicket))!;
            var proposal = new VoyagerRecoveryWindowProposal(DateTimeOffset.UtcNow.AddMinutes(25), VoyagerRecoveryWindowProposal.TaskScope, 9000, 240);
            using var source = OwnedSaveRecoveryLease.Open(GameSave.SavePath(GameSave.LastExit), Identity);
            using var primary = OwnedSaveRecoveryLease.Open(GameSave.SavePath(Identity), Identity);
            Assert.True(Tickets.TryOpenPriorFailedResumeProof(FixedV2Token, ticket.QuarantineActionId, out var prior, out _, true));
            Assert.True(Checkpoints.TryOpenRemoteResumeEvidence(ticket, source, primary, out var evidence, out _, proposal,
                prior, previous.PriorSourceFingerprint, true, previous.PriorLegacySourceFingerprint));
            using (evidence)
            {
                FixedV3Authorization = new(Guid.NewGuid().ToString(), evidence!.Fingerprint,
                    CanonicalStateHash.Combine("synthetic-fixed-v3-confirmation"), DateTimeOffset.UtcNow, proposal, FixedV2Hash);
                FixedV3Token = Token; Assert.True(Tickets.TryCaptureRemoteCredential(Token, out var credential, out _));
                Assert.True(Tickets.TryConsumeRemoteCredential(Token, credential, FixedV3Authorization));
            }
            RearmQuarantine(FixedV3Authorization.ResumeActionId);
            var r = WindowRequest(); r.PriorConsumedResumeToken = FixedV3Token; r.PriorSourceFingerprint = source.Fingerprint;
            r.PriorIntermediateSourceFingerprint = previous.PriorSourceFingerprint; r.PriorLegacySourceFingerprint = previous.PriorLegacySourceFingerprint;
            r.PriorActionKnownTerminalFailed = true; return r;
        }
        internal void ChangeFixedV3(Action<OwnedWorldResumeConsumptionTombstone> change)
        {
            var t = PluginJson.Deserialize<OwnedWorldResumeConsumptionTombstone>(File.ReadAllText(FixedV3Paths[0]))!;
            change(t); foreach (var path in FixedV3Paths) File.WriteAllText(path, PluginJson.Serialize(t));
        }
    }
}
