namespace Spherewright.Contracts.Sessions;

public sealed class RuntimePerformanceSnapshot
{
    public int Version { get; set; } = 1;
    public string Source { get; set; } = "unity_update_stopwatch_v1";
    public string Scope { get; set; } = "plugin-process";
    public string State { get; set; } = "warming_up";
    public int SampleCount { get; set; }
    public int SampleCapacity { get; set; }
    public long CompletedUpdates { get; set; }
    public double WindowDurationMs { get; set; }
    public double? SampleAgeMs { get; set; }
    public RuntimeDurationStatistics? UpdateInterval { get; set; }
    public RuntimeDurationStatistics? BridgePump { get; set; }
    public int PendingMainThreadRequests { get; set; }
    public int MainThreadQueueCapacity { get; set; }
    public int LastProcessedMainThreadRequests { get; set; }
    public int MaxRequestsPerFrame { get; set; }
    public double MainThreadBudgetMs { get; set; }
}

public sealed class RuntimeDurationStatistics
{
    public double MeanMs { get; set; }
    public double P95Ms { get; set; }
    public double MaxMs { get; set; }
}
