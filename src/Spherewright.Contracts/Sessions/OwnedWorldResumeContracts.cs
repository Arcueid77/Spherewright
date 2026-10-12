namespace Spherewright.Contracts.Sessions;

public static class OwnedWorldResumeModes
{
    public const string Default = "default";
    public const string VerifiedNewerLastExit = "verified_newer_lastexit";
    public const string ReauthorizeExpiredPrimary = "reauthorize_expired_primary";
    public const string ReauthorizeExpiredAutosave0 = "reauthorize_expired_autosave0";
    public const string ReauthorizeFixedAutosave0 = "reauthorize_fixed_autosave0";
    public const string ReauthorizeVersionChangedPrimary = "reauthorize_version_changed_primary";
}

public sealed class PrepareOwnedWorldResumeRequest
{
    public string ResumeToken { get; set; } = string.Empty;

    public string RecoveryMode { get; set; } = OwnedWorldResumeModes.Default;

    public bool UserConfirmedInConversation { get; set; }

    public long? MinimumRecoveryGameTick { get; set; }

    public long? ExpectedRecoveryGameTick { get; set; }
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

    public long? CandidateGameTick { get; set; }

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
