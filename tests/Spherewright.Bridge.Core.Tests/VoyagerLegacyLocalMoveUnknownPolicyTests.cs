using System.Security.Cryptography;
using System.Text;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerLegacyLocalMoveUnknownPolicyTests
{
    [Fact]
    public void HashCommitsExactUtf8BytesWithoutRenderingOrUnicodeNormalization()
    {
        const string json = "{\"capture\":\"保护\",\"number\":1.00}";
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        Assert.Equal(expected, VoyagerLegacyLocalMoveUnknownPolicy.HashEvidence(json));
        Assert.NotEqual(expected, VoyagerLegacyLocalMoveUnknownPolicy.HashEvidence(json.Replace("1.00", "1")));
        Assert.NotEqual(expected, VoyagerLegacyLocalMoveUnknownPolicy.HashEvidence(json + " "));
        Assert.Null(VoyagerLegacyLocalMoveUnknownPolicy.HashEvidence("\ud800"));
        Assert.Null(VoyagerLegacyLocalMoveUnknownPolicy.HashEvidence(new string('x', 65537)));
        Assert.NotNull(VoyagerLegacyLocalMoveUnknownPolicy.HashEvidence(new string('x', 65536)));
        Assert.Null(VoyagerLegacyLocalMoveUnknownPolicy.HashEvidence(new string('界', 21846)));
    }

    [Theory]
    [InlineData(OwnedWorldResumeModes.VerifiedNewerLastExit)]
    [InlineData(OwnedWorldResumeModes.ReauthorizeExpiredPrimary)]
    [InlineData(OwnedWorldResumeModes.ReauthorizeExpiredAutosave0)]
    [InlineData(OwnedWorldResumeModes.ReauthorizeFixedAutosave0)]
    [InlineData(OwnedWorldResumeModes.ReauthorizeVersionChangedPrimary)]
    [InlineData(OwnedWorldResumeModes.ReauthorizeFailedRemoteLastExit)]
    public void LegacyCannotBorrowAnotherRecoveryMode(string mode)
    {
        var request = Request(); request.RecoveryMode = mode;
        Assert.NotNull(OwnedWorldRecoveryPolicy.ValidateRequest(request));
    }

    [Theory]
    [InlineData("token")] [InlineData("source")] [InlineData("attestation")]
    [InlineData("v2")] [InlineData("v3")] [InlineData("context")]
    [InlineData("json_missing")] [InlineData("hash_missing")] [InlineData("hash_changed")]
    [InlineData("uppercase_hash")] [InlineData("prepare_confirmation")]
    public void LegacyRequiresItsOwnCompletePairAndNoPriorFailureClaim(string fault)
    {
        var r = Request();
        switch (fault)
        {
            case "token": r.PriorConsumedResumeToken = "synthetic-token"; break;
            case "source": r.PriorSourceFingerprint = CanonicalStateHash.Combine("source"); break;
            case "attestation": r.PriorActionKnownTerminalFailed = false; break;
            case "v2": r.PriorLegacySourceFingerprint = CanonicalStateHash.Combine("v2"); break;
            case "v3": r.PriorIntermediateSourceFingerprint = CanonicalStateHash.Combine("v3"); break;
            case "context": r.ExpectedRemoteRecoveryContextFingerprint = CanonicalStateHash.Combine("context"); break;
            case "json_missing": r.LegacyLocalMoveUnknownEvidenceJson = ""; break;
            case "hash_missing": r.ExpectedLegacyLocalMoveUnknownEvidenceHash = ""; break;
            case "hash_changed": r.LegacyLocalMoveUnknownEvidenceJson += " "; break;
            case "uppercase_hash": r.ExpectedLegacyLocalMoveUnknownEvidenceHash = r.ExpectedLegacyLocalMoveUnknownEvidenceHash.ToUpperInvariant(); break;
            case "prepare_confirmation": r.UserConfirmedInConversation = true; break;
        }
        Assert.NotNull(OwnedWorldRecoveryPolicy.ValidateRequest(r));
    }

    [Fact]
    public void OldDefaultIsUnchangedAndExistingFiniteWindowIsStillValidated()
    {
        Assert.Null(OwnedWorldRecoveryPolicy.ValidateRequest(new()));
        var r = Request(); Assert.Null(OwnedWorldRecoveryPolicy.ValidateRequest(r));
        r.RecoveryDeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(20);
        Assert.NotNull(OwnedWorldRecoveryPolicy.ValidateRequest(r));
        r.RecoveryScope = VoyagerRecoveryWindowProposal.TaskScope;
        r.MaximumChargeGameTicks = 9000; r.MaximumChargeWallSeconds = 240;
        Assert.Null(OwnedWorldRecoveryPolicy.ValidateRequest(r));
        r.MaximumChargeWallSeconds = 301; Assert.NotNull(OwnedWorldRecoveryPolicy.ValidateRequest(r));
    }

    private static PrepareOwnedWorldResumeRequest Request()
    {
        const string json = "{\"synthetic\":\"carrier input validation only\"}";
        return new() { LegacyLocalMoveUnknownEvidenceJson = json,
            ExpectedLegacyLocalMoveUnknownEvidenceHash = VoyagerLegacyLocalMoveUnknownPolicy.HashEvidence(json)! };
    }
}
