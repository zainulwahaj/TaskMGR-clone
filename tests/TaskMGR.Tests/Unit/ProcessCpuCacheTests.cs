using FluentAssertions;
using TaskMGR.Platform.Windows;

namespace TaskMGR.Tests.Unit;

public sealed class ProcessCpuCacheTests
{
    [Fact]
    public void TryGetPercent_OnEmptyCache_ReturnsFalse()
    {
        var clock = new FakeClock();
        var cache = new ProcessCpuCache(clock, processorCount: 4);

        var found = cache.TryGetPercent(1234, out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryGetPercent_AfterOneUpdate_ReturnsFalse()
    {
        var clock = new FakeClock();
        var cache = new ProcessCpuCache(clock, processorCount: 4);

        cache.Update(1234, TimeSpan.FromSeconds(1));

        var found = cache.TryGetPercent(1234, out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryGetPercent_AfterTwoUpdatesWithKnownDelta_ReturnsExpectedPercent()
    {
        var clock = new FakeClock();
        var cache = new ProcessCpuCache(clock, processorCount: 2);

        cache.Update(1234, TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(1));
        cache.Update(1234, TimeSpan.FromSeconds(2));

        var found = cache.TryGetPercent(1234, out var percent);

        found.Should().BeTrue();
        percent.Should().BeApproximately(50d, 0.001d);
    }

    [Fact]
    public void Cleanup_AfterTtlExpiry_EvictsEntry()
    {
        var clock = new FakeClock();
        var cache = new ProcessCpuCache(clock, processorCount: 2);

        cache.Update(1234, TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(1));
        cache.Update(1234, TimeSpan.FromSeconds(2));

        cache.TryGetPercent(1234, out _).Should().BeTrue();

        clock.Advance(TimeSpan.FromSeconds(31));
        cache.Cleanup(new HashSet<int> { 1234 });

        cache.TryGetPercent(1234, out _).Should().BeFalse();
    }

    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; private set; } = DateTime.UnixEpoch;

        public void Advance(TimeSpan delta)
        {
            UtcNow = UtcNow.Add(delta);
        }
    }
}
