using System.Diagnostics;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Bridge.Core.Diagnostics;

/// <summary>Bounded process-level measurements. No Unity objects or world state are retained.</summary>
public sealed class RuntimePerformanceMonitor
{
    private readonly object _gate = new object();
    private readonly Func<long> _timestamp;
    private readonly long _frequency;
    private readonly int _queueCapacity;
    private readonly int _maxRequestsPerFrame;
    private readonly double _budgetMs;
    private readonly double[] _intervals;
    private readonly double[] _pumpDurations;
    private int _count;
    private int _next;
    private long _completedUpdates;
    private long? _lastStarted;
    private long? _lastCompleted;
    private int _lastProcessedRequests;

    public RuntimePerformanceMonitor(int queueCapacity, int maxRequestsPerFrame, double budgetMs,
        Func<long>? timestamp = null, long timestampFrequency = 0, int sampleCapacity = 600)
    {
        if (queueCapacity <= 0 || maxRequestsPerFrame <= 0 || double.IsNaN(budgetMs)
            || double.IsInfinity(budgetMs) || budgetMs <= 0 || timestampFrequency < 0
            || sampleCapacity < 1 || sampleCapacity > 600)
        {
            throw new ArgumentOutOfRangeException(nameof(queueCapacity), "Positive bounded measurement settings are required.");
        }

        _queueCapacity = queueCapacity;
        _maxRequestsPerFrame = maxRequestsPerFrame;
        _budgetMs = budgetMs;
        _timestamp = timestamp ?? Stopwatch.GetTimestamp;
        _frequency = timestampFrequency == 0 ? Stopwatch.Frequency : timestampFrequency;
        _intervals = new double[sampleCapacity];
        _pumpDurations = new double[sampleCapacity];
    }

    public void RecordCompletedUpdate(long started, long completed, int processedRequests)
    {
        if (started < 0 || completed < started || processedRequests < 0 || processedRequests > _maxRequestsPerFrame)
        {
            throw new ArgumentOutOfRangeException(nameof(started));
        }

        lock (_gate)
        {
            if (_lastCompleted.HasValue && started < _lastCompleted.Value)
            {
                throw new ArgumentException("Completed update measurements must be monotonic.", nameof(started));
            }

            if (_lastStarted.HasValue)
            {
                _intervals[_next] = Milliseconds(started - _lastStarted.Value);
                _pumpDurations[_next] = Milliseconds(completed - started);
                _next = (_next + 1) % _intervals.Length;
                _count = Math.Min(_count + 1, _intervals.Length);
            }

            _lastStarted = started;
            _lastCompleted = completed;
            _lastProcessedRequests = processedRequests;
            _completedUpdates++;
        }
    }

    public RuntimePerformanceSnapshot Capture(int pendingMainThreadRequests)
    {
        if (pendingMainThreadRequests < 0 || pendingMainThreadRequests > _queueCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(pendingMainThreadRequests));
        }

        lock (_gate)
        {
            var now = _timestamp();
            var intervals = new double[_count];
            var pumps = new double[_count];
            Array.Copy(_intervals, intervals, _count);
            Array.Copy(_pumpDurations, pumps, _count);
            return new RuntimePerformanceSnapshot
            {
                State = _count >= Math.Min(30, _intervals.Length) ? "ready" : "warming_up",
                SampleCount = _count,
                SampleCapacity = _intervals.Length,
                CompletedUpdates = _completedUpdates,
                WindowDurationMs = intervals.Sum(),
                SampleAgeMs = _lastCompleted.HasValue ? Milliseconds(Math.Max(0, now - _lastCompleted.Value)) : (double?)null,
                UpdateInterval = Summarize(intervals),
                BridgePump = Summarize(pumps),
                PendingMainThreadRequests = pendingMainThreadRequests,
                MainThreadQueueCapacity = _queueCapacity,
                LastProcessedMainThreadRequests = _lastProcessedRequests,
                MaxRequestsPerFrame = _maxRequestsPerFrame,
                MainThreadBudgetMs = _budgetMs,
            };
        }
    }

    private double Milliseconds(long ticks) => ticks * (1000d / _frequency);

    private static RuntimeDurationStatistics? Summarize(double[] values)
    {
        if (values.Length == 0) return null;
        Array.Sort(values);
        return new RuntimeDurationStatistics
        {
            MeanMs = values.Average(),
            P95Ms = values[(int)Math.Ceiling(values.Length * 0.95d) - 1],
            MaxMs = values[values.Length - 1],
        };
    }
}
