using System.Collections.Generic;

namespace TaskMGR.Platform.Windows;

public sealed class ProcessCpuCache : IProcessCpuCache
{
    private static readonly TimeSpan DefaultEntryTtl = TimeSpan.FromSeconds(30);

    private readonly Dictionary<int, CacheEntry> _entries = new();
    private readonly int _processorCount;
    private readonly IClock _clock;
    private readonly TimeSpan _entryTtl;

    public ProcessCpuCache(int? processorCount = null)
        : this(new SystemClock(), processorCount)
    {
    }

    public ProcessCpuCache(IClock clock, int? processorCount = null, TimeSpan? entryTtl = null)
    {
        ArgumentNullException.ThrowIfNull(clock);

        _clock = clock;
        _entryTtl = entryTtl ?? DefaultEntryTtl;
        _processorCount = Math.Max(1, processorCount ?? Environment.ProcessorCount);
    }

    public void Update(int pid, TimeSpan totalCpu)
    {
        var now = _clock.UtcNow;
        var percent = 0d;
        var hasPercent = false;

        if (_entries.TryGetValue(pid, out var cached))
        {
            var elapsedMs = (now - cached.LastTime).TotalMilliseconds;
            if (elapsedMs > 0)
            {
                var cpuMs = (totalCpu - cached.LastTotalTime).TotalMilliseconds;
                percent = Math.Clamp((cpuMs / elapsedMs) * 100d / _processorCount, 0d, 100d);
                hasPercent = true;
            }
            else
            {
                percent = cached.LastPercent;
                hasPercent = cached.HasPercent;
            }
        }

        _entries[pid] = new CacheEntry(now, totalCpu, percent, hasPercent);
    }

    public bool TryGetPercent(int pid, out double pct)
    {
        if (_entries.TryGetValue(pid, out var cached) && cached.HasPercent)
        {
            pct = cached.LastPercent;
            return true;
        }

        pct = 0;
        return false;
    }

    public void Cleanup(IReadOnlySet<int> activePids)
    {
        var now = _clock.UtcNow;
        var expired = _entries
            .Where(entry =>
                !activePids.Contains(entry.Key)
                || now - entry.Value.LastTime > _entryTtl)
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var pid in expired)
        {
            _entries.Remove(pid);
        }
    }

    private readonly record struct CacheEntry(
        DateTime LastTime,
        TimeSpan LastTotalTime,
        double LastPercent,
        bool HasPercent);
}
