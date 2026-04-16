using FluentAssertions;
using TaskMGR.Platform.Windows;
using TaskMGR.Tests.TestInfrastructure;

namespace TaskMGR.Tests.Integration;

public sealed class WindowsPlatformServiceTests
{
    [WindowsFact]
    public async Task GetProcessesAsync_ReturnsAtLeastOneItem()
    {
        var service = new WindowsPlatformService();

        var result = await service.GetProcessesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [WindowsFact]
    public async Task GetProcessesAsync_ReturnedItemsHaveValidPidAndName()
    {
        var service = new WindowsPlatformService();

        var result = await service.GetProcessesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().OnlyContain(process => process.Pid > 0 && !string.IsNullOrWhiteSpace(process.Name));
    }

    [WindowsFact]
    public async Task GetSystemMetricsAsync_TotalMemoryBytesIsPositive()
    {
        var service = new WindowsPlatformService();

        _ = await service.GetProcessesAsync();
        var metrics = await service.GetSystemMetricsAsync();

        metrics.TotalMemoryBytes.Should().BeGreaterThan(0);
    }
}
