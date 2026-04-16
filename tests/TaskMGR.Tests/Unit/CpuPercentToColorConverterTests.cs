using Avalonia.Media;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using TaskMGR.UI.Converters;

namespace TaskMGR.Tests.Unit;

public sealed class CpuPercentToColorConverterTests
{
    [AvaloniaFact]
    public void Convert_UsesExpectedBrushByThreshold()
    {
        var testCases = new (double CpuPercent, int ExpectedBrushIndex)[]
        {
            (0d, 0),
            (19.9d, 0),
            (20d, 1),
            (50d, 1),
            (50.1d, 2),
            (100d, 2)
        };

        foreach (var (cpuPercent, expectedBrushIndex) in testCases)
        {
            var converter = new CpuPercentToColorConverter();
            var primary = new SolidColorBrush(Colors.Lime);
            var warning = new SolidColorBrush(Colors.Gold);
            var danger = new SolidColorBrush(Colors.OrangeRed);

            var values = new List<object?> { cpuPercent, primary, warning, danger };
            var result = converter.Convert(values, typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture);

            var expected = expectedBrushIndex switch
            {
                0 => primary,
                1 => warning,
                _ => danger
            };

            result.Should().BeSameAs(expected);
        }
    }
}
