using Spherewright.Contracts.Sessions;

namespace Spherewright.Bridge.Core.Safety;

// A new safety admission from the current saved boundary, never a successful
// outcome certificate or replay of the old outbound action.
public sealed class VoyagerFailedLandingSourceFacts
{
    public bool FixedLastExit { get; set; }
    public bool CompleteSourceAndPrimary { get; set; }
    public bool ExactOwnedIdentityAndNativeVersion { get; set; }
    public bool Peaceful { get; set; }
    public bool ExactOriginalJourneyCheckpointAndJournal { get; set; }
    public bool OriginalCheckpointRecoveryRequired { get; set; }
    public bool OriginalCheckpointFlightSucceeded { get; set; }
    public bool OriginalOutboundFailureContext { get; set; }
    public bool ExactSelectedRemotePlanet { get; set; }
    public bool CallerAttestsTerminalFailure { get; set; }
    public long SourceGameTick { get; set; }
    public long PrimaryGameTick { get; set; }
    public long TicketMinimumGameTick { get; set; }
    public long CheckpointGameTick { get; set; }
    public long LastAccountedGameTick { get; set; }
    public long SpentGameTicks { get; set; }
    public long MaximumGameTicks { get; set; }
    public long ReturnReserveGameTicks { get; set; }
    public DateTimeOffset NowUtc { get; set; }
}

public sealed class VoyagerFailedLandingRecoveryDecision
{
    public bool Proved { get; internal set; }
    public string? BlockerCode { get; internal set; }
    public long? SavedAccountingAnchorGameTick { get; internal set; }
    public long? ConservedSpentGameTicks { get; internal set; }
}

public static class VoyagerFailedLandingRecoveryPolicy
{
    public const string Mode = OwnedWorldResumeModes.AuthorizeFailedLandingLastExit;
    public const int EvidenceVersion = 1;

    public static bool HasMatchingEcho(PrepareOwnedWorldResumeRequest r, PreparedOwnedWorldResumePlan? p) =>
        p is not null && ValidateRequest(r) is null && p.Prepared && p.RecoveryMode == Mode
        && p.RecoveryEvidenceVersion == EvidenceVersion && p.FailedLandingRecoveryAuthorizationUsed
        && !string.IsNullOrEmpty(p.SourceGameVersion) && p.SourceGameVersion == p.TargetGameVersion
        && p.SavedLandingAccountingAnchorGameTick == r.ExpectedRecoveryGameTick
        && p.CandidateGameTick == r.ExpectedRecoveryGameTick && p.MinimumGameTick == r.ExpectedRecoveryGameTick
        && p.ExpectedPlanetId > 0 && Guid.TryParse(p.OriginalFailedFlightActionId, out _)
        && p.PriorActionTerminalFailureAttested && !p.PriorActionTerminalCompletionAttested
        && p.ExactEmbeddedIdentityVerified && p.UserConfirmationRequired && !p.CommitAllowedNow
        && OwnedWorldRecoveryPolicy.IsCanonicalFingerprint(p.ConfirmationDigest)
        && p.EffectiveRecoveryDeadlineUtc == r.RecoveryDeadlineUtc && p.RecoveryScope == r.RecoveryScope
        && p.MaximumChargeGameTicks == r.MaximumChargeGameTicks && p.MaximumChargeWallSeconds == r.MaximumChargeWallSeconds
        && p.NativeChargeFeasibilityRequiresFreshCapture && !p.NewChargingProhibited
        && p.AdditionalVoyagerGameTicks == 0 && p.OriginalJourneyMaximumGameTicks.HasValue
        && p.EffectiveJourneyMaximumGameTicks == p.OriginalJourneyMaximumGameTicks
        && p.SuccessfulFlightWitnessRetiredAtGameTick is null && !p.RemotePrimaryRecoveryAuthorizationUsed
        && !p.FailedRemoteLastExitReauthorizationUsed && !p.CompletedRemoteLastExitContinuationUsed
        && p.ReturnOnlyContinuation is null && !p.OpensSavedWorldOnly && !p.NativeOriginalOutcomeProved;

    public static string? ValidateRequest(PrepareOwnedWorldResumeRequest r)
    {
        if (r.RecoveryMode != Mode || r.UserConfirmedInConversation || r.AcceptUnstoredProgressLoss
            || r.AdditionalVoyagerGameTicks != 0 || r.PriorActionKnownTerminalFailed != true
            || r.PriorActionKnownTerminalCompleted.HasValue || !r.MinimumRecoveryGameTick.HasValue
            || r.MinimumRecoveryGameTick < 0 || r.ExpectedRecoveryGameTick != r.MinimumRecoveryGameTick
            || !string.IsNullOrEmpty(r.PriorConsumedResumeToken) || !string.IsNullOrEmpty(r.PriorSourceFingerprint)
            || !string.IsNullOrEmpty(r.PriorLegacySourceFingerprint) || !string.IsNullOrEmpty(r.PriorIntermediateSourceFingerprint)
            || !string.IsNullOrEmpty(r.ExpectedRemoteRecoveryContextFingerprint)
            || !string.IsNullOrEmpty(r.LegacyLocalMoveUnknownEvidenceJson) || !string.IsNullOrEmpty(r.ExpectedLegacyLocalMoveUnknownEvidenceHash))
            return "failed_landing_exact_no_loss_request_required";
        if (!r.RecoveryDeadlineUtc.HasValue || r.RecoveryScope != VoyagerRecoveryWindowProposal.TaskScope
            || r.MaximumChargeGameTicks < 1 || r.MaximumChargeGameTicks > 18000
            || double.IsNaN(r.MaximumChargeWallSeconds) || double.IsInfinity(r.MaximumChargeWallSeconds)
            || r.MaximumChargeWallSeconds <= 0 || r.MaximumChargeWallSeconds > 300)
            return "failed_landing_finite_window_required";
        return null;
    }

    public static VoyagerFailedLandingRecoveryDecision Evaluate(PrepareOwnedWorldResumeRequest request,
        VoyagerFailedLandingSourceFacts f)
    {
        VoyagerFailedLandingRecoveryDecision Reject(string code) => new() { BlockerCode = code };
        if (ValidateRequest(request) is { } requestError) return Reject(requestError);
        if (!f.FixedLastExit || !f.CompleteSourceAndPrimary || !f.ExactOwnedIdentityAndNativeVersion || !f.Peaceful)
            return Reject("failed_landing_complete_owned_source_required");
        if (!f.ExactOriginalJourneyCheckpointAndJournal || !f.OriginalCheckpointRecoveryRequired
            || f.OriginalCheckpointFlightSucceeded || !f.OriginalOutboundFailureContext
            || !f.ExactSelectedRemotePlanet || !f.CallerAttestsTerminalFailure)
            return Reject("failed_landing_original_failure_context_unproved");
        if (f.SourceGameTick < 0 || f.PrimaryGameTick < 0 || f.CheckpointGameTick < 0 || f.LastAccountedGameTick < 0
            || f.TicketMinimumGameTick < 0 || f.SourceGameTick < f.TicketMinimumGameTick
            || f.SourceGameTick != request.MinimumRecoveryGameTick
            || f.SourceGameTick < f.PrimaryGameTick || f.SourceGameTick != f.LastAccountedGameTick
            || f.LastAccountedGameTick < f.CheckpointGameTick)
            return Reject("failed_landing_current_boundary_not_preserved");
        if (f.SpentGameTicks < 0 || f.SpentGameTicks < (decimal)f.SourceGameTick - f.CheckpointGameTick
            || f.MaximumGameTicks < 7200 || f.MaximumGameTicks > 108000
            || f.ReturnReserveGameTicks < 3600 || f.ReturnReserveGameTicks > f.MaximumGameTicks - 3600)
            return Reject("failed_landing_original_budget_unproved");
        var spent = (decimal)f.SpentGameTicks;
        if (spent > long.MaxValue || spent + f.ReturnReserveGameTicks + 1 + VoyagerLocalWalkReservePolicy.StopGameTicks > f.MaximumGameTicks)
            return Reject("failed_landing_original_return_reserve_insufficient");
        var seconds = (request.RecoveryDeadlineUtc!.Value - f.NowUtc).TotalSeconds;
        if (f.NowUtc == default || f.NowUtc.Offset != TimeSpan.Zero || request.RecoveryDeadlineUtc.Value.Offset != TimeSpan.Zero
            || seconds > 3600 || seconds <= f.ReturnReserveGameTicks / 60d + VoyagerRemoteIdleAdmissionPolicy.StopWallSeconds + 1d / 60d)
            return Reject("failed_landing_new_owner_window_unproved");
        return new() { Proved = true, SavedAccountingAnchorGameTick = f.SourceGameTick, ConservedSpentGameTicks = (long)spent };
    }
}
