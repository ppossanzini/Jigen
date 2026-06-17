using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Jigen.Metrics.Handlers.Model;

public sealed class LogMetrics
{
    private readonly ConcurrentDictionary<string, MetricWindow> _metrics = new(StringComparer.Ordinal);

    public void Increment(string name, long delta = 1) => Get(name).Add(delta);

    public void SetGauge(string name, double value) => Get(name).Set(value);

    // Snapshot per finestra (es. 10s) per ridurre overhead e scrivere su disco in batch.
    public IReadOnlyList<MetricSample> CollectSnapshot(DateTimeOffset timestampUtc)
    {
        var results = new List<MetricSample>();

        foreach (var entry in _metrics)
        {
            var sample = entry.Value.SnapshotAndReset(entry.Key, timestampUtc);
            if (sample.HasValue)
                results.Add(sample.Value);
        }

        return results;
    }

    private MetricWindow Get(string name) => _metrics.GetOrAdd(name, static _ => new MetricWindow());
}

public readonly record struct MetricSample(
    string Name,
    DateTimeOffset TimestampUtc,
    long Count,
    double Sum,
    double Min,
    double Max,
    double Last
);

internal sealed class MetricWindow
{
    private long _count;
    private double _sum;
    private double _min = double.PositiveInfinity;
    private double _max = double.NegativeInfinity;
    private double _last;
    private int _hasValue;

    public void Add(long delta)
    {
        Interlocked.Add(ref _count, delta);
        InterlockedAdd(ref _sum, delta);
        InterlockedMin(ref _min, delta);
        InterlockedMax(ref _max, delta);
        Interlocked.Exchange(ref _last, (double)delta);
        Volatile.Write(ref _hasValue, 1);
    }

    public void Set(double value)
    {
        Interlocked.Increment(ref _count);
        InterlockedAdd(ref _sum, value);
        InterlockedMin(ref _min, value);
        InterlockedMax(ref _max, value);
        Interlocked.Exchange(ref _last, value);
        Volatile.Write(ref _hasValue, 1);
    }

    public MetricSample? SnapshotAndReset(string name, DateTimeOffset timestampUtc)
    {
        if (Volatile.Read(ref _hasValue) == 0)
            return null;

        var count = Interlocked.Exchange(ref _count, 0);
        var sum   = Interlocked.Exchange(ref _sum, 0.0);
        var min   = Interlocked.Exchange(ref _min, double.PositiveInfinity);
        var max   = Interlocked.Exchange(ref _max, double.NegativeInfinity);
        var last  = Interlocked.Exchange(ref _last, 0.0);
        Volatile.Write(ref _hasValue, 0);

        return new MetricSample(name, timestampUtc, count, sum, min, max, last);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InterlockedAdd(ref double location, double value)
    {
        var current = Volatile.Read(ref location);
        while (true)
        {
            var updated  = current + value;
            var observed = Interlocked.CompareExchange(ref location, updated, current);
            if (observed == current) return;
            current = observed;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InterlockedMin(ref double location, double value)
    {
        var current = Volatile.Read(ref location);
        while (current > value)
        {
            var observed = Interlocked.CompareExchange(ref location, value, current);
            if (observed == current) return;
            current = observed;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InterlockedMax(ref double location, double value)
    {
        var current = Volatile.Read(ref location);
        while (current < value)
        {
            var observed = Interlocked.CompareExchange(ref location, value, current);
            if (observed == current) return;
            current = observed;
        }
    }
}
