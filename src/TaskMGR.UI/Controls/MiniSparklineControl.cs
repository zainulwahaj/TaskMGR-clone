using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace TaskMGR.UI.Controls;

public sealed partial class MiniSparklineControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> SamplesProperty =
        AvaloniaProperty.Register<MiniSparklineControl, IReadOnlyList<double>?>(nameof(Samples));

    public static readonly StyledProperty<IBrush?> StrokeBrushProperty =
        AvaloniaProperty.Register<MiniSparklineControl, IBrush?>(nameof(StrokeBrush));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<MiniSparklineControl, double>(nameof(Maximum), 100d);

    static MiniSparklineControl()
    {
        AffectsRender<MiniSparklineControl>(SamplesProperty, StrokeBrushProperty, MaximumProperty);
    }

    public IReadOnlyList<double>? Samples
    {
        get => GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    public IBrush? StrokeBrush
    {
        get => GetValue(StrokeBrushProperty);
        set => SetValue(StrokeBrushProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public MiniSparklineControl()
    {
        InitializeComponent();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds.Deflate(new Thickness(2));
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            return;
        }

        var stroke = StrokeBrush ?? ResolveBrush("AccentBrush", Color.Parse("#39ff14"));
        var border = ResolveBrush("BorderBrush", Color.Parse("#1e2e1e"));
        var samples = Samples ?? Array.Empty<double>();

        context.DrawLine(new Pen(border, 1), new Point(bounds.Left, bounds.Bottom), new Point(bounds.Right, bounds.Bottom));

        if (samples.Count == 0)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            var firstPoint = CreatePoint(bounds, samples, 0, Maximum);
            stream.BeginFigure(firstPoint, false);

            for (var index = 1; index < samples.Count; index++)
            {
                stream.LineTo(CreatePoint(bounds, samples, index, Maximum));
            }

            stream.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(stroke, 1.5), geometry);

        var tail = CreatePoint(bounds, samples, samples.Count - 1, Maximum);
        context.DrawEllipse(stroke, null, tail, 1.75, 1.75);
    }

    private static Point CreatePoint(Rect bounds, IReadOnlyList<double> samples, int index, double maximum)
    {
        var normalizedX = samples.Count <= 1 ? 1d : index / (double)(samples.Count - 1);
        var normalizedY = maximum <= 0
            ? 0
            : Math.Clamp(samples[index] / maximum, 0d, 1d);

        var x = bounds.Left + (bounds.Width * normalizedX);
        var y = bounds.Bottom - (bounds.Height * normalizedY);
        return new Point(x, y);
    }

    private IBrush ResolveBrush(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true && resource is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
