namespace Spherewright.Bridge.Core.Safety;

// A fixed LastExit continuation proves a new runtime owner. It neither loads
// the successful pre-flight checkpoint nor renews any original journey grant.
public sealed class VoyagerRemoteResumeFacts
{
    public bool DefaultLastExit { get; set; }
    public bool ExactOwnedIdentityAndVersion { get; set; }
    public bool Peaceful { get; set; }
    public bool FullSourceAndPrimaryEvidence { get; set; }
    public bool ExactDurableJourneyAndCheckpoint { get; set; }
    public bool QuarantineMatchesOriginalAction { get; set; }
    public bool OriginalJournalCheckpointPresent { get; set; }
    public bool RemoteLandedOutbound { get; set; }
    public bool CheckpointFlightSucceededWithoutReload { get; set; }
    public long SourceGameTick { get; set; }
    public long PrimaryGameTick { get; set; }
    public long TicketMinimumGameTick { get; set; }
    public long LastAccountedGameTick { get; set; }
    public long? LastLandedGameTick { get; set; }
    public long SpentGameTicks { get; set; }
    public long MaximumGameTicks { get; set; }
    public long ReturnReserveGameTicks { get; set; }
    public DateTimeOffset NowUtc { get; set; }
    public DateTimeOffset OriginalDeadlineUtc { get; set; }
    public DateTimeOffset OriginalIssuedAtUtc { get; set; }
    public VoyagerRecoveryWindowProposal? RecoveryProposal { get; set; }
}

public static class VoyagerRemoteResumePolicy
{
    public const int EvidenceVersion = 1;
    public static string? Blocker(VoyagerRemoteResumeFacts f)
    {
        if (f is null) throw new ArgumentNullException(nameof(f));
        if (!f.DefaultLastExit) return "remote_resume_fixed_last_exit_required";
        if (!f.ExactOwnedIdentityAndVersion || !f.Peaceful) return "remote_resume_native_identity_unproved";
        if (!f.FullSourceAndPrimaryEvidence) return "remote_resume_full_file_evidence_unproved";
        if (!f.ExactDurableJourneyAndCheckpoint || !f.QuarantineMatchesOriginalAction
            || !f.OriginalJournalCheckpointPresent) return "remote_resume_original_provenance_unproved";
        if (!f.RemoteLandedOutbound || !f.CheckpointFlightSucceededWithoutReload)
            return "remote_resume_successful_outbound_required";
        if (f.PrimaryGameTick < 0 || f.TicketMinimumGameTick < 0 || f.LastAccountedGameTick < 0
            || !f.LastLandedGameTick.HasValue || f.LastLandedGameTick < 0
            || f.SourceGameTick < f.PrimaryGameTick || f.SourceGameTick < f.TicketMinimumGameTick
            || f.SourceGameTick < f.LastAccountedGameTick || f.SourceGameTick < f.LastLandedGameTick)
            return "remote_resume_newer_progress_rollback_forbidden";
        if (f.RecoveryProposal is { } proposal && VoyagerRecoveryWindowPolicy.ProposalBlocker(proposal, f.NowUtc,
                f.OriginalIssuedAtUtc, f.OriginalDeadlineUtc, f.ReturnReserveGameTicks) is { } proposalBlocker)
            return proposalBlocker;
        var effectiveDeadline = f.RecoveryProposal?.DeadlineUtc ?? f.OriginalDeadlineUtc;
        if (f.SpentGameTicks < 0 || f.MaximumGameTicks < 1 || f.ReturnReserveGameTicks < 3600
            || (decimal)f.SpentGameTicks + f.SourceGameTick - f.LastLandedGameTick.Value
                + f.ReturnReserveGameTicks + 1 + VoyagerLocalWalkReservePolicy.StopGameTicks > f.MaximumGameTicks
            || f.NowUtc == default || f.OriginalDeadlineUtc == default
            || (effectiveDeadline - f.NowUtc).TotalSeconds <= f.ReturnReserveGameTicks / 60d
                + VoyagerRemoteIdleAdmissionPolicy.StopWallSeconds + 1d / 60d)
            return "remote_resume_original_return_time_insufficient";
        return null;
    }
}
