using System;
using Avalonia.Controls;
using TaskMGR.UI.Services;
using TaskMGR.UI.ViewModels;

namespace TaskMGR.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        var platformService = PlatformServiceFactory.Create();
        var viewModel = new MainWindowViewModel(platformService);
        DataContext = viewModel;
        
        // Start auto-refresh with 2 second interval
        _ = viewModel.StartAutoRefreshAsync(TimeSpan.FromSeconds(2));
    }
}