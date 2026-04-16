using FluentAssertions;
using TaskMGR.Platform.MacOS;
using TaskMGR.Tests.TestInfrastructure;

namespace TaskMGR.Tests.Integration;

public sealed class MacOSPlatformServiceTests
{
    [MacOSFact]
    public async Task GetProcessesAsync_ReturnsAtLeastOneItem()
    {
        var service = new MacOSPlatformService();

        var result = await service.GetProcessesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [MacOSFact]
    public async Task GetProcessesAsync_ReturnedItemsHaveValidPidAndName()
    {
        var service = new MacOSPlatformService();

        var result = await service.GetProcessesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().OnlyContain(process => process.Pid > 0 && !string.IsNullOrWhiteSpace(process.Name));
    }

    [MacOSFact]
    public async Task GetSystemMetricsAsync_TotalMemoryBytesIsPositive()
    {
        var service = new MacOSPlatformService();

        _ = await service.GetProcessesAsync();
        var metrics = await service.GetSystemMetricsAsync();

        metrics.TotalMemoryBytes.Should().BeGreaterThan(0);
    }
}
