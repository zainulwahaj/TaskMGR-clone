using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace TaskMGR.UI.Converters;

public sealed class CpuPercentToColorConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 4 || values[0] is not double cpuPercent)
        {
            return values.Count > 1 && values[1] is IBrush fallbackBrush
                ? fallbackBrush
                : Brushes.White;
        }

        var primary = values[1] as IBrush ?? Brushes.White;
        var warning = values[2] as IBrush ?? primary;
        var danger = values[3] as IBrush ?? warning;

        return cpuPercent switch
        {
            < 20 => primary,
            <= 50 => warning,
            _ => danger
        };
    }
}
