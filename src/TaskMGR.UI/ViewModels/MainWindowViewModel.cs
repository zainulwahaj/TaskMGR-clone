using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskMGR.Core.Interfaces;
using TaskMGR.Core.Models;

namespace TaskMGR.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IPlatformService _platformService;
    private CancellationTokenSource? _refreshCts;

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
    private string _statusMessage = "Ready";

    public MainWindowViewModel(IPlatformService platformService)
    {
        _platformService = platformService;
    }

    public string PlatformName => _platformService.PlatformName;

    public string MemoryUsageText => SystemMetrics.TotalMemoryBytes > 0
        ? $"{FormatBytes(SystemMetrics.UsedMemoryBytes)} / {FormatBytes(SystemMetrics.TotalMemoryBytes)}"
        : "Loading...";

    public double MemoryUsagePercent => SystemMetrics.TotalMemoryBytes > 0
        ? (double)SystemMetrics.UsedMemoryBytes / SystemMetrics.TotalMemoryBytes * 100
        : 0;

    public string UptimeText => SystemMetrics.Uptime.TotalSeconds > 0
        ? $"{(int)SystemMetrics.Uptime.TotalDays}d {SystemMetrics.Uptime.Hours}h {SystemMetrics.Uptime.Minutes}m"
        : "Loading...";

    partial void OnSystemMetricsChanged(SystemMetrics value)
    {
        OnPropertyChanged(nameof(MemoryUsageText));
        OnPropertyChanged(nameof(MemoryUsagePercent));
        OnPropertyChanged(nameof(UptimeText));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        StatusMessage = "Refreshing...";

        try
        {
            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();

            var processTask = _platformService.GetProcessesAsync(_refreshCts.Token);
            var metricsTask = _platformService.GetSystemMetricsAsync(_refreshCts.Token);

            await Task.WhenAll(processTask, metricsTask);

            var processes = await processTask;
            SystemMetrics = await metricsTask;

            var filtered = string.IsNullOrWhiteSpace(SearchText)
                ? processes
                : processes.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

            Processes = new ObservableCollection<ProcessInfo>(filtered.OrderByDescending(p => p.CpuPercent));
            StatusMessage = $"{Processes.Count} processes | CPU: {SystemMetrics.CpuUsagePercent:F1}%";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Refresh cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task KillProcessAsync()
    {
        if (SelectedProcess == null) return;

        var pid = SelectedProcess.Pid;
        var name = SelectedProcess.Name;

        try
        {
            var result = await _platformService.KillProcessAsync(pid);
            StatusMessage = result ? $"Terminated: {name} ({pid})" : $"Failed to terminate: {name}";
            
            if (result)
                await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    public async Task StartAutoRefreshAsync(TimeSpan interval)
    {
        await RefreshAsync();

        while (true)
        {
            await Task.Delay(interval);
            await RefreshAsync();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = RefreshAsync();
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
