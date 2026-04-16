using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskMGR.Core.Interfaces;
using TaskMGR.Core.Models;
using TaskMGR.Core.Results;
using TaskMGR.UI.Converters;
using TaskMGR.UI.Services;

namespace TaskMGR.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private const int CpuHistoryLength = 20;
    private static readonly TimeSpan SearchRefreshDebounce = TimeSpan.FromMilliseconds(200);

    private readonly IPlatformService _platformService;
    private readonly DispatcherTimer _clockTimer;
    private readonly Dictionary<int, Queue<double>> _cpuHistory = new();
    private IReadOnlyList<ProcessInfo> _latestProcesses = Array.Empty<ProcessInfo>();
    private CancellationTokenSource? _refreshCts;
    private CancellationTokenSource? _updatesCts;
    private CancellationTokenSource? _searchRefreshCts;
    private Task? _updatesTask;
    private bool _showClockSeparators = true;

    [ObservableProperty]
    private ObservableCollection<ProcessInfo> _processes = new();

    [ObservableProperty]
    private ProcessInfo? _selectedProcess;

    [ObservableProperty]
    private SystemMetrics _systemMetrics = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusTimestampText = string.Empty;

    [ObservableProperty]
    private string _statusLineText = string.Empty;

    [ObservableProperty]
    private string _uptimeTickerText = "00:00:00";

    [ObservableProperty]
    private string _windowTitle = "TASKMGR V1.0 — DESIGN";

    [ObservableProperty]
    private bool _isAutoRefreshActive;

    [ObservableProperty]
    private StatusState _currentStatus = new StatusState.Idle();

    public MainWindowViewModel()
        : this(new DesignTimePlatformService())
    {
    }

    public MainWindowViewModel(IPlatformService platformService)
    {
        _platformService = platformService;
        _clockTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, OnClockTick);

        UpdateWindowTitle();
        UpdateClockState();
        UpdateStatusLine();
    }

    public string PlatformName => _platformService.PlatformName.ToUpperInvariant();

    public string StatusMessage => CurrentStatus switch
    {
        StatusState.Idle => "LIVE FEED NOMINAL",
        StatusState.Loading => "STREAM SYNCHRONIZING",
        StatusState.Error error => $"ERROR {error.Message.ToUpperInvariant()}",
        StatusState.Refreshed refreshed => $"TRACKING {refreshed.ProcessCount} DISPLAYED / CPU {refreshed.CpuPercent:0.0}%",
        _ => "STATUS UNKNOWN"
    };

    public bool IsSearchHintVisible => string.IsNullOrWhiteSpace(SearchText);

    public string MemoryUsageText => SystemMetrics.TotalMemoryBytes > 0
        ? $"{BytesToStringConverter.FormatBytes(SystemMetrics.UsedMemoryBytes)} / {BytesToStringConverter.FormatBytes(SystemMetrics.TotalMemoryBytes)}"
        : $"{BytesToStringConverter.FormatBytes(0)} / {BytesToStringConverter.FormatBytes(0)}";

    public double MemoryUsagePercent => SystemMetrics.TotalMemoryBytes > 0
        ? (double)SystemMetrics.UsedMemoryBytes / SystemMetrics.TotalMemoryBytes * 100
        : 0;

    public double CpuMeterValue => Math.Clamp(SystemMetrics.CpuUsagePercent, 0, 100);

    public string CpuMeterText => $"{SystemMetrics.CpuUsagePercent:00.0}%";

    public double MemoryMeterValue => Math.Clamp(MemoryUsagePercent, 0, 100);

    public string MemoryMeterText => $"{MemoryUsagePercent:00.0}%";

    public double ProcessMeterValue => Math.Clamp(SystemMetrics.ProcessCount / 999d * 100d, 0, 100);

    public string ProcessMeterText => SystemMetrics.ProcessCount > 999
        ? "999+"
        : SystemMetrics.ProcessCount.ToString("000");

    public Task StartAsync(ChannelReader<RefreshResult> updates, CancellationToken cancellationToken = default)
    {
        if (_updatesTask is not null)
        {
            return Task.CompletedTask;
        }

        _updatesCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsAutoRefreshActive = true;
        _clockTimer.Start();
        _updatesTask = ConsumeUpdatesAsync(updates, _updatesCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;

        _searchRefreshCts?.Cancel();
        _searchRefreshCts?.Dispose();
        _searchRefreshCts = null;

        if (_updatesCts is not null)
        {
            _updatesCts.Cancel();

            if (_updatesTask is not null)
            {
                try
                {
                    await _updatesTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_updatesCts.IsCancellationRequested)
                {
                }
            }

            _updatesCts.Dispose();
            _updatesCts = null;
            _updatesTask = null;
        }

        IsAutoRefreshActive = false;
        _clockTimer.Stop();
        UpdateStatusLine();
    }

    partial void OnSystemMetricsChanged(SystemMetrics value)
    {
        OnPropertyChanged(nameof(MemoryUsageText));
        OnPropertyChanged(nameof(MemoryUsagePercent));
        OnPropertyChanged(nameof(CpuMeterValue));
        OnPropertyChanged(nameof(CpuMeterText));
        OnPropertyChanged(nameof(MemoryMeterValue));
        OnPropertyChanged(nameof(MemoryMeterText));
        OnPropertyChanged(nameof(ProcessMeterValue));
        OnPropertyChanged(nameof(ProcessMeterText));
        UpdateClockState();
        UpdateStatusLine();
    }

    partial void OnCurrentStatusChanged(StatusState value)
    {
        OnPropertyChanged(nameof(StatusMessage));
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearchHintVisible));
        ApplyProcessFilter();
        ScheduleSearchRefresh();
    }

    partial void OnStatusTimestampTextChanged(string value) => UpdateStatusLine();

    partial void OnIsAutoRefreshActiveChanged(bool value) => UpdateStatusLine();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsLoading = true;
            CurrentStatus = new StatusState.Loading();
        });

        try
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = new CancellationTokenSource();
            var refreshToken = _refreshCts.Token;

            var result = await Task.Run(
                async () =>
                {
                    var processesResult = await _platformService.GetProcessesAsync(refreshToken).ConfigureAwait(false);
                    if (!processesResult.IsSuccess)
                    {
                        return RefreshResult.FromError(processesResult.Error);
                    }

                    var metrics = await _platformService.GetSystemMetricsAsync(refreshToken).ConfigureAwait(false);
                    return new RefreshResult(processesResult.Value, metrics);
                },
                refreshToken).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() => ApplyRefreshResult(result));
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() => CurrentStatus = new StatusState.Idle());
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => CurrentStatus = new StatusState.Error(ex.Message));
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private async Task KillProcessAsync()
    {
        if (SelectedProcess is null)
        {
            return;
        }

        var pid = SelectedProcess.Pid;
        var name = SelectedProcess.Name;

        try
        {
            var result = await _platformService.KillProcessAsync(pid).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentStatus = result.IsSuccess
                    ? new StatusState.Refreshed(Processes.Count, SystemMetrics.CpuUsagePercent)
                    : new StatusState.Error(FormatProcessError(result.Error, pid, name));
            });

            if (result.IsSuccess)
            {
                await RefreshAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => CurrentStatus = new StatusState.Error(ex.Message));
        }
    }

    private async Task ConsumeUpdatesAsync(ChannelReader<RefreshResult> updates, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var result in updates.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await Dispatcher.UIThread.InvokeAsync(() => ApplyRefreshResult(result));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void ApplyRefreshResult(RefreshResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            CurrentStatus = new StatusState.Error(result.ErrorMessage);
            return;
        }

        UpdateCpuHistory(result.Processes);
        _latestProcesses = result.Processes;
        SystemMetrics = result.SystemMetrics with { ProcessCount = result.Processes.Count };

        ApplyProcessFilter();
    }

    private void ApplyProcessFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _latestProcesses
            : _latestProcesses
                .Where(process => process.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        var ordered = filtered
            .OrderByDescending(process => process.CpuPercent)
            .ThenBy(process => process.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        MergeProcesses(ordered);
        CurrentStatus = Processes.Count == 0 && _latestProcesses.Count == 0
            ? new StatusState.Idle()
            : new StatusState.Refreshed(Processes.Count, SystemMetrics.CpuUsagePercent);
        UpdateStatusLine();
    }

    private void MergeProcesses(IReadOnlyList<ProcessInfo> latest)
    {
        var selectedPid = SelectedProcess?.Pid;
        var latestPids = latest.Select(process => process.Pid).ToHashSet();

        for (var index = Processes.Count - 1; index >= 0; index--)
        {
            if (!latestPids.Contains(Processes[index].Pid))
            {
                Processes.RemoveAt(index);
            }
        }

        var indexByPid = BuildIndexMap();

        foreach (var process in latest)
        {
            if (indexByPid.TryGetValue(process.Pid, out var existingIndex))
            {
                Processes[existingIndex].UpdateFrom(process);
                continue;
            }

            Processes.Add(process.Clone());
            indexByPid[process.Pid] = Processes.Count - 1;
        }

        for (var targetIndex = 0; targetIndex < latest.Count; targetIndex++)
        {
            indexByPid = BuildIndexMap();

            if (!indexByPid.TryGetValue(latest[targetIndex].Pid, out var currentIndex) || currentIndex == targetIndex)
            {
                continue;
            }

            Processes.Move(currentIndex, targetIndex);
        }

        SelectedProcess = selectedPid.HasValue
            ? Processes.FirstOrDefault(process => process.Pid == selectedPid.Value)
            : null;
    }

    private void UpdateCpuHistory(IReadOnlyList<ProcessInfo> processes)
    {
        var activePids = processes.Select(process => process.Pid).ToHashSet();
        var stalePids = _cpuHistory.Keys.Where(pid => !activePids.Contains(pid)).ToArray();

        foreach (var stalePid in stalePids)
        {
            _cpuHistory.Remove(stalePid);
        }

        foreach (var process in processes)
        {
            if (!_cpuHistory.TryGetValue(process.Pid, out var history))
            {
                history = new Queue<double>(CpuHistoryLength);
                _cpuHistory[process.Pid] = history;
            }

            if (history.Count == CpuHistoryLength)
            {
                history.Dequeue();
            }

            history.Enqueue(Math.Clamp(process.CpuPercent, 0, 100));
            process.SetCpuHistorySamples(history.ToArray());
        }
    }

    private Dictionary<int, int> BuildIndexMap()
    {
        var indexByPid = new Dictionary<int, int>(Processes.Count);

        for (var index = 0; index < Processes.Count; index++)
        {
            indexByPid[Processes[index].Pid] = index;
        }

        return indexByPid;
    }

    private void ScheduleSearchRefresh()
    {
        _searchRefreshCts?.Cancel();
        _searchRefreshCts?.Dispose();

        _searchRefreshCts = new CancellationTokenSource();
        var token = _searchRefreshCts.Token;

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(SearchRefreshDebounce, token).ConfigureAwait(false);
                    await RefreshAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
            },
            token);
    }

    private void OnClockTick(object? sender, EventArgs e)
    {
        _showClockSeparators = !_showClockSeparators;
        UpdateClockState();
    }

    private void UpdateClockState()
    {
        StatusTimestampText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        UptimeTickerText = FormatUptimeTicker(SystemMetrics.Uptime, _showClockSeparators);
    }

    private void UpdateStatusLine()
    {
        StatusLineText =
            $"[ {PlatformName} ]  {SystemMetrics.ProcessCount} PROC  |  CPU {SystemMetrics.CpuUsagePercent:0.0}%  |  {StatusTimestampText}";
    }

    private void UpdateWindowTitle()
    {
        WindowTitle = $"TASKMGR V1.0 — {PlatformName}";
    }

    private static string FormatProcessError(ProcessError error, int pid, string name) =>
        error switch
        {
            ProcessError.NotFound => $"FAILED TO TERMINATE {name} [{pid}] - PROCESS NOT FOUND",
            ProcessError.AccessDenied => $"FAILED TO TERMINATE {name} [{pid}] - ACCESS DENIED",
            _ => $"FAILED TO TERMINATE {name} [{pid}]"
        };

    private static string FormatUptimeTicker(TimeSpan uptime, bool showSeparators)
    {
        var separator = showSeparators ? ":" : " ";
        var days = Math.Max(0, (int)uptime.TotalDays);
        return $"{days:00}{separator}{uptime.Hours:00}{separator}{uptime.Minutes:00}";
    }

    private sealed class DesignTimePlatformService : IPlatformService
    {
        public string PlatformName => "Design";

        public Task<Result<IReadOnlyList<ProcessInfo>, string>> GetProcessesAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProcessInfo> processes =
            [
                ProcessInfo.Create(
                    101,
                    "TASKMGR",
                    3.1,
                    96 * 1024 * 1024,
                    "RUNNING",
                    Environment.UserName,
                    DateTime.Now.AddMinutes(-15))
            ];

            processes[0].SetCpuHistorySamples(new[] { 1d, 4d, 3d, 6d, 5d, 3.1d });
            return Task.FromResult(Result<IReadOnlyList<ProcessInfo>, string>.Ok(processes));
        }

        public Task<ProcessInfo?> GetProcessByIdAsync(int pid, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProcessInfo?>(null);

        public Task<Result<Unit, ProcessError>> KillProcessAsync(int pid, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<Unit, ProcessError>.Fail(ProcessError.Unknown));

        public Task<SystemMetrics> GetSystemMetricsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new SystemMetrics
                {
                    CpuUsagePercent = 12.4,
                    TotalMemoryBytes = 16L * 1024 * 1024 * 1024,
                    UsedMemoryBytes = 8L * 1024 * 1024 * 1024,
                    AvailableMemoryBytes = 8L * 1024 * 1024 * 1024,
                    ProcessCount = 1,
                    ThreadCount = 12,
                    Uptime = TimeSpan.FromHours(54)
                });
    }
}
