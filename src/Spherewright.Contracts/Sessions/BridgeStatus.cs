namespace Spherewright.Contracts.Sessions;

public sealed class BridgeStatus
{
    public bool BridgeConnected { get; set; }

    public string BridgeInstanceId { get; set; } = string.Empty;

    public string PluginVersion { get; set; } = string.Empty;

    public int ProtocolVersion { get; set; }

    public string GameVersion { get; set; } = string.Empty;

    public bool GameLoaded { get; set; }

    public bool WritesConfigured { get; set; }

    public string WriteHealth { get; set; } = WriteHealthStates.Healthy;

    public RuntimePerformanceSnapshot? RuntimePerformance { get; set; }
}

