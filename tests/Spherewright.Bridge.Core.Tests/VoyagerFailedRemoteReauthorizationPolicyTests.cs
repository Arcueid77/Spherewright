using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerFailedRemoteReauthorizationPolicyTests
{
    private static readonly string Context = CanonicalStateHash.Combine("synthetic-owner-context");
    private static PrepareOwnedWorldResumeRequest Request() => new()
    {
        RecoveryMode = OwnedWorldResumeModes.ReauthorizeFailedRemoteLastExit,
        ExpectedRemoteRecoveryContextFingerprint = Context, PriorConsumedResumeToken = "synthetic-consumed",
        PriorActionKnownTerminalFailed = true, RecoveryDeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(30),
        RecoveryScope = VoyagerRecoveryWindowProposal.TaskScope, MaximumChargeGameTicks = 9000, MaximumChargeWallSeconds = 240,
    };

    [Theory]
    [InlineData("context")] [InlineData("format")] [InlineData("token")] [InlineData("false_ack")]
    [InlineData("unknown_ack")] [InlineData("source")] [InlineData("legacy")] [InlineData("middle")]
    [InlineData("confirmation")] [InlineData("tick")] [InlineData("deadline")] [InlineData("scope")]
    [InlineData("charge_ticks")] [InlineData("wall")] [InlineData("nan")] [InlineData("default")]
    public void EveryPartialOrLegacyCombinationFailsClosed(string fault)
    {
        var r = Request();
        switch (fault)
        {
            case "context": r.ExpectedRemoteRecoveryContextFingerprint = ""; break;
            case "format": r.ExpectedRemoteRecoveryContextFingerprint = Context.ToUpperInvariant(); break;
            case "token": r.PriorConsumedResumeToken = ""; break;
            case "false_ack": r.PriorActionKnownTerminalFailed = false; break;
            case "unknown_ack": r.PriorActionKnownTerminalFailed = null; break;
            case "source": r.PriorSourceFingerprint = Context; break;
            case "legacy": r.PriorLegacySourceFingerprint = Context; break;
            case "middle": r.PriorIntermediateSourceFingerprint = Context; break;
            case "confirmation": r.UserConfirmedInConversation = true; break;
            case "tick": r.ExpectedRecoveryGameTick = 101; break;
            case "deadline": r.RecoveryDeadlineUtc = null; break;
            case "scope": r.RecoveryScope = "withdraw-or-select-another-task"; break;
            case "charge_ticks": r.MaximumChargeGameTicks = 0; break;
            case "wall": r.MaximumChargeWallSeconds = 301; break;
            case "nan": r.MaximumChargeWallSeconds = double.NaN; break;
            case "default": r.RecoveryMode = OwnedWorldResumeModes.Default; break;
        }
        Assert.NotNull(OwnedWorldRecoveryPolicy.ValidateRequest(r));
    }

    [Fact]
    public void ExactEightFieldCommitmentPreservesDomainOrderAndWholeJournalContent()
    {
        const string journey = "{\"syntheticDouble\":0.10000000000000001}";
        var result = VoyagerFailedRemoteReauthorizationPolicy.ContextFingerprint(journey, "checkpoint", "native-cp",
            "primary", 101, 90, "journal-prefix", "whole-journal-hash");
        Assert.Equal(CanonicalStateHash.Combine("voyager-zero-progress-remote-context-v1", journey,
            "checkpoint", "native-cp", "primary", 101L, 90L, "journal-prefix", "whole-journal-hash"), result);
        Assert.NotEqual(result, VoyagerFailedRemoteReauthorizationPolicy.ContextFingerprint(
            "{\"syntheticDouble\":0.1}", "checkpoint", "native-cp", "primary", 101, 90, "journal-prefix", "whole-journal-hash"));
        Assert.NotEqual(result, VoyagerFailedRemoteReauthorizationPolicy.ContextFingerprint(journey,
            "checkpoint", "native-cp", "primary", 101, 90, "journal-prefix", "different-whole-journal"));
        Assert.NotEqual(result, VoyagerFailedRemoteReauthorizationPolicy.ContextFingerprint(journey,
            "checkpoint", "primary", "native-cp", 101, 90, "journal-prefix", "whole-journal-hash"));
    }

    private static VoyagerRemoteResumeFacts Facts(long localElapsed = 1) => new()
    {
        DefaultLastExit = true, ExactOwnedIdentityAndVersion = true, Peaceful = true,
        FullSourceAndPrimaryEvidence = true, ExactDurableJourneyAndCheckpoint = true, OriginalJournalCheckpointPresent = true,
        // Deliberately false: the new entry must never invent opened legacy provenance.
        QuarantineMatchesOriginalAction = false, RemoteLandedOutbound = true, CheckpointFlightSucceededWithoutReload = true,
        SourceGameTick = 100 + localElapsed, TicketMinimumGameTick = 100 + localElapsed, PrimaryGameTick = 90,
        LastAccountedGameTick = 100, LastLandedGameTick = 100, SpentGameTicks = 500,
        MaximumGameTicks = 108000, ReturnReserveGameTicks = 54000, NowUtc = DateTimeOffset.UtcNow,
        OriginalIssuedAtUtc = DateTimeOffset.UtcNow.AddHours(-2), OriginalDeadlineUtc = DateTimeOffset.UtcNow.AddHours(-1),
        RecoveryProposal = new(DateTimeOffset.UtcNow.AddMinutes(30), VoyagerRecoveryWindowProposal.TaskScope, 9000, 240),
    };

    [Theory]
    [InlineData("context")] [InlineData("pair")] [InlineData("ack")] [InlineData("source_after_min")]
    [InlineData("source_before_min")] [InlineData("at_landed")] [InlineData("before_landed")]
    [InlineData("missing_landed")] [InlineData("negative_landed")]
    [InlineData("accounted")] [InlineData("success")] [InlineData("overflow")] [InlineData("new_primary")]
    [InlineData("caps_tick")] [InlineData("caps_wall")] [InlineData("spent")] [InlineData("utc")]
    [InlineData("identity")] [InlineData("phase")] [InlineData("checkpoint")] [InlineData("journal")]
    [InlineData("expired")] [InlineData("larger_window")] [InlineData("files")]
    public void FreshAuthorityStillCannotResetProgressCapsOrOriginalLimits(string fault)
    {
        var f = Facts(600); var expected = Context; var pair = true; var ack = true; long? success = 100;
        var prior = new VoyagerRecoveryWindowProposal(f.NowUtc.AddMinutes(-5), VoyagerRecoveryWindowProposal.TaskScope, 9000, 240);
        switch (fault)
        {
            case "context": expected = CanonicalStateHash.Combine("changed"); break;
            case "pair": pair = false; break;
            case "ack": ack = false; break;
            case "source_after_min": f.SourceGameTick++; break;
            case "source_before_min": f.SourceGameTick--; break;
            case "at_landed": f.SourceGameTick = f.TicketMinimumGameTick = 100; break;
            case "before_landed": f.SourceGameTick = f.TicketMinimumGameTick = 99; break;
            case "missing_landed": f.LastLandedGameTick = null; break;
            case "negative_landed": f.LastLandedGameTick = -1; break;
            case "accounted": f.LastAccountedGameTick++; break;
            case "success": success++; break;
            case "overflow": f.LastLandedGameTick = long.MaxValue; break;
            case "new_primary": f.PrimaryGameTick = f.SourceGameTick + 1; break;
            case "caps_tick": prior = new(prior.DeadlineUtc, prior.Scope, 8999, 240); break;
            case "caps_wall": prior = new(prior.DeadlineUtc, prior.Scope, 9000, 239); break;
            case "spent": f.SpentGameTicks = 108000; break;
            case "utc": f.RecoveryProposal = new(f.NowUtc.AddMinutes(5), prior.Scope, 9000, 240); break;
            case "identity": f.ExactOwnedIdentityAndVersion = false; break;
            case "phase": f.RemoteLandedOutbound = false; break;
            case "checkpoint": f.ExactDurableJourneyAndCheckpoint = false; break;
            case "journal": f.OriginalJournalCheckpointPresent = false; break;
            case "expired": f.RecoveryProposal = new(f.NowUtc, prior.Scope, 9000, 240); break;
            case "larger_window": f.RecoveryProposal = new(f.NowUtc.AddHours(2), prior.Scope, 9000, 240); break;
            case "files": f.FullSourceAndPrimaryEvidence = false; break;
        }
        Assert.NotNull(VoyagerFailedRemoteReauthorizationPolicy.Blocker(f, expected, Context, pair, ack, success, prior));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(600)] [InlineData(24000)]
    public void ExactCurrentMinimumAllowsBudgetedLocalElapsedWithoutOpeningOldProvenance(long localElapsed)
    {
        var f = Facts(localElapsed);
        var originalDeadline = f.OriginalDeadlineUtc;
        var effectiveDeadline = f.RecoveryProposal!.DeadlineUtc;
        Assert.Null(OwnedWorldRecoveryPolicy.ValidateRequest(Request()));
        Assert.NotNull(VoyagerRemoteResumePolicy.Blocker(f));
        Assert.Null(VoyagerFailedRemoteReauthorizationPolicy.Blocker(f, Context, Context, true, true, 100,
            new(f.NowUtc.AddMinutes(-1), VoyagerRecoveryWindowProposal.TaskScope, 9000, 240)));
        Assert.False(f.QuarantineMatchesOriginalAction);
        Assert.Equal(100 + localElapsed, f.SourceGameTick);
        Assert.Equal(f.SourceGameTick, f.TicketMinimumGameTick);
        Assert.Equal(500, f.SpentGameTicks); Assert.Equal(100, f.LastAccountedGameTick);
        Assert.Equal(100, f.LastLandedGameTick); Assert.Equal(108000, f.MaximumGameTicks);
        Assert.Equal(54000, f.ReturnReserveGameTicks);
        Assert.Equal(originalDeadline, f.OriginalDeadlineUtc);
        Assert.Equal(effectiveDeadline, f.RecoveryProposal.DeadlineUtc);
    }

    [Fact]
    public void EveryLocalElapsedTickConsumesOriginalBudgetBeforeTheReturnAndStopReserve()
    {
        var f = Facts();
        var elapsedLimit = f.MaximumGameTicks - f.SpentGameTicks - f.ReturnReserveGameTicks
            - 1 - VoyagerLocalWalkReservePolicy.StopGameTicks;
        f.SourceGameTick = f.TicketMinimumGameTick = f.LastLandedGameTick!.Value + elapsedLimit;
        var prior = new VoyagerRecoveryWindowProposal(f.NowUtc.AddMinutes(-1), VoyagerRecoveryWindowProposal.TaskScope, 9000, 240);
        Assert.Null(VoyagerFailedRemoteReauthorizationPolicy.Blocker(f, Context, Context, true, true, 100, prior));

        f.SourceGameTick++; f.TicketMinimumGameTick++;
        Assert.Equal("remote_resume_original_return_time_insufficient",
            VoyagerFailedRemoteReauthorizationPolicy.Blocker(f, Context, Context, true, true, 100, prior));
        Assert.Equal(500, f.SpentGameTicks); Assert.Equal(100, f.LastAccountedGameTick);
        Assert.Equal(100, f.LastLandedGameTick); Assert.Equal(108000, f.MaximumGameTicks);
    }
}
