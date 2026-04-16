using FluentAssertions;
using TaskMGR.Core.Models;

namespace TaskMGR.Tests.Unit;

public sealed class ProcessInfoTests
{
    [Fact]
    public void Create_WithValidArgs_Succeeds()
    {
        var startTime = DateTime.UtcNow;

        var process = ProcessInfo.Create(
            pid: 42,
            name: "dotnet",
            cpuPercent: 12.5,
            memoryBytes: 1024,
            status: "Running",
            user: "tester",
            startTime: startTime);

        process.Pid.Should().Be(42);
        process.Name.Should().Be("dotnet");
        process.CpuPercent.Should().Be(12.5);
        process.MemoryBytes.Should().Be(1024);
        process.Status.Should().Be("Running");
        process.User.Should().Be("tester");
        process.StartTime.Should().Be(startTime);
    }

    [Fact]
    public void Create_WithNegativePid_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => ProcessInfo.Create(
            pid: -1,
            name: "dotnet",
            cpuPercent: 0,
            memoryBytes: 1024,
            status: "Running",
            user: "tester",
            startTime: DateTime.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrWhitespaceName_ThrowsArgumentException(string name)
    {
        Action act = () => ProcessInfo.Create(
            pid: 1,
            name: name,
            cpuPercent: 0,
            memoryBytes: 1024,
            status: "Running",
            user: "tester",
            startTime: DateTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNullName_ThrowsArgumentException()
    {
        Action act = () => ProcessInfo.Create(
            pid: 1,
            name: null!,
            cpuPercent: 0,
            memoryBytes: 1024,
            status: "Running",
            user: "tester",
            startTime: DateTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }
}
