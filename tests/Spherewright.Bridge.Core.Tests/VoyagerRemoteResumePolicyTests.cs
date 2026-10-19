using Spherewright.Bridge.Core.Safety;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class VoyagerRemoteResumePolicyTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FixedLastExitContinuationRetainsEveryOriginalTimeField()
    {
        var f = Good(); var deadline = f.OriginalDeadlineUtc; var spent = f.SpentGameTicks;
        Assert.Null(VoyagerRemoteResumePolicy.Blocker(f));
        Assert.Equal(deadline, f.OriginalDeadlineUtc); Assert.Equal(spent, f.SpentGameTicks);
        f.NowUtc = deadline.AddSeconds(-f.ReturnReserveGameTicks / 60d - 1);
        Assert.Equal("remote_resume_original_return_time_insufficient", VoyagerRemoteResumePolicy.Blocker(f));
        Assert.Equal(deadline, f.OriginalDeadlineUtc);
    }

    [Theory]
    [InlineData("mode", "remote_resume_fixed_last_exit_required")]
    [InlineData("identity", "remote_resume_native_identity_unproved")]
    [InlineData("combat", "remote_resume_native_identity_unproved")]
    [InlineData("full_file", "remote_resume_full_file_evidence_unproved")]
    [InlineData("durable", "remote_resume_original_provenance_unproved")]
    [InlineData("action", "remote_resume_original_provenance_unproved")]
    [InlineData("journal", "remote_resume_original_provenance_unproved")]
    [InlineData("phase", "remote_resume_successful_outbound_required")]
    [InlineData("checkpoint_reload", "remote_resume_successful_outbound_required")]
    [InlineData("primary", "remote_resume_newer_progress_rollback_forbidden")]
    [InlineData("minimum", "remote_resume_newer_progress_rollback_forbidden")]
    [InlineData("accounted", "remote_resume_newer_progress_rollback_forbidden")]
    [InlineData("landed", "remote_resume_newer_progress_rollback_forbidden")]
    [InlineData("unknown_landed", "remote_resume_newer_progress_rollback_forbidden")]
    [InlineData("expired", "remote_resume_original_return_time_insufficient")]
    [InlineData("spent", "remote_resume_original_return_time_insufficient")]
    [InlineData("overflow", "remote_resume_original_return_time_insufficient")]
    public void MissingChangedOrExpiredProofNeverCreatesAContinuation(string fault, string code)
    {
        var f = Good();
        switch (fault)
        {
            case "mode": f.DefaultLastExit = false; break;
            case "identity": f.ExactOwnedIdentityAndVersion = false; break;
            case "combat": f.Peaceful = false; break;
            case "full_file": f.FullSourceAndPrimaryEvidence = false; break;
            case "durable": f.ExactDurableJourneyAndCheckpoint = false; break;
            case "action": f.QuarantineMatchesOriginalAction = false; break;
            case "journal": f.OriginalJournalCheckpointPresent = false; break;
            case "phase": f.RemoteLandedOutbound = false; break;
            case "checkpoint_reload": f.CheckpointFlightSucceededWithoutReload = false; break;
            case "primary": f.PrimaryGameTick = f.SourceGameTick + 1; break;
            case "minimum": f.TicketMinimumGameTick = f.SourceGameTick + 1; break;
            case "accounted": f.LastAccountedGameTick = f.SourceGameTick + 1; break;
            case "landed": f.LastLandedGameTick = f.SourceGameTick + 1; break;
            case "unknown_landed": f.LastLandedGameTick = null; break;
            case "expired": f.NowUtc = f.OriginalDeadlineUtc; break;
            case "spent": f.SpentGameTicks = f.MaximumGameTicks; break;
            case "overflow": f.SpentGameTicks = long.MaxValue; break;
        }
        Assert.Equal(code, VoyagerRemoteResumePolicy.Blocker(f));
    }

    [Fact]
    public void EqualityWithNewerPrimaryIsAllowedButOneOlderTickIsForbidden()
    {
        var f = Good(); f.PrimaryGameTick = f.SourceGameTick;
        Assert.Null(VoyagerRemoteResumePolicy.Blocker(f));
        f.PrimaryGameTick++;
        Assert.Equal("remote_resume_newer_progress_rollback_forbidden", VoyagerRemoteResumePolicy.Blocker(f));
    }

    private static VoyagerRemoteResumeFacts Good() => new()
    {
        DefaultLastExit = true, ExactOwnedIdentityAndVersion = true, Peaceful = true,
        FullSourceAndPrimaryEvidence = true, ExactDurableJourneyAndCheckpoint = true,
        QuarantineMatchesOriginalAction = true, OriginalJournalCheckpointPresent = true,
        RemoteLandedOutbound = true, CheckpointFlightSucceededWithoutReload = true,
        SourceGameTick = 1000, PrimaryGameTick = 900, TicketMinimumGameTick = 950,
        LastAccountedGameTick = 950, LastLandedGameTick = 950, SpentGameTicks = 100,
        MaximumGameTicks = 108000, ReturnReserveGameTicks = 54000,
        NowUtc = Now, OriginalDeadlineUtc = Now.AddHours(1),
    };
}
