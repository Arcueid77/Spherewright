using System.Diagnostics;
using BepInEx.Logging;
using Spherewright.Bridge.Core.Abstractions;
using Spherewright.Contracts.Protocol;
using Spherewright.Plugin.Bootstrap;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.RuntimeDescriptor;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.Hosting;

internal sealed class SpherewrightBridgeHost : IDisposable
{
    private readonly SpherewrightConfiguration _configuration;
    private readonly ManualLogSource _logger;
    private readonly GameVersionSnapshotProvider _gameVersionProvider;
    private readonly GameSessionTracker _sessionTracker;
    private readonly OverseerLogisticsProgressStore _overseerLogisticsProgressStore;
    private readonly GameplayJournalManager _gameplayJournalManager;
    private readonly NormalGameActionCoordinator _normalActionCoordinator;
    private readonly ResearchResultAutoAcknowledger _researchResultAutoAcknowledger;
    private readonly BridgeStatusSnapshotProvider _statusProvider;
    private readonly BoundedMainThreadDispatcher _dispatcher;
    private readonly RuntimeDescriptorPublisher _descriptorPublisher;
    private readonly NamedPipeBridgeServer _pipeServer;
    private readonly BridgeRuntimeDescriptor _descriptor;
    private bool _started;
    private bool _pumpLogged;
    private int _framesSinceVersionRefresh;
    private GameLogic? _voyagerTickLogic;
    private Action? _voyagerTickHandler;
    private readonly int _unityThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
    private bool _voyagerStopping;
    private bool _gameEndHooked;
    private GameLogic? _localPrePlayerLogic;
    private GameData? _localPrePlayerWorld;
    private ThreadManager? _localPrePlayerManager;
    private ThreadManager.TaskFunc? _localPrePlayerHandler;

    private SpherewrightBridgeHost(
        SpherewrightConfiguration configuration,
        ManualLogSource logger,
        GameVersionSnapshotProvider gameVersionProvider,
        GameSessionTracker sessionTracker,
        OverseerLogisticsProgressStore overseerLogisticsProgressStore,
        GameplayJournalManager gameplayJournalManager,
        NormalGameActionCoordinator normalActionCoordinator,
        ResearchResultAutoAcknowledger researchResultAutoAcknowledger,
        BridgeStatusSnapshotProvider statusProvider,
        BoundedMainThreadDispatcher dispatcher,
        RuntimeDescriptorPublisher descriptorPublisher,
        NamedPipeBridgeServer pipeServer,
        BridgeRuntimeDescriptor descriptor)
    {
        _configuration = configuration;
        _logger = logger;
        _gameVersionProvider = gameVersionProvider;
        _sessionTracker = sessionTracker;
        _overseerLogisticsProgressStore = overseerLogisticsProgressStore;
        _gameplayJournalManager = gameplayJournalManager;
        _normalActionCoordinator = normalActionCoordinator;
        _researchResultAutoAcknowledger = researchResultAutoAcknowledger;
        _statusProvider = statusProvider;
        _dispatcher = dispatcher;
        _descriptorPublisher = descriptorPublisher;
        _pipeServer = pipeServer;
        _descriptor = descriptor;
        _sessionTracker.SetRemoteResumeLoadCompleteGuard(OnRemoteResumeNativeLoadComplete);
    }

    public static SpherewrightBridgeHost Create(
        SpherewrightConfiguration configuration,
        ManualLogSource logger,
        string pluginVersion)
    {
        var identity = BridgeIdentity.Create(configuration.PipeNamePrefix);
        var versionProvider = new GameVersionSnapshotProvider();
        var gameVersion = versionProvider.CaptureOnMainThread();
        var statusProvider = new BridgeStatusSnapshotProvider(
            identity.BridgeInstanceId,
            pluginVersion,
            gameVersion,
            configuration.AllowWrites);
        var dispatcher = new BoundedMainThreadDispatcher(configuration.MaxMainThreadQueue);
        var resumeTickets = new OwnedWorldResumeTicketStore(
            configuration.RuntimeDescriptorDirectory,
            identity.BridgeInstanceId,
            gameVersion,
            logger);
        var flightCheckpoints = new FlightCheckpointStore(identity.BridgeInstanceId, gameVersion, logger);
        var sessionTracker = new GameSessionTracker(
            configuration.AllowWrites,
            configuration.AllowUserSaveImport,
            gameVersion,
            resumeTickets,
            flightCheckpoints,
            logger);
        var userSaveImportCoordinator = new UserSaveImportCoordinator(
            configuration.AllowUserSaveImport,
            configuration.AllowWrites,
            configuration.PlanTokenLifetimeSeconds,
            configuration.IdempotencyRetentionMinutes,
            configuration.MaxIdempotencyEntriesPerSession,
            sessionTracker);
        var overseerLogisticsProgressStore = new OverseerLogisticsProgressStore(
            configuration.RuntimeDescriptorDirectory,
            gameVersion,
            sessionTracker,
            logger);
        var gameStateReader = new GameStateReader(sessionTracker, overseerLogisticsProgressStore,
            new GovernorDeclarationStore(configuration.RuntimeDescriptorDirectory, gameVersion, sessionTracker, logger));
        gameStateReader.VoyagerCheckpoints = flightCheckpoints;
        var gameplayJournalManager = new GameplayJournalManager(
            configuration.RuntimeDescriptorDirectory,
            gameVersion,
            sessionTracker,
            logger);
        sessionTracker.SetGameplayJournalCheckpointProvider(
            gameplayJournalManager.CaptureResumeCheckpointOnMainThread);
        var normalActionCoordinator = new NormalGameActionCoordinator(
            configuration.PlanTokenLifetimeSeconds,
            configuration.IdempotencyRetentionMinutes,
            configuration.MaxIdempotencyEntriesPerSession,
            sessionTracker,
            gameStateReader,
            flightCheckpoints,
            new BlueprintBuildStore(configuration.RuntimeDescriptorDirectory, gameVersion, sessionTracker, logger, () =>
            {
                var journal = gameplayJournalManager.CaptureOnMainThread(sessionTracker.SessionId);
                return journal.Success ? journal.Value : null;
            }));
        var researchResultAutoAcknowledger = new ResearchResultAutoAcknowledger(
            configuration.AutoAcknowledgeResearchResults,
            logger);
        sessionTracker.SetRemoteResumeAdmissionGuard(normalActionCoordinator.AdmitVerifiedVoyagerRemoteResumeOnMainThread,
            normalActionCoordinator.PreflightVerifiedVoyagerRemoteResumeOnMainThread,
            normalActionCoordinator.ObserveVerifiedRemoteResumeUniverseOnMainThread);
        var testWorldCoordinator = new TestWorldCoordinator(
            configuration.AllowWrites,
            configuration.PlanTokenLifetimeSeconds,
            configuration.IdempotencyRetentionMinutes,
            configuration.MaxIdempotencyEntriesPerSession,
            sessionTracker);
        var ownedWorldResumeCoordinator = new OwnedWorldResumeCoordinator(
            configuration.AllowWrites,
            configuration.PlanTokenLifetimeSeconds,
            configuration.IdempotencyRetentionMinutes,
            configuration.MaxIdempotencyEntriesPerSession,
            sessionTracker,
            resumeTickets);
        var flightCheckpointReloadCoordinator = new FlightCheckpointReloadCoordinator(
            configuration.AllowWrites,
            configuration.PlanTokenLifetimeSeconds,
            configuration.IdempotencyRetentionMinutes,
            configuration.MaxIdempotencyEntriesPerSession,
            sessionTracker,
            flightCheckpoints,
            normalActionCoordinator);
        var descriptorPublisher = new RuntimeDescriptorPublisher(configuration.RuntimeDescriptorDirectory, logger);
        var pipeServer = new NamedPipeBridgeServer(
            identity,
            pluginVersion,
            configuration.MaxFrameBytes,
            configuration.ReadRequestTimeoutSeconds,
            statusProvider,
            dispatcher,
            gameStateReader,
            gameplayJournalManager,
            testWorldCoordinator,
            userSaveImportCoordinator,
            ownedWorldResumeCoordinator,
            flightCheckpointReloadCoordinator,
            normalActionCoordinator,
            logger);
        var descriptor = new BridgeRuntimeDescriptor
        {
            ProcessId = Process.GetCurrentProcess().Id,
            BridgeInstanceId = identity.BridgeInstanceId,
            PipeName = identity.PipeName,
            AuthToken = identity.AuthToken,
            ProtocolVersion = ProtocolConstants.CurrentVersion,
            PluginVersion = pluginVersion,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        return new SpherewrightBridgeHost(
            configuration,
            logger,
            versionProvider,
            sessionTracker,
            overseerLogisticsProgressStore,
            gameplayJournalManager,
            normalActionCoordinator,
            researchResultAutoAcknowledger,
            statusProvider,
            dispatcher,
            descriptorPublisher,
            pipeServer,
            descriptor);
    }

    public void Start()
    {
        if (_started)
        {
            throw new InvalidOperationException("Spherewright bridge host is already started.");
        }

        try
        {
            _pipeServer.Start();
            _descriptorPublisher.Publish(_descriptor);
            GameMain.onGameEnded += OnNativeGameEnded;
            _gameEndHooked = true;
            _started = true;
        }
        catch
        {
            _pipeServer.Dispose();
            _descriptorPublisher.Dispose();
            throw;
        }
    }

    public void PumpMainThread()
    {
        if (!_started)
        {
            return;
        }

        RefreshVoyagerNativeTickBinding();
        RefreshVoyagerLocalPrePlayerBinding();
        _sessionTracker.UpdateOnMainThread();
        _statusProvider.UpdateGameLoadedOnMainThread(_sessionTracker.GameLoaded);
        _gameplayJournalManager.UpdateOnMainThread();
        _normalActionCoordinator.UpdateOnMainThread();
        _researchResultAutoAcknowledger.UpdateOnMainThread();

        _dispatcher.Pump(
            _configuration.MaxRequestsPerFrame,
            TimeSpan.FromMilliseconds(_configuration.FrameBudgetMs));

        _framesSinceVersionRefresh++;
        if (_framesSinceVersionRefresh >= 120)
        {
            _framesSinceVersionRefresh = 0;
            _statusProvider.UpdateGameVersionOnMainThread(_gameVersionProvider.CaptureOnMainThread());
        }

        if (!_pumpLogged)
        {
            _pumpLogged = true;
            _logger.LogInfo("Spherewright main-thread pump active");
        }
    }

    public void Dispose()
    {
        StopVoyagerLocalPrePlayerControl();
        StopVoyagerNativeControl();
        if (_gameEndHooked)
        {
            GameMain.onGameEnded -= OnNativeGameEnded;
            _gameEndHooked = false;
        }
        _sessionTracker.Dispose();
        if (!_started)
        {
            return;
        }

        _started = false;
        _overseerLogisticsProgressStore.Dispose();
        _gameplayJournalManager.Dispose();
        _pipeServer.Dispose();
        _descriptorPublisher.Dispose();
        _dispatcher.Dispose();
    }

    private void RefreshVoyagerNativeTickBinding()
    {
        var next = GameMain.isRunning ? GameMain.logic : null;
        if (ReferenceEquals(next, _voyagerTickLogic)) return;
        StopVoyagerNativeControl();
        _voyagerStopping = false;
        if (next is null) return;
        var captured = next;
        _voyagerTickHandler = () => OnVoyagerNativePlayerTick(captured);
        _voyagerTickLogic = captured;
        captured.onFactoryFrameBegin += _voyagerTickHandler;
    }

    private void OnVoyagerNativePlayerTick(GameLogic source)
    {
        if (_voyagerStopping || System.Threading.Thread.CurrentThread.ManagedThreadId != _unityThreadId
            || !ReferenceEquals(source, GameMain.logic) || !ReferenceEquals(source.data, GameMain.data)) return;
        // The event follows native Player.GameTick in each logic frame, including
        // multiple FixedUpdates in one rendered frame. Do not pump RPCs or save here.
        try
        {
            _normalActionCoordinator.VoyagerNativeTickAvailable = true;
            _normalActionCoordinator.VoyagerLastNativeTickObservedAtUtc = DateTimeOffset.UtcNow;
            _normalActionCoordinator.UpdateVoyagerNativeTickOnMainThread();
            _normalActionCoordinator.UpdateStructuredConstructionNativeTickOnMainThread();
        }
        catch (Exception exception)
        {
            // Never let our event subscriber interrupt native factory/game ticks.
            StopVoyagerNativeControl();
            LogVoyagerStopFailure(exception);
        }
    }

    private void OnNativeGameEnded()
    {
        StopVoyagerLocalPrePlayerControl();
        StopVoyagerNativeControl();
    }

    private string? OnRemoteResumeNativeLoadComplete()
    {
        if (!_started || System.Threading.Thread.CurrentThread.ManagedThreadId != _unityThreadId)
            return "remote_resume_loader_main_thread_unproved";
        RefreshVoyagerNativeTickBinding();
        RefreshVoyagerLocalPrePlayerBinding();
        var logic = GameMain.logic;
        var manager = logic?.threadController?.threadManager;
        if (logic is null || manager is null || _localPrePlayerHandler is null
            || !ReferenceEquals(logic, _localPrePlayerLogic) || !ReferenceEquals(GameMain.data, _localPrePlayerWorld)
            || !ReferenceEquals(manager, _localPrePlayerManager)
            || !NativeTaskObserverOrderGuard.HasNativeBeforeObserver(manager.OnTaskLogic, logic, typeof(GameLogic), _localPrePlayerHandler))
            return "remote_resume_loader_pre_player_binding_unproved";
        // Begin/InitLogic completed, but no Native601 has run in this callback.
        // Do not set Available, publish an idle owner, release leases or resave.
        _sessionTracker.UpdateOnMainThread();
        _gameplayJournalManager.UpdateOnMainThread();
        return _sessionTracker.PreflightRemoteResumeBeforeFirstPlayerOnMainThread();
    }

    private void RefreshVoyagerLocalPrePlayerBinding()
    {
        var logic = GameMain.isRunning ? GameMain.logic : null;
        var manager = logic?.threadController?.threadManager;
        var world = GameMain.data;
        if (ReferenceEquals(logic, _localPrePlayerLogic) && ReferenceEquals(manager, _localPrePlayerManager)
            && ReferenceEquals(world, _localPrePlayerWorld) && logic is not null && manager is not null && _localPrePlayerHandler is not null)
        {
            if (NativeTaskObserverOrderGuard.HasNativeBeforeObserver(manager.OnTaskLogic, logic, typeof(GameLogic), _localPrePlayerHandler)) return;
            StopVoyagerLocalPrePlayerControl();
            return; // Lost/reordered Native authority is an interruption, never a fast-path success.
        }
        StopVoyagerLocalPrePlayerControl();
        // GameLogic.Init publishes its Native delegate first. Append only our
        // observer; never replace the Native callback or another subscriber.
        if (logic is null || manager is null || world is null || !ReferenceEquals(logic.data, world)
            || !NativeTaskObserverOrderGuard.HasUniqueNative(manager.OnTaskLogic, logic, typeof(GameLogic))) return;
        var source = logic; var sourceManager = manager; var sourceWorld = world;
        ThreadManager.TaskFunc? sourceObserver = null;
        sourceObserver = (task, ordinal, count) => OnVoyagerLocalPrePlayerTask(source, sourceManager, sourceWorld, sourceObserver!, task, ordinal);
        _localPrePlayerHandler = sourceObserver;
        _localPrePlayerLogic = source; _localPrePlayerManager = sourceManager; _localPrePlayerWorld = sourceWorld;
        manager.OnTaskLogic += _localPrePlayerHandler;
        if (!NativeTaskObserverOrderGuard.HasNativeBeforeObserver(manager.OnTaskLogic, source, typeof(GameLogic), _localPrePlayerHandler))
            StopVoyagerLocalPrePlayerControl();
    }

    private void OnVoyagerLocalPrePlayerTask(GameLogic logic, ThreadManager manager, GameData world,
        ThreadManager.TaskFunc sourceObserver, int task, int ordinal)
    {
        if ((task != (int)EGameLogicTask.UniverseSimulator && task != (int)EGameLogicTask.Physics
            && task != (int)EGameLogicTask.FactoryConstructionSystem) || ordinal != -1) return;
        if (!ReferenceEquals(sourceObserver, _localPrePlayerHandler)) return; // A retired observer cannot borrow a newer binding's proof.
        if (System.Threading.Thread.CurrentThread.ManagedThreadId != _unityThreadId)
        { _normalActionCoordinator.VoyagerLocalPrePlayerTickAvailable = false; return; }
        // Native task501 Physics has returned. Ascending synchronized tasks put
        // the next601 Player after this observer, once per actual game tick,
        // including several logic ticks in a single rendered frame.
        try
        {
            if (!ReferenceEquals(GameMain.logic, logic) || !ReferenceEquals(GameMain.data, world)
                || !ReferenceEquals(logic.data, world) || !ReferenceEquals(logic.threadController?.threadManager, manager)
                || !ReferenceEquals(_localPrePlayerManager, manager) || _localPrePlayerHandler is null
                || !NativeTaskObserverOrderGuard.HasNativeBeforeObserver(manager.OnTaskLogic, logic, typeof(GameLogic), _localPrePlayerHandler))
            { StopVoyagerLocalPrePlayerControl(); return; }
            if (task == (int)EGameLogicTask.UniverseSimulator)
            {
                // Native151 returned; this is not a501 observation or permission
                // to publish an owner/resave. FailurePause still pays this601.
                _sessionTracker.ObserveRemoteResumeUniverseOnMainThread();
                return;
            }
            if (task == (int)EGameLogicTask.FactoryConstructionSystem)
            {
                _normalActionCoordinator.ObserveStructuredConstructionNativeFactoryPassOnMainThread(world);
                return;
            }
            _normalActionCoordinator.VoyagerLocalPrePlayerTickAvailable = true;
            _normalActionCoordinator.VoyagerLocalPrePlayerObservedAtUtc = DateTimeOffset.UtcNow;
            if (_sessionTracker.HasPendingRemoteResumeAdoption)
            {
                // Only this exact Native501 return may acknowledge the new idle
                // owner. Ordinary Update/poll cannot release its held evidence.
                _sessionTracker.UpdateOnMainThread();
                _gameplayJournalManager.UpdateOnMainThread();
                if (!_sessionTracker.TryAdmitRemoteResumeOnMainThread()) return;
            }
            _normalActionCoordinator.UpdateStructuredConstructionPrePlayerTickOnMainThread();
            _normalActionCoordinator.UpdateVoyagerLocalPrePlayerTickOnMainThread();
        }
        catch (Exception exception)
        {
            // Our observer must never interrupt Native's remaining tasks.
            try { StopVoyagerLocalPrePlayerControl(); } catch (Exception) { }
            LogVoyagerStopFailure(exception);
        }
    }

    private void StopVoyagerLocalPrePlayerControl()
    {
        _normalActionCoordinator.VoyagerLocalPrePlayerTickAvailable = false;
        _sessionTracker.StopPendingRemoteFirstFrameOnMainThread();
        try
        {
            _normalActionCoordinator.StopStructuredConstructionForHostShutdownOnMainThread();
            _normalActionCoordinator.StopVoyagerLocalForHostShutdownOnMainThread();
        }
        catch (Exception exception) { LogVoyagerStopFailure(exception); }
        finally
        {
            if (_localPrePlayerManager is not null && _localPrePlayerHandler is not null)
                _localPrePlayerManager.OnTaskLogic -= _localPrePlayerHandler;
            _localPrePlayerLogic = null; _localPrePlayerManager = null; _localPrePlayerWorld = null; _localPrePlayerHandler = null;
        }
    }

    private void StopVoyagerNativeControl()
    {
        _voyagerStopping = true;
        // Original session/player/controller bindings must still exist when owned
        // commands are released. A command release is not proof of native exit.
        try
        {
            _normalActionCoordinator.StopStructuredConstructionForHostShutdownOnMainThread();
            _normalActionCoordinator.StopVoyagerForHostShutdownOnMainThread();
        }
        catch (Exception exception) { LogVoyagerStopFailure(exception); }
        finally
        {
            if (_voyagerTickLogic is not null && _voyagerTickHandler is not null)
                _voyagerTickLogic.onFactoryFrameBegin -= _voyagerTickHandler;
            _voyagerTickLogic = null;
            _voyagerTickHandler = null;
        }
    }

    private void LogVoyagerStopFailure(Exception exception)
    {
        try { _logger.LogError("Voyager control stopped after " + exception.GetType().Name + "; preserve the original action and checkpoint."); }
        catch (Exception) { /* A logging failure must not escape a native event subscriber. */ }
    }
}
