using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace TaskMGR.UI.Converters;

public sealed class BytesToStringConverter : IValueConverter
{
    public static string FormatBytes(long bytes)
    {
        const double kilobyte = 1024d;
        const double gigabyte = 1024d * 1024 * 1024;
        const double megabyte = 1024d * 1024;

        var safeBytes = Math.Max(0, bytes);

        if (safeBytes < kilobyte)
        {
            return $"{safeBytes} B";
        }

        if (safeBytes < megabyte)
        {
            return FormatSize(safeBytes / kilobyte, "KB");
        }

        if (safeBytes >= gigabyte)
        {
            return FormatSize(safeBytes / gigabyte, "GB");
        }

        return FormatSize(safeBytes / megabyte, "MB");
    }

    private static string FormatSize(double value, string unit)
    {
        var format = Math.Abs(value % 1d) < double.Epsilon ? "0" : "0.0";
        return $"{value.ToString(format, CultureInfo.InvariantCulture)} {unit}";
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes)
        {
            return FormatBytes(0);
        }

        return FormatBytes(bytes);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
