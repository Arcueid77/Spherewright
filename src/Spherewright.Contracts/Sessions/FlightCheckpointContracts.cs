using Spherewright.Contracts.Actions;

namespace Spherewright.Contracts.Sessions;

public sealed class PrepareFlightCheckpointReloadRequest
{
    public string ReloadToken { get; set; } = string.Empty;

    public VoyagerReturnOnlyRecoveryRequest? ReturnOnlyTask { get; set; }
}

// Optional, explicit new task after an interrupted RETURN checkpoint. It never
// changes the original journey's accounting, deadline, resources or outcome.
public sealed class VoyagerReturnOnlyRecoveryRequest
{
    public string ExpectedJourneyId { get; set; } = string.Empty;
    public string ExpectedFlightActionId { get; set; } = string.Empty;
    public string ExpectedCheckpointId { get; set; } = string.Empty;
    public long? ExpectedCheckpointGameTick { get; set; }
    public string ExpectedCheckpointFileFingerprint { get; set; } = string.Empty;
    public DateTimeOffset DeadlineUtc { get; set; }
    public long MaximumGameTicks { get; set; }
    public bool AcceptUnstoredProgressLoss { get; set; }
}

public sealed class CommitFlightCheckpointReloadRequest
{
    public string PlanToken { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public bool UserConfirmedInConversation { get; set; }
}

public sealed class PreparedFlightCheckpointReloadPlan
{
    public bool Prepared { get; set; }

    public string PlanToken { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string CheckpointId { get; set; } = string.Empty;

    public int OriginPlanetId { get; set; }

    public int DestinationPlanetId { get; set; }

    public long SavedGameTick { get; set; }

    public bool CommitAllowedNow { get; set; }

    public List<WriteBlocker> CommitBlockers { get; set; } = new List<WriteBlocker>();

    public string CompletionCondition { get; set; } = string.Empty;

    public VoyagerReturnOnlyRecoveryRequest? ReturnOnlyTask { get; set; }
}

public sealed class FlightCheckpointReloadResult
{
    public string ActionId { get; set; } = string.Empty;

    public string CheckpointId { get; set; } = string.Empty;

    public bool Accepted { get; set; }

    public bool IdempotentReplay { get; set; }

    public string State { get; set; } = string.Empty;
}
