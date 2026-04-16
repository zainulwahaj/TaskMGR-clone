using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using TaskMGR.UI;

[assembly: AvaloniaTestApplication(typeof(TaskMGR.Tests.TestInfrastructure.AvaloniaTestAppBuilder))]

namespace TaskMGR.Tests.TestInfrastructure;

public static class AvaloniaTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
