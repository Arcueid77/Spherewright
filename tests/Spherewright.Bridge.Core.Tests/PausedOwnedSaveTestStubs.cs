using BepInEx.Logging;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;

namespace UnityEngine { internal static class Time { internal static int frameCount; } }

namespace Spherewright.Plugin.Game
{
    // TEST ONLY. Native flags/render boundaries and Data.Export are substituted.
    // Actual production capture, commit access, resave gate, Save/readback and
    // Store/lease/ticket operations are linked. This cannot certify Unity timing.
    internal sealed class PausedSaveTestWorld
    { internal object? localLoadedPlanetFactory; internal PausedSaveTestDescriptor gameDesc = new(); internal string gameName = ""; }
    internal sealed class PausedSaveTestDescriptor { internal bool isPeaceMode = true; }
    internal static partial class GameMain
    {
        internal static PausedSaveTestWorld? data;
        internal static bool isRunning = true;
    }
    internal sealed partial class NormalGameActionCoordinator
    {
        internal static BridgeError? SaveAccessForTest(SessionState session, PausedOwnedSaveBinding? binding)
            => ValidateCommitCommon(session, new NormalActionPlanPayload { ActionKind = NormalActionKinds.Save,
                SessionId = session.SessionId!, PlanetId = session.LocalPlanetId!.Value, SavePreservation = binding },
                new CommitNormalActionRequest { SessionId = session.SessionId!, PlanetId = session.LocalPlanetId!.Value });
        internal sealed partial class NormalActionPlanPayload
        {
            internal string ActionKind = "";
            internal string SessionId = "";
            internal PausedOwnedSaveBinding? SavePreservation;
        }
    }
    internal sealed partial class GameSessionTracker
    {
        private PausedSaveTestWorld? _ownedData;
        private string? _sessionId, _ownedSaveName, _resumeAdoptionError;
        private string _gameVersion = "", _ownedSaveState = "", _writeHealth = "";
        private bool _writesConfigured = true;
        private string? _writeQuarantineActionId, _writeQuarantineReason, _ownedSaveError;
        private long _revision;
        private long? _lastOwnedSaveGameTick;
        private OwnedWorldResumeTicket? _pendingJournalResumeTicket;
        private OwnedSaveRecoveryLease? _resumeSourceLease => SourceLease;
        private OwnedSaveRecoveryLease? _resumePrimaryLease => PrimaryLease;
        private IDisposable? _reauthorizationJournalLease => JournalLease;
        private OwnedSaveRecoveryLease? _flightCheckpointSourceLease;
        private FlightCheckpointStore _flightCheckpoints = null!;
        private OwnedWorldResumeTicketStore _resumeTickets = null!;
        private ManualLogSource _logger = new();
        private VoyagerRemoteResumeAdoption? _remoteResumeAdoption;
        private Func<VoyagerRemoteResumeAdoption, string?>? _remoteResumeAdmissionGuard = null;
        internal bool HasPendingRemoteResumeAdoption => _pendingRemoteResumeEvidence is not null && !_remoteResumeAdmitted;
        private OwnedWorldGameplayJournalCheckpoint CaptureGameplayJournalCheckpointOnMainThread() =>
            Spherewright.Plugin.Transport.PluginJson.Deserialize<OwnedWorldGameplayJournalCheckpoint>(RemoteEvidence!.JournalJson)!;

        internal void BindPausedSaveShell(string session, string identity, string version, string resumeAction,
            FlightCheckpointStore checkpoints, OwnedWorldResumeTicketStore tickets, bool completed)
        {
            _sessionId = SessionId = session; _ownedSaveName = OwnedSaveName = identity; _gameVersion = version;
            IsCurrentSessionOwned = true; _revision = 9; _ownedSaveState = OwnedSaveStates.Saved;
            _lastOwnedSaveGameTick = GameMain.gameTick - 10; _writeHealth = WriteHealthStates.Healthy;
            _flightCheckpoints = checkpoints; _resumeTickets = tickets;
            _ownedData = GameMain.data = new() { localLoadedPlanetFactory = new object(), gameName = identity };
            GameMain.localPlanet = new() { id = 201, factory = _ownedData.localLoadedPlanetFactory };
            _remoteResumeAdoption = new() { World = _ownedData, SessionId = session, ResumeActionId = resumeAction };
            if (completed) CompleteContactForTest(null);
            Snapshot.OwnedBySpherewright = true; Snapshot.SaveName = identity; Snapshot.GameLoaded = true;
            Snapshot.SessionId = session; Snapshot.LocalPlanetId = 201; Snapshot.GameVersion = version;
        }

        private void UpdatePausedSaveQuarantineShell(string actionId, string reason)
        {
            _writeHealth = Snapshot.WriteHealth = WriteHealthStates.Quarantined;
            _writeQuarantineActionId = Snapshot.WriteQuarantineActionId = actionId;
            _writeQuarantineReason = reason;
            Snapshot.WritesAllowed = false;
            Snapshot.WriteBlockers = new() { new() { Code = BridgeErrorCodes.WriteSubsystemQuarantined, Message = reason } };
        }
        internal void BreakPausedSaveShell(string fault)
        {
            switch (fault)
            {
                case "adoption_failure": _resumeAdoptionError = "synthetic_original_failure"; break;
                case "journal_pending": _pendingJournalResumeTicket = new(); break;
                case "load_pending": _flightCheckpointSourceLease = OwnedSaveRecoveryLease.Open(GameSave.SavePath(_ownedSaveName!), _ownedSaveName!); break;
                case "newer_saved": _lastOwnedSaveGameTick = GameMain.gameTick + 1; break;
                case "unowned": IsCurrentSessionOwned = false; break;
            }
        }
        internal long? LastPreservedTick => _lastOwnedSaveGameTick;
        internal string PreservationWriteHealth => _writeHealth;
        internal void DisablePausedSaveWrites() => _writesConfigured = false;
        internal void SetPausedSaveCheckpoints(FlightCheckpointStore checkpoints) => _flightCheckpoints = checkpoints;
    }
}
