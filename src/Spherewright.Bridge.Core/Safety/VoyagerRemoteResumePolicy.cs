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
        return RemainingJourneyBlocker(f);
    }

    // Both entry points establish their own provenance before sharing these
    // unchanged phase, rollback and original finite-budget checks.
    internal static string? RemainingJourneyBlocker(VoyagerRemoteResumeFacts f, NativeResumeDecisionEvidence? evidence = null)
    {
        bool All(params (string Name, bool Value)[] checks)
        {
            evidence?.NotReached(checks.Select(c => c.Name).ToArray());
            foreach (var c in checks) if (evidence is null ? !c.Value : !evidence.Require(c.Name, c.Value)) return false;
            return true;
        }
        if (!All(("remaining_remote_landed", f.RemoteLandedOutbound), ("remaining_successful_outbound", f.CheckpointFlightSucceededWithoutReload)))
            return "remote_resume_successful_outbound_required";
        if (!All(("remaining_primary_nonnegative", f.PrimaryGameTick >= 0), ("remaining_minimum_nonnegative", f.TicketMinimumGameTick >= 0),
            ("remaining_accounted_nonnegative", f.LastAccountedGameTick >= 0), ("remaining_landing_observed", f.LastLandedGameTick.HasValue),
            ("remaining_landing_nonnegative", f.LastLandedGameTick >= 0), ("remaining_source_covers_primary", f.SourceGameTick >= f.PrimaryGameTick),
            ("remaining_source_covers_minimum", f.SourceGameTick >= f.TicketMinimumGameTick),
            ("remaining_source_covers_accounted", f.SourceGameTick >= f.LastAccountedGameTick),
            ("remaining_source_covers_landing", f.SourceGameTick >= f.LastLandedGameTick)))
            return "remote_resume_newer_progress_rollback_forbidden";
        if (f.RecoveryProposal is { } proposal && VoyagerRecoveryWindowPolicy.ProposalBlocker(proposal, f.NowUtc,
                f.OriginalIssuedAtUtc, f.OriginalDeadlineUtc, f.ReturnReserveGameTicks) is { } proposalBlocker)
        { evidence?.Require("remaining_existing_proposal_valid", false); return proposalBlocker; }
        var effectiveDeadline = f.RecoveryProposal?.DeadlineUtc ?? f.OriginalDeadlineUtc;
        if (!All(("remaining_spent_nonnegative", f.SpentGameTicks >= 0), ("remaining_maximum_positive", f.MaximumGameTicks >= 1),
            ("remaining_return_minimum", f.ReturnReserveGameTicks >= 3600),
            ("remaining_original_tick_reserve", (decimal)f.SpentGameTicks + f.SourceGameTick - f.LastLandedGameTick!.Value
                + f.ReturnReserveGameTicks + 1 + VoyagerLocalWalkReservePolicy.StopGameTicks <= f.MaximumGameTicks),
            ("remaining_clock_observed", f.NowUtc != default), ("remaining_deadline_observed", f.OriginalDeadlineUtc != default),
            ("remaining_original_wall_reserve", (effectiveDeadline - f.NowUtc).TotalSeconds > f.ReturnReserveGameTicks / 60d
                + VoyagerRemoteIdleAdmissionPolicy.StopWallSeconds + 1d / 60d)))
            return "remote_resume_original_return_time_insufficient";
        return null;
    }
}
