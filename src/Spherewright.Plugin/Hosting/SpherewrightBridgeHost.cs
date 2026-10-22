using System.Diagnostics;
using BepInEx.Logging;
using Spherewright.Bridge.Core.Abstractions;
using Spherewright.Bridge.Core.Diagnostics;
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
    private readonly RuntimePerformanceMonitor _performanceMonitor;
    private readonly RuntimeDescriptorPublisher _descriptorPublisher;
    private readonly NamedPipeBridgeServer _pipeServer;
    private readonly BridgeRuntimeDescriptor _descriptor;
    private bool _started;
    private bool _pumpLogged;
    private int _framesSinceVersionRefresh;

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
        RuntimePerformanceMonitor performanceMonitor,
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
        _performanceMonitor = performanceMonitor;
        _descriptorPublisher = descriptorPublisher;
        _pipeServer = pipeServer;
        _descriptor = descriptor;
    }

    public static SpherewrightBridgeHost Create(
        SpherewrightConfiguration configuration,
        ManualLogSource logger,
        string pluginVersion)
    {
        var identity = BridgeIdentity.Create(configuration.PipeNamePrefix);
        var versionProvider = new GameVersionSnapshotProvider();
        var gameVersion = versionProvider.CaptureOnMainThread();
        var dispatcher = new BoundedMainThreadDispatcher(configuration.MaxMainThreadQueue);
        var performanceMonitor = new RuntimePerformanceMonitor(configuration.MaxMainThreadQueue,
            configuration.MaxRequestsPerFrame, configuration.FrameBudgetMs);
        var statusProvider = new BridgeStatusSnapshotProvider(
            identity.BridgeInstanceId,
            pluginVersion,
            gameVersion,
            configuration.AllowWrites,
            performanceMonitor,
            dispatcher);
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
            performanceMonitor,
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

        var updateStarted = Stopwatch.GetTimestamp();
        _sessionTracker.UpdateOnMainThread();
        _statusProvider.UpdateGameLoadedOnMainThread(_sessionTracker.GameLoaded);
        _gameplayJournalManager.UpdateOnMainThread();
        _normalActionCoordinator.UpdateOnMainThread();
        _researchResultAutoAcknowledger.UpdateOnMainThread();

        var processedRequests = _dispatcher.Pump(
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

        _performanceMonitor.RecordCompletedUpdate(updateStarted, Stopwatch.GetTimestamp(), processedRequests);
    }

    public void Dispose()
    {
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
}
