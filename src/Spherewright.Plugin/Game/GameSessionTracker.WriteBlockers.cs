using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Bridge.Core.Safety;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    private List<WriteBlocker> CreateWriteBlockers(string peacefulState)
    {
        var blockers = new List<WriteBlocker>();
        if (_flightCheckpoints.VoyagerJourney?.ReturnOnlyScopeActive == true)
            blockers.Add(new WriteBlocker { Code = VoyagerReturnOnlyRecoveryPolicy.ScopeBlocker,
                Message = VoyagerReturnOnlyRecoveryPolicy.ScopeMessage });
        if (!string.IsNullOrEmpty(_flightCheckpoints.VoyagerJourneyLoadError))
            blockers.Add(new WriteBlocker { Code = BridgeErrorCodes.WriteSubsystemQuarantined,
                Message = _flightCheckpoints.VoyagerJourneyLoadError! });
        if (_flightCheckpoints.HasUnreconciledReload)
            blockers.Add(new WriteBlocker { Code = BridgeErrorCodes.ServerBusy,
                Message = "The original checkpoint reload is unresolved; poll its exact action before any new write." });
        if (string.Equals(_writeHealth, WriteHealthStates.Quarantined, StringComparison.Ordinal))
        {
            blockers.Add(new WriteBlocker
            {
                Code = BridgeErrorCodes.WriteSubsystemQuarantined,
                Message = _writeQuarantineReason ?? "The current session write subsystem is quarantined.",
            });
        }
        if (_pendingJournalResumeTicket is not null)
        {
            blockers.Add(new WriteBlocker
            {
                Code = BridgeErrorCodes.BridgeNotReady,
                Message = "Writes are blocked until the protected gameplay journal proves the resume ticket's durable checkpoint.",
            });
        }
        if (HasPendingRemoteResumeAdoption)
            blockers.Add(new WriteBlocker { Code = BridgeErrorCodes.BridgeNotReady,
                Message = "The exact remote LastExit continuation awaits its first verified Native pre-player idle handoff; writes and primary resave remain blocked." });
        if (!_writesConfigured)
        {
            blockers.Add(new WriteBlocker
            {
                Code = BridgeErrorCodes.WritesDisabled,
                Message = "Writes are disabled by configuration.",
            });
        }

        if (string.Equals(peacefulState, PeacefulModeStates.Unknown, StringComparison.Ordinal))
        {
            blockers.Add(new WriteBlocker
            {
                Code = BridgeErrorCodes.PeacefulModeUnknown,
                Message = "Peaceful mode could not be confirmed.",
            });
        }
        else if (!string.Equals(peacefulState, PeacefulModeStates.ConfirmedPeaceful, StringComparison.Ordinal))
        {
            blockers.Add(new WriteBlocker
            {
                Code = BridgeErrorCodes.PeacefulModeRequired,
                Message = "M0 writes require a peaceful world.",
            });
        }

        return blockers;
    }
}
