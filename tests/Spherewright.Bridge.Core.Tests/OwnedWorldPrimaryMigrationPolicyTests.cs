using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class OwnedWorldPrimaryMigrationPolicyTests
{
    [Theory]
    [InlineData("0.10.35.29104", "0.10.35.29131", true)]
    [InlineData("0.10.35.29131", "0.10.35.29104", false)]
    [InlineData("0.10.35.29104", "0.10.35.29104", false)]
    [InlineData("0.10.35.29088", "0.10.35.29131", false)]
    [InlineData("0.10.34.28529", "0.10.35.29131", false)]
    [InlineData("0.10.35.29104", "0.10.35.29132", false)]
    public void OnlyTheOneActiveMigrationPairQualifies(string source, string target, bool allowed) =>
        Assert.Equal(allowed, OwnedWorldPrimaryMigrationPolicy.IsSupportedPair(source, target));

    [Theory]
    [InlineData("valid", true)]
    [InlineData("confirmation-at-prepare", false)]
    [InlineData("caller-selected-tick", false)]
    [InlineData("wrong-mode", false)]
    public void PreparationIsOnlyTheExactPrimaryDisclosure(string fault, bool valid)
    {
        var request = new PrepareOwnedWorldResumeRequest
        { RecoveryMode = OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary };
        if (fault == "confirmation-at-prepare") request.UserConfirmedInConversation = true;
        if (fault == "caller-selected-tick") request.ExpectedRecoveryGameTick = 12345;
        if (fault == "wrong-mode") request.RecoveryMode = OwnedWorldResumeModes.Default;
        Assert.Equal(valid, OwnedWorldPrimaryMigrationPolicy.ValidateRequest(request) is null);
        if (fault != "wrong-mode")
            Assert.Equal(valid, OwnedWorldRecoveryPolicy.ValidateRequest(request) is null);
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("wrong-saved-tick", false)]
    [InlineData("wrong-identity", false)]
    [InlineData("non-peaceful", false)]
    [InlineData("wrong-source-version", false)]
    [InlineData("wrong-target-version", false)]
    public void CandidateMustBeTheExactSourcePrimary(string fault, bool valid)
    {
        const string identity = "synthetic-owned-primary";
        using var stream = OwnedSavePrefixReaderTests.Fixture(
            identity: fault == "wrong-identity" ? "other-world" : identity,
            versionPatch: 35, versionBuild: 29104, patch: 23, gameDescVersion: 10);
        if (fault == "non-peaceful") { stream.Position = 19; stream.WriteByte(0); stream.Position = 0; }
        var evidence = OwnedSavePrefixReader.Read(stream, identity);
        var result = OwnedWorldPrimaryMigrationPolicy.ValidateCandidate(
            fault == "wrong-saved-tick" ? 12346 : 12345,
            fault == "wrong-source-version" ? "0.10.35.29088" : "0.10.35.29104",
            fault == "wrong-target-version" ? "0.10.35.29132" : "0.10.35.29131", evidence);
        Assert.Equal(valid, result is null);
    }

    [Theory]
    [InlineData(23, 10, true)]
    [InlineData(22, 10, false)]
    [InlineData(23, 9, false)]
    public void TargetPrefixKeepsTheResearchedNativeFormatTuple(int patch, int descriptor, bool valid)
    {
        const string identity = "synthetic-target-primary";
        using var stream = OwnedSavePrefixReaderTests.Fixture(
            identity: identity, versionPatch: 35, versionBuild: 29131, patch: patch, gameDescVersion: descriptor);
        if (valid)
        {
            var prefix = OwnedSavePrefixReader.Read(stream, identity);
            Assert.Equal("0.10.35.29131", prefix.GameVersion);
            Assert.True(prefix.MatchesExpectedIdentity);
        }
        else Assert.Throws<InvalidDataException>(() => OwnedSavePrefixReader.Read(stream, identity));
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("old-mode", false)]
    [InlineData("old-evidence", false)]
    [InlineData("wrong-version", false)]
    [InlineData("missing-identity", false)]
    [InlineData("different-tick", false)]
    [InlineData("negative-tick", false)]
    [InlineData("missing-confirmation", false)]
    [InlineData("premature-commit", false)]
    [InlineData("missing-digest", false)]
    [InlineData("old-prompt", false)]
    public void EchoCannotDowngradeTheNativeDisclosure(string fault, bool valid)
    {
        var plan = new PreparedOwnedWorldResumePlan
        {
            Prepared = true, RecoveryMode = OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary,
            RecoveryEvidenceVersion = 5, SourceGameVersion = "0.10.35.29104", TargetGameVersion = "0.10.35.29131",
            ExactEmbeddedIdentityVerified = true, CandidateGameTick = 12345, MinimumGameTick = 12345,
            UserConfirmationRequired = true, CommitAllowedNow = false, ConfirmationDigest = "sha256:synthetic-plan",
            ConfirmationPrompt = OwnedWorldPrimaryMigrationPolicy.ConfirmationPrompt,
        };
        switch (fault)
        {
            case "old-mode": plan.RecoveryMode = OwnedWorldResumeModes.Default; break;
            case "old-evidence": plan.RecoveryEvidenceVersion = 2; break;
            case "wrong-version": plan.TargetGameVersion = "0.10.35.29132"; break;
            case "missing-identity": plan.ExactEmbeddedIdentityVerified = false; break;
            case "different-tick": plan.CandidateGameTick++; break;
            case "negative-tick": plan.CandidateGameTick = plan.MinimumGameTick = -1; break;
            case "missing-confirmation": plan.UserConfirmationRequired = false; break;
            case "premature-commit": plan.CommitAllowedNow = true; break;
            case "missing-digest": plan.ConfirmationDigest = ""; break;
            case "old-prompt": plan.ConfirmationPrompt = OwnedWorldReauthorizationPolicy.ConfirmationPrompt; break;
        }
        Assert.Equal(valid, OwnedWorldPrimaryMigrationPolicy.HasMatchingEcho(plan));
    }
}
