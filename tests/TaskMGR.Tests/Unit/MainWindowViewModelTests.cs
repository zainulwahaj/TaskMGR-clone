using System.Reflection;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using NSubstitute;
using TaskMGR.Core.Interfaces;
using TaskMGR.Core.Models;
using TaskMGR.Core.Results;
using TaskMGR.UI.ViewModels;
using ResultUnit = TaskMGR.Core.Results.Unit;

namespace TaskMGR.Tests.Unit;

public sealed class MainWindowViewModelTests
{
    [AvaloniaFact]
    public void Constructor_PlatformNameMatchesFake()
    {
        var platformService = CreatePlatformService();
        platformService.PlatformName.Returns("TEST");

        var viewModel = new MainWindowViewModel(platformService);

        viewModel.PlatformName.Should().Be("TEST");
    }

    [AvaloniaFact]
    public async Task RefreshAsync_HappyPath_ProcessesCountMatchesFakeListLength()
    {
        var fakeProcesses = CreateProcessList((1001, "alpha"), (1002, "beta"), (1003, "gamma"));
        var platformService = CreatePlatformService(fakeProcesses);
        var viewModel = new MainWindowViewModel(platformService);

        await InvokePrivateAsync(viewModel, "RefreshAsync");

        viewModel.Processes.Count.Should().Be(fakeProcesses.Count);
    }

    [AvaloniaFact]
    public async Task RefreshAsync_StatusMessageContainsProcessCount()
    {
        var fakeProcesses = CreateProcessList((1001, "alpha"), (1002, "beta"));
        var platformService = CreatePlatformService(fakeProcesses);
        var viewModel = new MainWindowViewModel(platformService);

        await InvokePrivateAsync(viewModel, "RefreshAsync");

        viewModel.StatusMessage.Should().Contain(fakeProcesses.Count.ToString());
    }

    [AvaloniaFact]
    public async Task RefreshAsync_WithSearchText_FiltersProcesses()
    {
        var fakeProcesses = CreateProcessList((1001, "alpha"), (1002, "beta"), (1003, "alpha-worker"));
        var platformService = CreatePlatformService(fakeProcesses);
        var viewModel = new MainWindowViewModel(platformService);
        SetSearchTextWithoutDebounce(viewModel, "alpha");

        await InvokePrivateAsync(viewModel, "RefreshAsync");

        viewModel.Processes.Should().OnlyContain(process =>
            process.Name.Contains("alpha", StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact]
    public async Task KillProcessAsync_WithNullSelectedProcess_DoesNotCallPlatform()
    {
        var platformService = CreatePlatformService();
        var viewModel = new MainWindowViewModel(platformService)
        {
            SelectedProcess = null
        };

        await InvokePrivateAsync(viewModel, "KillProcessAsync");

        _ = platformService.DidNotReceive().KillProcessAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [AvaloniaFact]
    public async Task KillProcessAsync_Success_CallsRefreshAsyncPath()
    {
        var fakeProcesses = CreateProcessList((1001, "alpha"));
        var platformService = CreatePlatformService(fakeProcesses);
        platformService.KillProcessAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<ResultUnit, ProcessError>.Ok(ResultUnit.Value)));

        var viewModel = new MainWindowViewModel(platformService)
        {
            SelectedProcess = fakeProcesses[0]
        };

        await InvokePrivateAsync(viewModel, "KillProcessAsync");

        _ = platformService.Received(1).KillProcessAsync(1001, Arg.Any<CancellationToken>());
        _ = platformService.Received(1).GetProcessesAsync(Arg.Any<CancellationToken>());
    }

    [AvaloniaFact]
    public async Task KillProcessAsync_FailResult_StatusMessageContainsFailed()
    {
        var fakeProcesses = CreateProcessList((1001, "alpha"));
        var platformService = CreatePlatformService(fakeProcesses);
        platformService.KillProcessAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<ResultUnit, ProcessError>.Fail(ProcessError.Unknown)));

        var viewModel = new MainWindowViewModel(platformService)
        {
            SelectedProcess = fakeProcesses[0]
        };

        await InvokePrivateAsync(viewModel, "KillProcessAsync");

        viewModel.StatusMessage.Should().Contain("FAILED");
    }

    private static IReadOnlyList<ProcessInfo> CreateProcessList(params (int Pid, string Name)[] descriptors)
    {
        return descriptors
            .Select(descriptor => ProcessInfo.Create(
                descriptor.Pid,
                descriptor.Name,
                cpuPercent: 10,
                memoryBytes: 64 * 1024,
                status: "Running",
                user: "tester",
                startTime: DateTime.UtcNow.AddMinutes(-5)))
            .ToArray();
    }

    private static IPlatformService CreatePlatformService(IReadOnlyList<ProcessInfo>? processes = null)
    {
        var fakeProcesses = processes ?? Array.Empty<ProcessInfo>();

        var platformService = Substitute.For<IPlatformService>();
        platformService.PlatformName.Returns("TEST");
        platformService.GetProcessesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<IReadOnlyList<ProcessInfo>, string>.Ok(fakeProcesses)));
        platformService.GetSystemMetricsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                new SystemMetrics
                {
                    CpuUsagePercent = 25,
                    ProcessCount = fakeProcesses.Count,
                    TotalMemoryBytes = 8L * 1024 * 1024 * 1024,
                    UsedMemoryBytes = 4L * 1024 * 1024 * 1024,
                    AvailableMemoryBytes = 4L * 1024 * 1024 * 1024,
                    ThreadCount = 100,
                    Uptime = TimeSpan.FromHours(3)
                }));
        platformService.KillProcessAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<ResultUnit, ProcessError>.Ok(ResultUnit.Value)));

        return platformService;
    }

    private static async Task InvokePrivateAsync(MainWindowViewModel viewModel, string methodName)
    {
        var method = typeof(MainWindowViewModel).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var result = method!.Invoke(viewModel, null);
        result.Should().BeAssignableTo<Task>();

        var task = (Task)result!;
        await task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
    }

    private static void CancelPendingSearchRefresh(MainWindowViewModel viewModel)
    {
        var field = typeof(MainWindowViewModel).GetField("_searchRefreshCts", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(viewModel) is not CancellationTokenSource cts)
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
        field.SetValue(viewModel, null);
    }

    private static void SetSearchTextWithoutDebounce(MainWindowViewModel viewModel, string searchText)
    {
        var field = typeof(MainWindowViewModel).GetField("_searchText", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        field!.SetValue(viewModel, searchText);

        CancelPendingSearchRefresh(viewModel);
    }
}
