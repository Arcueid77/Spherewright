using System.Text;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class OwnedWorldAutosaveRecoveryPolicyTests
{
    private const string Identity = "synthetic-protected-world-identity";
    private const string Version = "0.10.35.29088";
    private const string ConfirmationDigest = "sha256:exact-autosave0-plan";
    private const string ExpiredMode = OwnedWorldResumeModes.ReauthorizeExpiredAutosave0;
    private const string PreapprovedExpiredMode = OwnedWorldResumeModes.ReauthorizePreapprovedExpiredAutosave0;
    private const string ActiveMode = OwnedWorldResumeModes.ReauthorizeFixedAutosave0;
    private static readonly DateTimeOffset Issued = new(2026, 9, 16, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PreapprovedExpiredModeHasDistinctEvidenceAndNeverChangesTheOriginalExpiredPrompt()
    {
        Assert.Equal(5, OwnedWorldAutosaveRecoveryPolicy.EvidenceVersionFor(PreapprovedExpiredMode));
        Assert.Equal(3, OwnedWorldAutosaveRecoveryPolicy.EvidenceVersionFor(ExpiredMode));
        Assert.Contains("may precede the fresh plan", OwnedWorldAutosaveRecoveryPolicy.ConfirmationPromptFor(PreapprovedExpiredMode));
        Assert.Contains("Confirm after reading this prepared plan", OwnedWorldAutosaveRecoveryPolicy.ConfirmationPromptFor(ExpiredMode));
        var plan = MatchingPlan(PreapprovedExpiredMode);
        plan.RecoveryMode = ExpiredMode;
        Assert.False(OwnedWorldAutosaveRecoveryPolicy.HasMatchingEcho(Request(PreapprovedExpiredMode), plan));
    }

    [Theory]
    [InlineData(ExpiredMode)]
    [InlineData(PreapprovedExpiredMode)]
    [InlineData(ActiveMode)]
    public void ExactNewerSameVersionAutoSave0CandidatePasses(string mode)
    {
        var evidence = ReadEvidence();

        Assert.Null(Validate(Request(mode), evidence));
    }

    [Theory]
    [InlineData(ExpiredMode, "confirmation")]
    [InlineData(PreapprovedExpiredMode, "confirmation")]
    [InlineData(ExpiredMode, "missing-known")]
    [InlineData(PreapprovedExpiredMode, "missing-known")]
    [InlineData(ExpiredMode, "missing-expected")]
    [InlineData(PreapprovedExpiredMode, "missing-expected")]
    [InlineData(ExpiredMode, "negative-known")]
    [InlineData(PreapprovedExpiredMode, "negative-known")]
    [InlineData(ExpiredMode, "expected-before-known")]
    [InlineData(PreapprovedExpiredMode, "expected-before-known")]
    [InlineData(ActiveMode, "confirmation")]
    [InlineData(ActiveMode, "missing-known")]
    [InlineData(ActiveMode, "missing-expected")]
    [InlineData(ActiveMode, "negative-known")]
    [InlineData(ActiveMode, "expected-before-known")]
    public void AutoSave0PrepareIsDisclosureOnlyAndRequiresExactBounds(string mode, string changed)
    {
        var request = Request(mode);
        switch (changed)
        {
            case "confirmation": request.UserConfirmedInConversation = true; break;
            case "missing-known": request.MinimumRecoveryGameTick = null; break;
            case "missing-expected": request.ExpectedRecoveryGameTick = null; break;
            case "negative-known": request.MinimumRecoveryGameTick = -1; break;
            case "expected-before-known": request.ExpectedRecoveryGameTick = 11999; break;
        }

        Assert.NotNull(OwnedWorldRecoveryPolicy.ValidateRequest(request));
    }

    [Theory]
    [InlineData(ExpiredMode, "ticket-floor")]
    [InlineData(PreapprovedExpiredMode, "ticket-floor")]
    [InlineData(ExpiredMode, "candidate")]
    [InlineData(PreapprovedExpiredMode, "candidate")]
    [InlineData(ExpiredMode, "not-newer")]
    [InlineData(PreapprovedExpiredMode, "not-newer")]
    [InlineData(ExpiredMode, "primary-missing")]
    [InlineData(PreapprovedExpiredMode, "primary-missing")]
    [InlineData(ExpiredMode, "primary-older")]
    [InlineData(PreapprovedExpiredMode, "primary-older")]
    [InlineData(ExpiredMode, "primary-equal")]
    [InlineData(PreapprovedExpiredMode, "primary-equal")]
    [InlineData(ExpiredMode, "primary-newer")]
    [InlineData(PreapprovedExpiredMode, "primary-newer")]
    [InlineData(ExpiredMode, "source-migration")]
    [InlineData(PreapprovedExpiredMode, "source-migration")]
    [InlineData(ExpiredMode, "unsupported-same-version")]
    [InlineData(PreapprovedExpiredMode, "unsupported-same-version")]
    [InlineData(ExpiredMode, "written-before-ticket")]
    [InlineData(PreapprovedExpiredMode, "written-before-ticket")]
    [InlineData(ExpiredMode, "saved-before-ticket")]
    [InlineData(PreapprovedExpiredMode, "saved-before-ticket")]
    [InlineData(ActiveMode, "ticket-floor")]
    [InlineData(ActiveMode, "candidate")]
    [InlineData(ActiveMode, "not-newer")]
    [InlineData(ActiveMode, "primary-missing")]
    [InlineData(ActiveMode, "primary-older")]
    [InlineData(ActiveMode, "primary-equal")]
    [InlineData(ActiveMode, "primary-newer")]
    [InlineData(ActiveMode, "source-migration")]
    [InlineData(ActiveMode, "unsupported-same-version")]
    [InlineData(ActiveMode, "written-before-ticket")]
    [InlineData(ActiveMode, "saved-before-ticket")]
    public void ProgressVersionAndFreshnessChangesRejectAutoSave0Candidate(string mode, string changed)
    {
        var request = Request(mode);
        var evidence = ReadEvidence();
        var ticketMinimum = 12000L;
        var ticketIssued = Issued;
        var source = Version;
        var current = Version;
        var written = Issued;
        long? primary = 12000;

        switch (changed)
        {
            case "ticket-floor": request.MinimumRecoveryGameTick = 11999; break;
            case "candidate": request.ExpectedRecoveryGameTick = 12346; break;
            case "not-newer": ticketMinimum = 12345; break;
            case "primary-missing": primary = null; break;
            case "primary-older": primary = 11999; break;
            case "primary-equal": primary = 12345; break;
            case "primary-newer": primary = 12346; break;
            case "source-migration": source = "0.10.34.28529"; break;
            case "unsupported-same-version": source = current = "0.10.33.00000"; break;
            case "written-before-ticket": written = Issued.AddSeconds(-3); break;
            case "saved-before-ticket": ticketIssued = evidence.SavedAtUtc.AddSeconds(3); break;
        }

        Assert.NotNull(OwnedWorldAutosaveRecoveryPolicy.ValidateCandidate(request, ticketMinimum,
            ticketIssued, source, current, evidence, written, primary));
    }

    [Theory]
    [InlineData(ExpiredMode, "identity")]
    [InlineData(PreapprovedExpiredMode, "identity")]
    [InlineData(ExpiredMode, "peaceful")]
    [InlineData(PreapprovedExpiredMode, "peaceful")]
    [InlineData(ExpiredMode, "candidate-version")]
    [InlineData(PreapprovedExpiredMode, "candidate-version")]
    [InlineData(ActiveMode, "identity")]
    [InlineData(ActiveMode, "peaceful")]
    [InlineData(ActiveMode, "candidate-version")]
    public void EmbeddedEvidenceMustRemainExactAndPeaceful(string mode, string changed)
    {
        var identity = changed == "identity" ? "different-world" : Identity;
        var evidence = ReadEvidence(identity: identity,
            versionBuild: changed == "candidate-version" ? 29057 : 29088);
        if (changed == "peaceful")
        {
            using var stream = OwnedSavePrefixReaderTests.Fixture(versionPatch: 35, versionBuild: 29088,
                patch: 23, gameDescVersion: 10);
            stream.Position = 19;
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) writer.Write(false);
            stream.Position = 0;
            evidence = OwnedSavePrefixReader.Read(stream, Identity);
        }

        Assert.NotNull(Validate(Request(mode), evidence));
    }

    [Theory]
    [InlineData(ExpiredMode, "old-plugin")]
    [InlineData(PreapprovedExpiredMode, "old-plugin")]
    [InlineData(ExpiredMode, "wrong-mode")]
    [InlineData(PreapprovedExpiredMode, "wrong-mode")]
    [InlineData(ExpiredMode, "not-prepared")]
    [InlineData(PreapprovedExpiredMode, "not-prepared")]
    [InlineData(ExpiredMode, "identity")]
    [InlineData(PreapprovedExpiredMode, "identity")]
    [InlineData(ExpiredMode, "candidate")]
    [InlineData(PreapprovedExpiredMode, "candidate")]
    [InlineData(ExpiredMode, "minimum")]
    [InlineData(PreapprovedExpiredMode, "minimum")]
    [InlineData(ExpiredMode, "confirmation-missing")]
    [InlineData(PreapprovedExpiredMode, "confirmation-missing")]
    [InlineData(ExpiredMode, "commit-allowed")]
    [InlineData(PreapprovedExpiredMode, "commit-allowed")]
    [InlineData(ExpiredMode, "prompt")]
    [InlineData(PreapprovedExpiredMode, "prompt")]
    [InlineData(ExpiredMode, "digest")]
    [InlineData(PreapprovedExpiredMode, "digest")]
    [InlineData(ExpiredMode, "migration")]
    [InlineData(PreapprovedExpiredMode, "migration")]
    [InlineData(ExpiredMode, "unsupported-same-version")]
    [InlineData(PreapprovedExpiredMode, "unsupported-same-version")]
    [InlineData(ActiveMode, "old-plugin")]
    [InlineData(ActiveMode, "wrong-mode")]
    [InlineData(ActiveMode, "not-prepared")]
    [InlineData(ActiveMode, "identity")]
    [InlineData(ActiveMode, "candidate")]
    [InlineData(ActiveMode, "minimum")]
    [InlineData(ActiveMode, "confirmation-missing")]
    [InlineData(ActiveMode, "commit-allowed")]
    [InlineData(ActiveMode, "prompt")]
    [InlineData(ActiveMode, "digest")]
    [InlineData(ActiveMode, "migration")]
    [InlineData(ActiveMode, "unsupported-same-version")]
    public void PluginEchoMustBindTheExactAutoSave0Disclosure(string mode, string changed)
    {
        var plan = MatchingPlan(mode);
        Assert.True(OwnedWorldAutosaveRecoveryPolicy.HasMatchingEcho(Request(mode), plan));

        switch (changed)
        {
            case "old-plugin": plan.RecoveryEvidenceVersion--; break;
            case "wrong-mode": plan.RecoveryMode = mode == ActiveMode ? ExpiredMode : OwnedWorldResumeModes.ReauthorizeExpiredPrimary; break;
            case "not-prepared": plan.Prepared = false; break;
            case "identity": plan.ExactEmbeddedIdentityVerified = false; break;
            case "candidate": plan.CandidateGameTick = 12346; break;
            case "minimum": plan.MinimumGameTick = 12000; break;
            case "confirmation-missing": plan.UserConfirmationRequired = false; break;
            case "commit-allowed": plan.CommitAllowedNow = true; break;
            case "prompt": plan.ConfirmationPrompt = "other"; break;
            case "digest": plan.ConfirmationDigest = string.Empty; break;
            case "migration": plan.SourceGameVersion = "0.10.34.28529"; break;
            case "unsupported-same-version": plan.SourceGameVersion = plan.TargetGameVersion = "0.10.33.00000"; break;
        }

        Assert.False(OwnedWorldAutosaveRecoveryPolicy.HasMatchingEcho(Request(mode), plan));
    }

    [Theory]
    [InlineData(true, true, false, -1, 1, true)]
    [InlineData(true, true, false, 0, 1, true)]
    [InlineData(true, true, false, 1, 2, false)]
    [InlineData(true, true, false, -1, 0, false)]
    [InlineData(true, true, false, -1, -1, false)]
    [InlineData(false, true, false, -1, 1, false)]
    [InlineData(true, false, false, -1, 1, false)]
    [InlineData(true, true, true, -1, 1, false)]
    public void ActiveCredentialMustBeHealthyJournaledUnconsumedAndInsideItsOriginalExpiryWindow(
        bool healthy, bool journalVerified, bool consumed, int issuedDelta, int expiresDelta, bool allowed)
    {
        var now = Issued;
        Assert.Equal(allowed, OwnedWorldReauthorizationPolicy.AllowsActiveProvenance(
            healthy, journalVerified, consumed,
            now.AddSeconds(issuedDelta), now.AddSeconds(expiresDelta), now));
    }

    [Theory]
    [InlineData(12345, true, true, false)]
    [InlineData(12346, true, true, true)]
    [InlineData(12346, false, true, false)]
    [InlineData(12345, true, false, true)]
    [InlineData(12346, false, false, true)]
    public void AutoSave0LeaseRequiresReauthorizationAndStrictAdvanceWithoutChangingOldModes(
        long candidate, bool reauthorizing, bool reauthorizingAutosave0, bool allowed) =>
        Assert.Equal(allowed, OwnedWorldReauthorizationPolicy.AllowsLeaseTick(
            candidate, 12345, reauthorizing, reauthorizingAutosave0));

    private static PrepareOwnedWorldResumeRequest Request(string mode = ExpiredMode) => new()
    {
        RecoveryMode = mode,
        MinimumRecoveryGameTick = 12000,
        ExpectedRecoveryGameTick = 12345,
    };

    private static PreparedOwnedWorldResumePlan MatchingPlan(string mode = ExpiredMode) => new()
    {
        Prepared = true,
        RecoveryMode = mode,
        RecoveryEvidenceVersion = OwnedWorldAutosaveRecoveryPolicy.EvidenceVersionFor(mode),
        ExactEmbeddedIdentityVerified = true,
        CandidateGameTick = 12345,
        MinimumGameTick = 12345,
        UserConfirmationRequired = true,
        ConfirmationPrompt = OwnedWorldAutosaveRecoveryPolicy.ConfirmationPromptFor(mode),
        ConfirmationDigest = ConfirmationDigest,
        SourceGameVersion = Version,
        TargetGameVersion = Version,
        CommitAllowedNow = false,
    };

    private static string? Validate(PrepareOwnedWorldResumeRequest request, OwnedSavePrefixEvidence evidence) =>
        OwnedWorldAutosaveRecoveryPolicy.ValidateCandidate(request, 12000, Issued,
            Version, Version, evidence, Issued, 12000);

    private static OwnedSavePrefixEvidence ReadEvidence(string identity = Identity, int versionBuild = 29088)
    {
        using var stream = OwnedSavePrefixReaderTests.Fixture(identity: identity, versionPatch: 35,
            versionBuild: versionBuild, patch: 23, gameDescVersion: 10);
        return OwnedSavePrefixReader.Read(stream, Identity);
    }
}
