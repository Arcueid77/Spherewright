using Spherewright.Bridge.Core.Abstractions;
using Spherewright.Bridge.Core.Diagnostics;
using Spherewright.Contracts.Protocol;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Hosting;

internal sealed class BridgeStatusSnapshotProvider
{
    private readonly object _gate = new object();
    private readonly string _bridgeInstanceId;
    private readonly string _pluginVersion;
    private readonly bool _writesConfigured;
    private readonly RuntimePerformanceMonitor _performanceMonitor;
    private readonly BoundedMainThreadDispatcher _dispatcher;
    private string _gameVersion;
    private bool _gameLoaded;

    public BridgeStatusSnapshotProvider(
        string bridgeInstanceId,
        string pluginVersion,
        string gameVersion,
        bool writesConfigured,
        RuntimePerformanceMonitor performanceMonitor,
        BoundedMainThreadDispatcher dispatcher)
    {
        _bridgeInstanceId = bridgeInstanceId;
        _pluginVersion = pluginVersion;
        _gameVersion = gameVersion;
        _writesConfigured = writesConfigured;
        _performanceMonitor = performanceMonitor;
        _dispatcher = dispatcher;
    }

    public void UpdateGameVersionOnMainThread(string gameVersion)
    {
        lock (_gate)
        {
            _gameVersion = gameVersion;
        }
    }

    public void UpdateGameLoadedOnMainThread(bool gameLoaded)
    {
        lock (_gate)
        {
            _gameLoaded = gameLoaded;
        }
    }

    public BridgeStatus Capture()
    {
        lock (_gate)
        {
            return new BridgeStatus
            {
                BridgeConnected = true,
                BridgeInstanceId = _bridgeInstanceId,
                PluginVersion = _pluginVersion,
                ProtocolVersion = ProtocolConstants.CurrentVersion,
                GameVersion = _gameVersion,
                GameLoaded = _gameLoaded,
                WritesConfigured = _writesConfigured,
                WriteHealth = WriteHealthStates.Healthy,
                RuntimePerformance = _performanceMonitor.Capture(_dispatcher.Count),
            };
        }
    }
}
