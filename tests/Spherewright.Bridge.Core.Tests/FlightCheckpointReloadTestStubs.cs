using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;

namespace Spherewright.Plugin.Game;

// TEST ONLY. Production recovery coordinator/store execute against these copied
// native/session facts. They do not prove Unity adoption, real saves or Windows ACLs.
internal static partial class DSPGame
{
    // Counts entry even when the native substitute throws before a load completes.
    internal static int DispatchCalls;
}

internal sealed partial class GameSessionTracker
{
    internal string? FlightCheckpointAdoptionError { get; set; }
    internal FlightCheckpointTicket? ExpectedFlightCheckpoint { get; private set; }
    internal int FlightExpectationCalls { get; private set; }
    internal int FlightExpectationCancellationCalls { get; private set; }
    internal int QuarantineCalls { get; private set; }
    internal int ClearQuarantineCalls { get; private set; }
    internal string? QuarantineReason { get; private set; }

    internal void ExpectNextSessionToBeLoadedFromFlightCheckpoint(
        FlightCheckpointTicket ticket, OwnedSaveRecoveryLease checkpointLease)
    {
        if (ExpectedFlightCheckpoint is not null) throw new InvalidOperationException("Test expectation is already armed.");
        ExpectedFlightCheckpoint = ticket;
        SourceLease = checkpointLease;
        FlightExpectationCalls++;
        FlightCheckpointAdoptionError = null;
    }

    internal void CancelExpectedFlightCheckpointSession()
    {
        FlightExpectationCancellationCalls++;
        FinishExpectedFlightCheckpointAdoptionForTest();
    }

    internal void FinishExpectedFlightCheckpointAdoptionForTest()
    {
        ExpectedFlightCheckpoint = null;
        SourceLease?.Dispose();
        SourceLease = null;
    }

    private void DisposeFlightCheckpointExpectation() => ExpectedFlightCheckpoint = null;

    internal void QuarantineWritesOnMainThread(string actionId, string reason)
    {
        QuarantineCalls++;
        if (!IsCurrentSessionOwned || Snapshot.WriteHealth == WriteHealthStates.Quarantined) return;
        Snapshot.WriteHealth = WriteHealthStates.Quarantined;
        Snapshot.WriteQuarantineActionId = actionId;
        Snapshot.WritesAllowed = false;
        QuarantineReason = reason;
    }

    internal bool TryClearQuarantineOnMainThread(string expectedActionId, string expectedReason, out string? rejection)
    {
        ClearQuarantineCalls++;
        rejection = null;
        if (!IsCurrentSessionOwned || Snapshot.WriteHealth != WriteHealthStates.Quarantined
            || !string.Equals(Snapshot.WriteQuarantineActionId, expectedActionId, StringComparison.Ordinal)
            || !string.Equals(QuarantineReason, expectedReason, StringComparison.Ordinal))
        {
            rejection = "Test quarantine identity or reason does not match the original action.";
            return false;
        }
        Snapshot.WriteHealth = WriteHealthStates.Healthy;
        Snapshot.WriteQuarantineActionId = null;
        Snapshot.WritesAllowed = true;
        QuarantineReason = null;
        return true;
    }
}

internal sealed class NormalGameActionCoordinator
{
    internal bool ReloadAllowed { get; set; } = true;
    internal bool HasFailedFlight { get; set; } = true;
    internal string? FailedFlightCheckpointId { get; set; }
    internal Action? BeforeCanReload { get; set; }
    internal int ReloadStartingCalls { get; private set; }
    internal string? ReloadStartingCheckpointId { get; private set; }

    internal bool CanReloadFlightCheckpointOnMainThread(FlightCheckpointTicket ticket, out string rejection)
    {
        BeforeCanReload?.Invoke();
        rejection = ReloadAllowed ? string.Empty : "A synthetic unrelated action still owns the write channel.";
        return ReloadAllowed;
    }

    internal bool HasRecoveryRequiredFlightOnMainThread(string checkpointId) =>
        HasFailedFlight && string.Equals(checkpointId, FailedFlightCheckpointId, StringComparison.Ordinal);

    internal void NotifyFlightCheckpointReloadStartingOnMainThread(FlightCheckpointTicket ticket)
    {
        ReloadStartingCalls++;
        ReloadStartingCheckpointId = ticket.CheckpointId;
    }
}
