namespace Spherewright.Contracts.Sessions;

public static class OwnedWorldResumeModes
{
    public const string Default = "default";
    public const string VerifiedNewerLastExit = "verified_newer_lastexit";
    public const string ReauthorizeExpiredPrimary = "reauthorize_expired_primary";
    public const string ReauthorizeExpiredAutosave0 = "reauthorize_expired_autosave0";
    public const string ReauthorizeFixedAutosave0 = "reauthorize_fixed_autosave0";
    public const string ReauthorizeVersionChangedPrimary = "reauthorize_version_changed_primary";
    public const string ReauthorizeFailedRemoteLastExit = "reauthorize_failed_remote_last_exit";
    public const string ContinueCompletedRemoteLastExit = "continue_completed_remote_last_exit";
    public const string OpenSavedPrimary = "open_saved_primary";
}

public sealed class PrepareOwnedWorldResumeRequest
{
    public string ResumeToken { get; set; } = string.Empty;

    public string RecoveryMode { get; set; } = OwnedWorldResumeModes.Default;

    public bool UserConfirmedInConversation { get; set; }

    /// <summary>Only an exact disclosed saved-primary rollback; never an automatic fallback.</summary>
    public bool AcceptUnstoredProgressLoss { get; set; }

    public long? MinimumRecoveryGameTick { get; set; }

    public long? ExpectedRecoveryGameTick { get; set; }
    public DateTimeOffset? RecoveryDeadlineUtc { get; set; }
    public string RecoveryScope { get; set; } = string.Empty;
    public long MaximumChargeGameTicks { get; set; }
    public double MaximumChargeWallSeconds { get; set; }
    public string PriorConsumedResumeToken { get; set; } = string.Empty;
    public string PriorSourceFingerprint { get; set; } = string.Empty;
    public string PriorLegacySourceFingerprint { get; set; } = string.Empty;
    public string PriorIntermediateSourceFingerprint { get; set; } = string.Empty;
    public bool? PriorActionKnownTerminalFailed { get; set; }
    public bool? PriorActionKnownTerminalCompleted { get; set; }
    public string ExpectedRemoteRecoveryContextFingerprint { get; set; } = string.Empty;
    public string LegacyLocalMoveUnknownEvidenceJson { get; set; } = string.Empty;
    public string ExpectedLegacyLocalMoveUnknownEvidenceHash { get; set; } = string.Empty;
}

public sealed class CommitOwnedWorldResumeRequest
{
    public string PlanToken { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public bool UserConfirmedInConversation { get; set; }

    public string ConfirmationDigest { get; set; } = string.Empty;
}

public sealed class PreparedOwnedWorldResumePlan
{
    public bool Prepared { get; set; }

    public string PlanToken { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public int ExpectedPlanetId { get; set; }

    public long MinimumGameTick { get; set; }

    public string RecoveryMode { get; set; } = OwnedWorldResumeModes.Default;

    public int RecoveryEvidenceVersion { get; set; }
    public DateTimeOffset? OriginalJourneyDeadlineUtc { get; set; }
    public DateTimeOffset? EffectiveRecoveryDeadlineUtc { get; set; }
    public string RecoveryScope { get; set; } = string.Empty;
    public long MaximumChargeGameTicks { get; set; }
    public double MaximumChargeWallSeconds { get; set; }
    public bool NativeChargeFeasibilityRequiresFreshCapture { get; set; }
    public bool PriorFailedResumeProvenanceUsed { get; set; }
    public bool PriorFailedResumeV2ProvenanceUsed { get; set; }
    public bool PriorFailedResumeV3ProvenanceUsed { get; set; }
    public bool PriorActionTerminalFailureAttested { get; set; }
    public bool FailedRemoteLastExitReauthorizationUsed { get; set; }
    public bool CompletedRemoteLastExitContinuationUsed { get; set; }
    public bool PriorActionTerminalCompletionAttested { get; set; }
    public bool NewChargingProhibited { get; set; }
    public bool OpensSavedWorldOnly { get; set; }
    public bool PreviousAutomationRemainsStopped { get; set; }
    public long? DiscardedUnstoredProgressGameTicks { get; set; }
    public DateTimeOffset? InheritedWindowConfirmedAtUtc { get; set; }
    public string RemoteRecoveryContextFingerprint { get; set; } = string.Empty;
    public bool LegacyLocalMoveUnknownProvenanceUsed { get; set; }
    public string LegacyLocalMoveUnknownEvidenceHash { get; set; } = string.Empty;
    public bool LegacyLocalMoveUnknownKindCallerAttested { get; set; }
    public bool NativeOriginalOutcomeProved => false;

    public long? CandidateGameTick { get; set; }
    /// <summary>Covered historical flight witness only; never a checkpoint-load capability.</summary>
    public long? SuccessfulFlightWitnessRetiredAtGameTick { get; set; }

    public bool ExactEmbeddedIdentityVerified { get; set; }

    public bool UserConfirmationRequired { get; set; }

    public string SourceGameVersion { get; set; } = string.Empty;

    public string TargetGameVersion { get; set; } = string.Empty;

    public string ConfirmationPrompt { get; set; } = string.Empty;

    public string ConfirmationDigest { get; set; } = string.Empty;

    public bool CommitAllowedNow { get; set; }

    public List<WriteBlocker> CommitBlockers { get; set; } = new List<WriteBlocker>();

    public string CompletionCondition { get; set; } = string.Empty;
}

public sealed class OwnedWorldResumeResult
{
    public string ActionId { get; set; } = string.Empty;

    public bool Accepted { get; set; }

    public bool IdempotentReplay { get; set; }

    public string State { get; set; } = string.Empty;
}
