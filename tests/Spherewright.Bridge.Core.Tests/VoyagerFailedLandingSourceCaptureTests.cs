using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed partial class VoyagerRemoteResumeCoordinatorIntegrationTests
{
    private static PrepareOwnedWorldResumeRequest FailedLandingRequest() => new()
    {
        RecoveryMode = VoyagerFailedLandingRecoveryPolicy.Mode, PriorActionKnownTerminalFailed = true,
        MinimumRecoveryGameTick = 12510, ExpectedRecoveryGameTick = 12510,
        RecoveryDeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(55),
        RecoveryScope = VoyagerRecoveryWindowProposal.TaskScope,
        MaximumChargeGameTicks = 3600, MaximumChargeWallSeconds = 120,
    };

    [Fact]
    public void FailedLandingCaptureLeasesCurrentFullLastExitWithoutLoadingOrRewritingOldFailure()
    {
        using var s = new Scenario(currentNative: true, failedLanding: true);
        var original = s.ProtectedEvidencePaths.ToDictionary(p => p, File.ReadAllBytes);
        Assert.True(s.Checkpoints.TryOpenFailedLandingSource(s.Tickets, s.Token, FailedLandingRequest(), out var proof, out var failure), failure?.Code);
        using (proof)
        {
            Assert.NotNull(proof); Assert.Null(failure);
            Assert.Equal(12510, proof.SavedAccountingAnchorGameTick);
            Assert.Equal(165, proof.ConservedSpentGameTicks);
            Assert.Equal("recovery_required", proof.Journey.Phase);
            Assert.Null(proof.Journey.LastLandedGameTick);
            Assert.Equal(104, proof.Ticket.ExpectedPlanetId); // The stale mother ticket is not repurposed.
            Assert.Equal(201, proof.Journey.RemotePlanetId);
            Assert.Equal(0, proof.Proposal.AdditionalVoyagerGameTicks);
            Assert.Equal(0, DSPGame.LoadCalls);
            using var admission = VoyagerRemoteResumeEvidence.CreateFailedLandingRecovery(proof);
            Assert.True(admission.FailedLandingRecovery);
            Assert.Equal(12510, admission.FailedLandingAccountingAnchorGameTick);
            Assert.Null(admission.Journey.LastLandedGameTick);
            Assert.True(s.Checkpoints.RemoteResumeEvidenceStillMatches(admission));
            // Actual file-sharing enforcement, not an in-memory availability flag.
            foreach (var path in s.ProtectedEvidencePaths)
                Assert.Throws<IOException>(() => { using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); });
            foreach (var pair in original) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        }
        foreach (var path in s.ProtectedEvidencePaths)
        { using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); }
        foreach (var pair in original) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
    }

    [Theory]
    [InlineData("older_exit")][InlineData("newer_exit")][InlineData("newer_primary")]
    [InlineData("changed_journey")][InlineData("missing_checkpoint")][InlineData("bad_journal")]
    [InlineData("invalid_request")][InlineData("wrong_token")]
    [InlineData("foreign_identity")][InlineData("sandbox")][InlineData("wrong_version")]
    public void FailedLandingCaptureRejectsUnboundSourcesAndReleasesEveryPartiallyOpenedLease(string fault)
    {
        using var s = new Scenario(currentNative: true, failedLanding: true);
        var r = FailedLandingRequest(); var token = s.Token;
        switch (fault)
        {
            case "older_exit": s.WriteSave(GameSave.LastExit, 12509); break;
            case "newer_exit": s.WriteSave(GameSave.LastExit, 12511); break;
            case "newer_primary": s.WriteSave(Scenario.Identity, 12511); break;
            case "changed_journey": File.AppendAllText(Path.Combine(s.Handoff, "voyager-journey.json"), "corrupt"); break;
            case "missing_checkpoint": File.Delete(s.ProtectedEvidencePaths.ElementAt(2)); break;
            case "bad_journal": File.WriteAllText(s.JournalPath, "corrupt"); break;
            case "invalid_request": r.AcceptUnstoredProgressLoss = true; break;
            case "wrong_token": token = "untrusted"; break;
            case "foreign_identity":
            case "sandbox":
            case "wrong_version":
                using (var replacement = fault == "wrong_version" ? OwnedSavePrefixReaderTests.Fixture(identity: Scenario.Identity)
                    : OwnedSavePrefixReaderTests.Fixture(identity: fault == "foreign_identity" ? "foreign-world" : Scenario.Identity,
                        sandbox: fault == "sandbox", versionPatch: 35, versionBuild: 29151, patch: 23, gameDescVersion: 10))
                {
                    replacement.Position = 36;
                    using (var writer = new BinaryWriter(replacement, System.Text.Encoding.UTF8, true)) writer.Write(12510L);
                    File.WriteAllBytes(GameSave.SavePath(GameSave.LastExit), replacement.ToArray());
                }
                break;
        }
        var original = s.ProtectedEvidencePaths.Where(File.Exists).ToDictionary(p => p, File.ReadAllBytes);
        Assert.False(s.Checkpoints.TryOpenFailedLandingSource(s.Tickets, token, r, out var proof, out var failure));
        Assert.Null(proof); Assert.NotNull(failure); Assert.Equal(0, DSPGame.LoadCalls);
        foreach (var pair in original)
        {
            Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            using var writer = new FileStream(pair.Key, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        }
    }

    [Fact]
    public void OldSuccessfulLandingCannotBeRecertifiedAsFailedOutboundSource()
    {
        using var s = new Scenario(currentNative: true);
        Assert.False(s.Checkpoints.TryOpenFailedLandingSource(s.Tickets, s.Token, FailedLandingRequest(), out var proof, out var failure));
        Assert.Null(proof); Assert.Equal("failed_landing_original_failure_context_unproved", failure?.Code);
        Assert.Equal(0, DSPGame.LoadCalls);
    }
}
