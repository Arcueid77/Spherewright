using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Hosting;

namespace Spherewright.Plugin.Game;

// Native object/reference and callback shell only. The linked production Store,
// reload coordinator, idle guard and safety policies make their own decisions.
// No Unity collision, projection, energy generation or Data.Export is simulated.
internal sealed partial class FlightStartTestPlayer
{
    internal object mecha = new();
    internal FlightStartTestPlanet? planetData;
    internal object? factory;
    internal int planetId;
}
internal sealed partial class GameSessionTracker
{
    private bool _checkpointNativeShellBound;
    internal string CurrentNativeGameVersion => Snapshot.GameVersion;
    internal void BindCheckpointNativeWorldFromSnapshotForTest(FlightCheckpointStore store)
    {
        _checkpointNativeShellBound = true;
        _flightCheckpoints = store; _sessionId = SessionId = Snapshot.SessionId;
        _ownedSaveName = OwnedSaveName = Snapshot.SaveName; _gameVersion = Snapshot.GameVersion;
        IsCurrentSessionOwned = Snapshot.OwnedBySpherewright;
        _writeHealth = Snapshot.WriteHealth; // never manufacture healthy admission
        _ownedSaveState = OwnedSaveStates.Saved;
        _lastOwnedSaveGameTick = store.CurrentTicket!.SavedGameTick;
        _ownedData = GameMain.data = new() { gameName = _ownedSaveName!, localLoadedPlanetFactory = new object() };
        GameMain.localPlanet = new() { id = Snapshot.LocalPlanetId!.Value, factory = _ownedData.localLoadedPlanetFactory };
        GameMain.mainPlayer = new() { planetId = GameMain.localPlanet.id, planetData = GameMain.localPlanet, factory = GameMain.localPlanet.factory };
        GameMain.gameTick = Snapshot.GameTick!.Value; GameMain.isPaused = false;
    }
    private void RefreshCheckpointNativeShellForTest()
    {
        if (!_checkpointNativeShellBound) return;
        Snapshot.GameTick = GameMain.gameTick; Snapshot.WriteHealth = _writeHealth;
        Snapshot.WriteQuarantineActionId = _writeQuarantineActionId;
        Snapshot.LastOwnedSaveGameTick = _lastOwnedSaveGameTick; Snapshot.OwnedSaveState = _ownedSaveState;
        RefreshReturnOnlyScopeForTest();
    }
}
internal sealed partial class NormalGameActionCoordinator
{
    public bool VoyagerLocalPrePlayerTickAvailable { get; set; }
    public DateTimeOffset VoyagerLocalPrePlayerObservedAtUtc { get; set; }
    private readonly CheckpointLogicContract _checkpointLogicContract = new();
    internal void ObserveBoundNative501ContractForTest()
    {
        Action<int, int, int> native = _checkpointLogicContract.Handler;
        Action<int, int, int> observer = CheckpointObserverContract;
        VoyagerLocalPrePlayerTickAvailable = NativeTaskObserverOrderGuard.HasNativeBeforeObserver(native + observer,
            _checkpointLogicContract, typeof(CheckpointLogicContract), observer);
        VoyagerLocalPrePlayerObservedAtUtc = DateTimeOffset.UtcNow;
    }
    private static void CheckpointObserverContract(int task, int ordinal, int count) => throw new InvalidOperationException("Metadata only");
    private sealed class CheckpointLogicContract
    {
        internal Action<int, int, int> Handler => OnGameLogicFrame;
        private void OnGameLogicFrame(int task, int ordinal, int count) => throw new InvalidOperationException("Native is not invoked by this fixture");
    }
    private readonly NativeIdleTestActions _actions = new();
    internal void ResetIdleNativeShellForTest() { _voyagerRemoteIdleOwner = null; _voyagerRemoteIdleScene = null; }
    internal CheckpointNativeObservation NativeIdleObservation { get; set; } = new();
    internal void RunNativeIdleGuardForTest()
    { ObserveBoundNative501ContractForTest(); UpdateVoyagerLocalPrePlayerTickOnMainThread(); }
    internal VoyagerLocalWalkReserve ContractMovementCostForTest() => VoyagerLocalWalkReservePolicy.Evaluate(new()
    {
        ModeTransitionsExcluded = NativeIdleObservation.MovementState == "Walk",
        IndependentMechaCosts = Spherewright.Bridge.Core.Tests.NativeMechaBackgroundCostPolicyTests.Idle(),
        WalkSpeed = 12,
        WalkPowerWatts = 1000,
        MiningPowerWatts = 200,
        OtherWorkPowerWatts = 100,
        ReactorConsumptionRatio = 2,
        RtsSpeed = NativeIdleObservation.RtsSpeed,
        ManualSpeed = NativeIdleObservation.ManualSpeed,
        SoftLandingRecover = 1,
        IncludePotentialWaterDrift = true,
        NativeThrustPowerPerAcceleration = 300,
        NativeGravityUpperBound = 50
    });
    private void UpdateVoyagerLocalPrePlayerTickCoreOnMainThread()
    { GuardVoyagerRemoteIdleOnMainThread(); GuardVoyagerRemoteIdleOnMainThread(); }
    private static bool IsVoyagerLocalOrder(ActionRecord a) => a.Plan.VoyagerLocalJourneyId is not null;
    private static bool IsStructuredConstruction(NormalActionPlanPayload p) => p.BlueprintState is not null;
    private VoyagerRemoteIdleAdmissionDecision CaptureVoyagerRemoteIdleAdmission(VoyagerRemoteIdleOwner owner,
        VoyagerJourneyRecord journey, NativeLocalWalkSceneBinding? scene, long spent, long? landed, out NativeLocalWalkSceneBinding? observed,
        int? prospectiveRemotePlanetId = null, Action<VoyagerRemoteIdleAdmissionFacts>? captureFacts = null, bool collectAfterGroundMiss = false,
        Action<NativePlayerGroundContactSnapshot>? captureGround = null, string groundStage = "native_guard", bool collectNaturalContact = false,
        bool reconcilePaused = false)
    {
        observed = scene ?? new(); var n = NativeIdleObservation;
        if (reconcilePaused && (!GameMain.isPaused || n.RtsSpeed != 0 || n.ManualSpeed != 0 || n.PhysicsSpeed != 0))
            return VoyagerRemoteIdleAdmissionPolicy.Blocked("paused_idle_not_stationary", "native_mode");
        if (n.Dirty) { observed = null; return VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_scene_unproved", "scene_capture"); }
        if (n.MovementState != "Walk") return VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_movement_mode_unproved", "native_mode");
        captureGround?.Invoke(new() { CapturedAtGameTick = GameMain.gameTick, CaptureStage = groundStage, CheckSphereHit = n.GroundSphereHit });
        if (n.GroundSphereHit != true) return VoyagerRemoteIdleAdmissionPolicy.Blocked(
            n.GroundSphereHit.HasValue ? "remote_idle_ground_contact_unproved" : "remote_idle_ground_not_observed", "native_mode");
        if (n.Speed is null || n.Speed > .1) return VoyagerRemoteIdleAdmissionPolicy.Blocked("remote_idle_actual_speed_not_at_rest", "walk_at_rest");
        var reserve = VoyagerLocalWalkReservePolicy.Evaluate(new()
        {
            ModeTransitionsExcluded = n.MovementState == "Walk",
            IndependentMechaCosts = Spherewright.Bridge.Core.Tests.NativeMechaBackgroundCostPolicyTests.Idle(),
            WalkSpeed = 12,
            WalkPowerWatts = 1000,
            MiningPowerWatts = 200,
            OtherWorkPowerWatts = 100,
            ReactorConsumptionRatio = 2,
            RtsSpeed = n.RtsSpeed,
            ManualSpeed = n.ManualSpeed,
            SoftLandingRecover = 1,
            IncludePotentialWaterDrift = true,
            NativeThrustPowerPerAcceleration = 300,
            NativeGravityUpperBound = 50
        });
        string? native = n.Dirty ? "remote_idle_scene_dirty" : n.GroundSphereHit is null ? "remote_idle_ground_not_observed"
            : n.GroundSphereHit is false ? "remote_idle_ground_contact_unproved" : n.MovementState != "Walk" ? "remote_idle_movement_mode_unproved"
            : n.Speed is null ? "remote_idle_actual_speed_unknown" : n.Speed > .1 ? "remote_idle_actual_speed_not_at_rest" : null;
        var task = journey.ReturnOnlyTask;
        var f = new VoyagerRemoteIdleAdmissionFacts
        {
            NativeBlockerCode = native,
            MovementReserve = reserve,
            CoreEnergyJoules = n.Core,
            JourneyBudget = new()
            {
                ExactRemoteBinding = VoyagerRemoteIdleBindingMatches(owner, journey),
                NativePlayerIdle = native is null,
                CurrentGameTick = GameMain.gameTick,
                LastLandedGameTick = landed,
                SpentGameTicks = spent,
                MaximumGameTicks = journey.MaximumGameTicks,
                ReturnReserveGameTicks = journey.ReturnReserveGameTicks,
                WorkGameTicks = 1,
                TailGameTicks = 13,
                NowUtc = n.NowOverride ?? DateTimeOffset.UtcNow,
                JourneyDeadlineUtc = journey.DeadlineUtc,
                AvailableLoadedEnergyJoules = n.Loaded,
                LockedReturnFloorJoules = journey.ReturnEnergyFloorJoules,
                WorkPowerUpperWatts = reserve.WorkPowerUpperWatts,
                TailAndNextTickEnergyJoules = reserve.StopEnergyJoules ?? double.NaN,
                NativeWarperCount = n.Warpers,
                OriginalExtraWarperReserve = journey.ExtraWarperReserve
            },
            ReturnOnlyBudget = task is null ? null : new()
            {
                ExactTaskBinding = task.CurrentActionId == owner.ActionId && task.CurrentSessionId == owner.SessionId,
                StartedAtGameTick = task.StartedAtGameTick,
                MaximumGameTicks = task.MaximumGameTicks,
                DeadlineUtc = task.CurrentDeadlineUtc
            }
        };
        captureFacts?.Invoke(f);
        return VoyagerRemoteIdleAdmissionPolicy.Evaluate(f, "energy_and_journey");
    }
    private sealed class NativeIdleTestActions { internal List<ActionRecord> ActiveValues { get; } = new(); }
    private sealed class VoyagerNativeBinding
    {
        internal VoyagerNativeBinding(PausedSaveTestWorld world, FlightStartTestPlayer player)
        { World = world; Player = player; Controller = player.controller; Mecha = player.mecha; }
        internal PausedSaveTestWorld World { get; }
        internal FlightStartTestPlayer Player { get; }
        internal FlightStartTestController Controller { get; }
        internal object Mecha { get; }
    }
    private sealed class NativeLocalWalkSceneBinding { }
    internal sealed partial class NormalActionPlanPayload
    { internal string? VoyagerLocalJourneyId = null; internal NativeIdleTestBlueprint? BlueprintState = null; }
    internal sealed class NativeIdleTestBlueprint { internal NativeIdleTestConstruction? StructuredConstruction = null; }
    internal sealed class NativeIdleTestConstruction { internal string? JourneyId = null; }
}

// Explicit Native contract substitute, never Ready flags or a Unity certificate.
// Ground/Walk/zero-speed/full-core reflect the sanitized post-pause observation;
// loaded inventory, costs and frame progression are synthetic contract inputs.
internal sealed class CheckpointNativeObservation
{
    internal string MovementState = "Walk";
    internal double? Speed = 0, Core = 1_600_000_000, Loaded = 10_000_000_000;
    internal bool? GroundSphereHit = true;
    internal bool Dirty = false;
    internal double RtsSpeed = 0, ManualSpeed = 0;
    internal double? PhysicsSpeed = 0;
    internal int? Warpers = 3;
    internal DateTimeOffset? NowOverride;
}
