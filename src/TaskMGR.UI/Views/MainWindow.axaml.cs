using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;
using TaskMGR.Core.Constants;
using TaskMGR.UI.Services;
using TaskMGR.UI.ViewModels;

namespace TaskMGR.UI;

public partial class MainWindow : Window
{
    private const int DwmwaCaptionColor = 35;
    private const int TerminalCaptionColor = 0x000A0C0A;

    private readonly MainWindowViewModel _viewModel;
    private readonly ProcessRefreshService _refreshService;
    private Task _startupTask = Task.CompletedTask;

    public MainWindow()
    {
        InitializeComponent();

        var platformService = PlatformServiceFactory.Create();
        _viewModel = new MainWindowViewModel(platformService);
        _refreshService = new ProcessRefreshService(platformService, RefreshIntervals.Default);

        DataContext = _viewModel;

        Opened += OnOpened;
        Closed += OnClosed;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _startupTask = _viewModel.StartAsync(_refreshService.Updates);
        _refreshService.Start();
        ApplyWindowsCaptionTint();
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        Closed -= OnClosed;

        await _startupTask;
        await _viewModel.StopAsync();
        await _refreshService.DisposeAsync();
    }

    private void ApplyWindowsCaptionTint()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var platformHandle = TryGetPlatformHandle();
        if (platformHandle is null || platformHandle.Handle == IntPtr.Zero)
        {
            return;
        }

        var color = TerminalCaptionColor;
        _ = DwmSetWindowAttribute(platformHandle.Handle, DwmwaCaptionColor, ref color, Marshal.SizeOf<int>());
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int attributeSize);
}
