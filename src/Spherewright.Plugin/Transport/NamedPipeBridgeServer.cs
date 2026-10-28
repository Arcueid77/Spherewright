using System.IO.Pipes;
using BepInEx.Logging;
using Newtonsoft.Json;
using Spherewright.Bridge.Core.Abstractions;
using Spherewright.Bridge.Core.Authentication;
using Spherewright.Bridge.Core.Framing;
using Spherewright.Bridge.Core.Routing;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Celestial;
using Spherewright.Contracts.Diagnostics;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Resources;
using Spherewright.Contracts.Protocol;
using Spherewright.Contracts.Sessions;
using Spherewright.Contracts.Testing;
using Spherewright.Plugin.Hosting;
using Spherewright.Plugin.Game;
using Spherewright.Plugin.Security;

namespace Spherewright.Plugin.Transport;

internal sealed class NamedPipeBridgeServer : IDisposable
{
    private readonly object _gate = new object();
    private readonly string _pipeName;
    private readonly string _bridgeInstanceId;
    private readonly string _pluginVersion;
    private readonly FrameCodec _frameCodec;
    private readonly TimeSpan _readRequestTimeout;
    private readonly HandshakeAuthenticator _authenticator;
    private readonly BridgeStatusSnapshotProvider _statusProvider;
    private readonly BoundedMainThreadDispatcher _dispatcher;
    private readonly GameStateReader _gameStateReader;
    private readonly GameplayJournalManager _gameplayJournalManager;
    private readonly TestWorldCoordinator _testWorldCoordinator;
    private readonly UserSaveImportCoordinator _userSaveImportCoordinator;
    private readonly OwnedWorldResumeCoordinator _ownedWorldResumeCoordinator;
    private readonly FlightCheckpointReloadCoordinator _flightCheckpointReloadCoordinator;
    private readonly NormalGameActionCoordinator _normalActionCoordinator;
    private readonly ManualLogSource _logger;
    private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
    private Task? _serverTask;
    private NamedPipeServerStream? _activePipe;
    private int _lastAuthWarningTick;

    public NamedPipeBridgeServer(
        BridgeIdentity identity,
        string pluginVersion,
        int maxFrameBytes,
        int readRequestTimeoutSeconds,
        BridgeStatusSnapshotProvider statusProvider,
        BoundedMainThreadDispatcher dispatcher,
        GameStateReader gameStateReader,
        GameplayJournalManager gameplayJournalManager,
        TestWorldCoordinator testWorldCoordinator,
        UserSaveImportCoordinator userSaveImportCoordinator,
        OwnedWorldResumeCoordinator ownedWorldResumeCoordinator,
        FlightCheckpointReloadCoordinator flightCheckpointReloadCoordinator,
        NormalGameActionCoordinator normalActionCoordinator,
        ManualLogSource logger)
    {
        _pipeName = identity.PipeName;
        _bridgeInstanceId = identity.BridgeInstanceId;
        _pluginVersion = pluginVersion;
        _frameCodec = new FrameCodec(maxFrameBytes);
        _readRequestTimeout = TimeSpan.FromSeconds(readRequestTimeoutSeconds);
        _authenticator = new HandshakeAuthenticator(identity.BridgeInstanceId, identity.AuthToken);
        _statusProvider = statusProvider;
        _dispatcher = dispatcher;
        _gameStateReader = gameStateReader;
        _gameplayJournalManager = gameplayJournalManager;
        _testWorldCoordinator = testWorldCoordinator;
        _userSaveImportCoordinator = userSaveImportCoordinator;
        _ownedWorldResumeCoordinator = ownedWorldResumeCoordinator;
        _flightCheckpointReloadCoordinator = flightCheckpointReloadCoordinator;
        _normalActionCoordinator = normalActionCoordinator;
        _logger = logger;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_serverTask is not null)
            {
                throw new InvalidOperationException("The Named Pipe bridge is already running.");
            }

            _serverTask = Task.Run(() => RunAsync(_shutdown.Token));
        }
    }

    public void Dispose()
    {
        Task? task;
        lock (_gate)
        {
            _shutdown.Cancel();
            _activePipe?.Dispose();
            task = _serverTask;
            _serverTask = null;
        }

        if (task is not null)
        {
            try
            {
                task.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException exception) when (exception.InnerExceptions.All(inner => inner is OperationCanceledException || inner is ObjectDisposedException))
            {
            }
        }

        _shutdown.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using (var pipe = CreateServerPipe())
            using (cancellationToken.Register(pipe.Dispose))
            {
                lock (_gate)
                {
                    _activePipe = pipe;
                }

                try
                {
                    await pipe.WaitForConnectionAsync().ConfigureAwait(false);
                    await HandleConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    cancellationToken.IsCancellationRequested
                    && (exception is OperationCanceledException || exception is ObjectDisposedException || exception is IOException))
                {
                    return;
                }
                catch (FrameProtocolException exception)
                {
                    _logger.LogWarning($"Spherewright rejected an invalid bridge frame: {exception.Message}");
                }
                catch (JsonException exception)
                {
                    _logger.LogWarning($"Spherewright rejected malformed bridge JSON: {exception.Message}");
                }
                catch (IOException exception)
                {
                    _logger.LogDebug($"Spherewright bridge connection ended: {exception.Message}");
                }
                catch (Exception exception)
                {
                    _logger.LogError($"Spherewright bridge connection failed: {exception.GetType().Name}: {exception.Message}");
                }
                finally
                {
                    lock (_gate)
                    {
                        if (ReferenceEquals(_activePipe, pipe))
                        {
                            _activePipe = null;
                        }
                    }
                }
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        var handshakeBytes = await _frameCodec.ReadFrameAsync(pipe, cancellationToken).ConfigureAwait(false);
        if (handshakeBytes is null)
        {
            return;
        }

        var handshakeJson = FrameCodec.DecodeUtf8(handshakeBytes);
        var handshakeHeader = PluginJson.Deserialize<BridgeEnvelopeHeader>(handshakeJson);
        var headerError = ProtocolValidator.ValidateHeader(handshakeHeader, BridgeMessageTypes.Handshake);
        var handshakeEnvelope = PluginJson.Deserialize<BridgeRequestEnvelope<HandshakeRequest>>(handshakeJson);
        var authError = headerError ?? _authenticator.Authenticate(handshakeEnvelope?.Payload);
        if (authError is not null)
        {
            LogAuthenticationFailureWithRateLimit();
            return;
        }

        var handshakeResponse = new BridgeResponseEnvelope<HandshakeResponse>
        {
            RequestId = handshakeEnvelope!.RequestId,
            Success = true,
            Result = new HandshakeResponse
            {
                Accepted = true,
                BridgeInstanceId = _bridgeInstanceId,
                PluginVersion = _pluginVersion,
                ProtocolVersion = ProtocolConstants.CurrentVersion,
            },
        };
        await WriteEnvelopeAsync(pipe, handshakeResponse, cancellationToken).ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
        {
            var requestBytes = await _frameCodec.ReadFrameAsync(pipe, cancellationToken).ConfigureAwait(false);
            if (requestBytes is null)
            {
                return;
            }

            var requestJson = FrameCodec.DecodeUtf8(requestBytes);
            var header = PluginJson.Deserialize<BridgeEnvelopeHeader>(requestJson);
            var validationError = ProtocolValidator.ValidateHeader(header, BridgeMessageTypes.Request);
            if (validationError is not null)
            {
                await WriteErrorAsync(pipe, header?.RequestId, validationError, cancellationToken).ConfigureAwait(false);
                continue;
            }

            switch (header!.Method)
            {
                case BridgeMethods.GetBridgeStatus:
                    await WriteResultAsync(
                        pipe,
                        header.RequestId,
                        null,
                        _statusProvider.Capture(),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.GetSessionState:
                    await DispatchAndWriteAsync(
                        pipe,
                        header.RequestId,
                        null,
                        _gameStateReader.GetSessionStateOnMainThread,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.GetPlayerState:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<LocalPlanetRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _gameStateReader.GetPlayerStateOnMainThread(header.SessionId, request.Payload,
                                captureMoveInspection: true),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.GetProgressionState:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<LocalPlanetRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _gameStateReader.GetProgressionStateOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.GetGameplayJournal:
                    await DispatchAndWriteAsync(
                        pipe,
                        header.RequestId,
                        header.SessionId,
                        () => _gameplayJournalManager.CaptureOnMainThread(header.SessionId),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.GetRecipeCatalog:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<LocalPlanetRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _gameStateReader.GetRecipeCatalogOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.GetFoundryPlan:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<GetFoundryPlanRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(pipe, header.RequestId, header.SessionId,
                            () => _gameStateReader.GetFoundryPlanOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.GetGovernorPlan:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<GetGovernorPlanRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }
                        await DispatchAndWriteAsync(pipe, header.RequestId, header.SessionId,
                            () => _gameStateReader.GetGovernorPlanOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.InspectBlueprint:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<InspectBlueprintRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }
                        await DispatchAndWriteAsync(pipe, header.RequestId, header.SessionId,
                            () => _gameStateReader.InspectBlueprintOnMainThread(header.SessionId, request.Payload), cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.GetBlueprintBuilds:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<BlueprintBuildRequest>>(requestJson);
                        if (request?.Payload is null) { await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false); break; }
                        await DispatchAndWriteAsync(pipe, header.RequestId, header.SessionId,
                            () => _normalActionCoordinator.GetBlueprintBuildsOnMainThread(header.SessionId, request.Payload), cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.PrepareBlueprintBuild:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareBlueprintBuildRequest>>(requestJson);
                        if (request?.Payload is null) { await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false); break; }
                        await DispatchAndWriteAsync(pipe, header.RequestId, header.SessionId,
                            () => _normalActionCoordinator.PrepareBlueprintBuildOnMainThread(header.SessionId, request.Payload), cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitBlueprintBuild:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.BlueprintBuild, cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareCancelBlueprint:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareCancelBlueprintRequest>>(requestJson);
                        if (request?.Payload is null) { await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false); break; }
                        await DispatchAndWriteAsync(pipe, header.RequestId, header.SessionId,
                            () => _normalActionCoordinator.PrepareCancelBlueprintOnMainThread(header.SessionId, request.Payload), cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitCancelBlueprint:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.CancelBlueprintBuild, cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.ExportBlueprint:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<ExportBlueprintRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }
                        await DispatchAndWriteAsync(pipe, header.RequestId, header.SessionId,
                            () => _gameStateReader.ExportBlueprintOnMainThread(header.SessionId, request.Payload), cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.ListResourceNodes:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<ListResourceNodesRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _gameStateReader.ListResourceNodesOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.InspectResourceNode:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<InspectResourceNodeRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _gameStateReader.InspectResourceNodeOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.ListFactoryEntities:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<ListFactoryEntitiesRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _gameStateReader.ListFactoryEntitiesOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.InspectFactoryEntity:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<InspectFactoryEntityRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _gameStateReader.InspectFactoryEntityOnMainThread(
                                header.SessionId, request.Payload, allowCurrentStarRemoteReadOnly: true),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.GetPowerSummary:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<LocalPlanetRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _gameStateReader.GetPowerSummaryOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.GetOverseerProduction:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<GetOverseerProductionRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _gameStateReader.GetOverseerProductionOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.GetOverseerSummary:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<GetOverseerSummaryRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _gameStateReader.GetOverseerSummaryOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.GetOverseerDiagnosticBundle:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<GetOverseerDiagnosticBundleRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _gameStateReader.GetOverseerDiagnosticBundleOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.GetActionResult:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<GetActionResultRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => GetActionResultOnMainThread(request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.PrepareMove:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareMoveRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.PrepareMoveOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitMove:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.Move, cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareInterplanetaryFlight:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareInterplanetaryFlightRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.PrepareInterplanetaryFlightOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitInterplanetaryFlight:
                    await DispatchNormalCommitAsync(
                        pipe,
                        header,
                        requestJson,
                        NormalActionKinds.InterplanetaryFlight,
                        cancellationToken, expectedVoyagerMode: false).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareVoyagerFlight:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareVoyagerFlightRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }
                        await DispatchAndWriteAsync(pipe, header.RequestId, header.SessionId,
                            () => _normalActionCoordinator.PrepareVoyagerFlightOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitVoyagerFlight:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.InterplanetaryFlight,
                        cancellationToken, expectedVoyagerMode: true).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareCancelVoyagerFlight:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareCancelVoyagerFlightRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }
                        await DispatchAndWriteAsync(pipe, header.RequestId, header.SessionId,
                            () => _normalActionCoordinator.PrepareCancelVoyagerFlightOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitCancelVoyagerFlight:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.CancelVoyagerFlight,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareHarvest:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareHarvestRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.PrepareHarvestOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitHarvest:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.Harvest, cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareHandcraft:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareHandcraftRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.PrepareHandcraftOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitHandcraft:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.Handcraft, cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareSelectResearch:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareSelectResearchRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.PrepareSelectResearchOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitSelectResearch:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.SelectResearch, cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareBuild:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareBuildRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.PrepareBuildOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitBuild:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.Build, cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareDismantle:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareDismantleRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.PrepareDismantleOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.PrepareUpgrade:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareUpgradeRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }
                        await DispatchAndWriteAsync(pipe, header.RequestId, header.SessionId,
                            () => _normalActionCoordinator.PrepareUpgradeOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitUpgrade:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.Upgrade, cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.CommitDismantle:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.Dismantle, cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareConfigureBuilding:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareConfigureBuildingRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.PrepareConfigureBuildingOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitConfigureBuilding:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.ConfigureBuilding, cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareTransfer:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareTransferRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.PrepareTransferOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitTransfer:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.Transfer, cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareLogisticsStationFleetTransfer:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareLogisticsStationFleetTransferRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.PrepareLogisticsStationFleetTransferOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitLogisticsStationFleetTransfer:
                    await DispatchNormalCommitAsync(
                        pipe,
                        header,
                        requestJson,
                        NormalActionKinds.LogisticsStationFleetTransfer,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareRefuel:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareRefuelRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.PrepareRefuelOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitRefuel:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.Refuel, cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareSupplyMechaWarpers:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareSupplyMechaWarpersRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.PrepareSupplyMechaWarpersOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitSupplyMechaWarpers:
                    await DispatchNormalCommitAsync(
                        pipe, header, requestJson, NormalActionKinds.SupplyMechaWarpers, cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareSave:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareSaveRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.PrepareSaveOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitSave:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.Save, cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareReturnToMainMenu:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareSaveRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.PrepareReturnToMainMenuOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitReturnToMainMenu:
                    await DispatchNormalCommitAsync(pipe, header, requestJson, NormalActionKinds.ReturnToMainMenu, cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareQuarantineReconciliation:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareQuarantineReconciliationRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.PrepareQuarantineReconciliationOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.GetLocalStarSystem:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<LocalPlanetRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _gameStateReader.GetLocalStarSystemOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.GetVoyagerStarCatalog:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<GetVoyagerStarCatalogRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }
                        await DispatchAndWriteAsync(pipe, header.RequestId, header.SessionId,
                            () => _gameStateReader.GetVoyagerStarCatalogOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.PreviewVoyagerRoute:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PreviewVoyagerRouteRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }
                        await DispatchAndWriteAsync(pipe, header.RequestId, header.SessionId,
                            () => _gameStateReader.PreviewVoyagerRouteOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitQuarantineReconciliation:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<CommitNormalActionRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _normalActionCoordinator.CommitQuarantineReconciliationOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.ListAssemblers:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<ListAssemblersRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _gameStateReader.ListAssemblersOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.InspectAssembler:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<InspectAssemblerRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _gameStateReader.InspectAssemblerOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.GetBuildCatalog:
                    await DispatchAndWriteAsync(
                        pipe,
                        header.RequestId,
                        header.SessionId,
                        () => _gameStateReader.GetBuildCatalogOnMainThread(header.SessionId),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case BridgeMethods.PrepareNewGame:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareTestWorldRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            null,
                            () => _testWorldCoordinator.PrepareOnMainThread(request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitNewGame:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<CommitTestWorldRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            null,
                            () => _testWorldCoordinator.CommitOnMainThread(request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.PrepareImportCurrentGame:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareUserSaveImportRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _userSaveImportCoordinator.PrepareOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitImportCurrentGame:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<CommitUserSaveImportRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            header.SessionId,
                            () => _userSaveImportCoordinator.CommitOnMainThread(header.SessionId, request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.PrepareResumeOwnedGame:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareOwnedWorldResumeRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            null,
                            () => _ownedWorldResumeCoordinator.PrepareOnMainThread(request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitResumeOwnedGame:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<CommitOwnedWorldResumeRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            null,
                            () => _ownedWorldResumeCoordinator.CommitOnMainThread(request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.PrepareReloadFlightCheckpoint:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<PrepareFlightCheckpointReloadRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            null,
                            () => _flightCheckpointReloadCoordinator.PrepareOnMainThread(request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                case BridgeMethods.CommitReloadFlightCheckpoint:
                    {
                        var request = PluginJson.Deserialize<BridgeRequestEnvelope<CommitFlightCheckpointReloadRequest>>(requestJson);
                        if (request?.Payload is null)
                        {
                            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await DispatchAndWriteAsync(
                            pipe,
                            header.RequestId,
                            null,
                            () => _flightCheckpointReloadCoordinator.CommitOnMainThread(request.Payload),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }
                default:
                    await WriteErrorAsync(
                        pipe,
                        header.RequestId,
                        BridgeError.Create(
                            BridgeErrorCodes.InvalidRequest,
                            "The requested bridge method is not available.",
                            false,
                            "Call a method advertised by the current Spherewright MCP server."),
                        cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
    }

    private GameCallResult<ActionResultSnapshot> GetActionResultOnMainThread(GetActionResultRequest request)
    {
        if (_userSaveImportCoordinator.TryGetActionResultOnMainThread(request.ActionId, out var importResult)
            && importResult is not null)
        {
            return GameCallResult<ActionResultSnapshot>.Succeeded(importResult);
        }

        if (_normalActionCoordinator.TryGetActionResultOnMainThread(request.ActionId, out var normalResult)
            && normalResult is not null)
        {
            return GameCallResult<ActionResultSnapshot>.Succeeded(normalResult);
        }

        if (_ownedWorldResumeCoordinator.TryGetActionResultOnMainThread(request.ActionId, out var resumeResult)
            && resumeResult is not null)
        {
            return GameCallResult<ActionResultSnapshot>.Succeeded(resumeResult);
        }

        if (_flightCheckpointReloadCoordinator.TryGetActionResultOnMainThread(request.ActionId, out var checkpointResult)
            && checkpointResult is not null)
        {
            return GameCallResult<ActionResultSnapshot>.Succeeded(checkpointResult);
        }

        return _testWorldCoordinator.GetActionResultOnMainThread(request);
    }

    private async Task DispatchNormalCommitAsync(
        Stream pipe,
        BridgeEnvelopeHeader header,
        string requestJson,
        string actionKind,
        CancellationToken cancellationToken,
        bool? expectedVoyagerMode = null)
    {
        var request = PluginJson.Deserialize<BridgeRequestEnvelope<CommitNormalActionRequest>>(requestJson);
        if (request?.Payload is null)
        {
            await WriteInvalidPayloadAsync(pipe, header.RequestId, cancellationToken).ConfigureAwait(false);
            return;
        }

        await DispatchAndWriteAsync(
            pipe,
            header.RequestId,
            header.SessionId,
            () => _normalActionCoordinator.CommitOnMainThread(actionKind, header.SessionId, request.Payload, expectedVoyagerMode),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task DispatchAndWriteAsync<T>(
        Stream pipe,
        string requestId,
        string? sessionId,
        Func<GameCallResult<T>> operation,
        CancellationToken cancellationToken)
    {
        if (!_dispatcher.TryEnqueue(operation, out var completion))
        {
            await WriteErrorAsync(
                pipe,
                requestId,
                BridgeError.Create(
                    BridgeErrorCodes.QueueFull,
                    "The Unity main-thread request queue is full.",
                    true,
                    "Retry after the game has processed pending requests."),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var timeoutTask = Task.Delay(_readRequestTimeout, cancellationToken);
        var completed = await Task.WhenAny(completion, timeoutTask).ConfigureAwait(false);
        if (!ReferenceEquals(completed, completion))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteErrorAsync(
                pipe,
                requestId,
                BridgeError.Create(
                    BridgeErrorCodes.RequestTimeout,
                    "The Unity main-thread read request timed out.",
                    true,
                    "Wait for DSP to become responsive and retry."),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        GameCallResult<T> result;
        try
        {
            result = await completion.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError($"Spherewright main-thread operation failed: {exception}");
            await WriteErrorAsync(
                pipe,
                requestId,
                BridgeError.Create(
                    BridgeErrorCodes.InternalError,
                    "A main-thread game-state read failed.",
                    true,
                    "Retry once. If it repeats, inspect the local Spherewright log."),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!result.Success || result.Value is null)
        {
            await WriteErrorAsync(
                pipe,
                requestId,
                result.Error ?? BridgeError.Create(
                    BridgeErrorCodes.InternalError,
                    "The game-state read returned an incomplete result.",
                    true,
                    "Retry the request."),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteResultAsync(pipe, requestId, sessionId, result.Value, cancellationToken).ConfigureAwait(false);
    }

    private Task WriteInvalidPayloadAsync(Stream pipe, string requestId, CancellationToken cancellationToken)
    {
        return WriteErrorAsync(
            pipe,
            requestId,
            BridgeError.Create(
                BridgeErrorCodes.InvalidRequest,
                "The bridge request payload is missing or invalid.",
                false,
                "Correct the request payload and retry."),
            cancellationToken);
    }

    private Task WriteResultAsync<T>(
        Stream pipe,
        string requestId,
        string? sessionId,
        T result,
        CancellationToken cancellationToken)
    {
        return WriteEnvelopeAsync(
            pipe,
            new BridgeResponseEnvelope<T>
            {
                RequestId = requestId,
                SessionId = sessionId,
                Success = true,
                Result = result,
            },
            cancellationToken);
    }

    private NamedPipeServerStream CreateServerPipe()
    {
        return WindowsCurrentUserSecurity.CreateSecurePipe(_pipeName, 4096, 4096);
    }

    private async Task WriteErrorAsync(
        Stream pipe,
        string? requestId,
        BridgeError error,
        CancellationToken cancellationToken)
    {
        var response = new BridgeResponseEnvelope<object>
        {
            RequestId = string.IsNullOrWhiteSpace(requestId) ? Guid.Empty.ToString("D") : requestId!,
            Success = false,
            Error = error,
        };
        await WriteEnvelopeAsync(pipe, response, cancellationToken).ConfigureAwait(false);
    }

    private Task WriteEnvelopeAsync<T>(
        Stream pipe,
        BridgeResponseEnvelope<T> envelope,
        CancellationToken cancellationToken)
    {
        var payload = FrameCodec.EncodeUtf8(PluginJson.Serialize(envelope));
        return _frameCodec.WriteFrameAsync(pipe, payload, cancellationToken);
    }

    private void LogAuthenticationFailureWithRateLimit()
    {
        var now = Environment.TickCount;
        var previous = Interlocked.Exchange(ref _lastAuthWarningTick, now);
        if (previous == 0 || unchecked(now - previous) >= 5000)
        {
            _logger.LogWarning("Spherewright rejected a bridge authentication attempt.");
        }
    }
}
