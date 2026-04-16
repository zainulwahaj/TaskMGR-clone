using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace TaskMGR.UI.Controls;

public sealed partial class ArcMeterControl : UserControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ArcMeterControl, double>(nameof(Value));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcMeterControl, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> DisplayTextProperty =
        AvaloniaProperty.Register<ArcMeterControl, string>(nameof(DisplayText), string.Empty);

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<ArcMeterControl, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<bool> ShowArcProperty =
        AvaloniaProperty.Register<ArcMeterControl, bool>(nameof(ShowArc), true);

    private readonly TextBlock _labelBlock;
    private readonly TextBlock _valueBlock;
    private readonly Border _footerLine;

    static ArcMeterControl()
    {
        AffectsRender<ArcMeterControl>(ValueProperty, AccentBrushProperty, ShowArcProperty);
    }

    public ArcMeterControl()
    {
        InitializeComponent();

        _labelBlock = new TextBlock
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8)
        };

        _valueBlock = new TextBlock
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            FontSize = 26,
            FontWeight = FontWeight.Bold
        };

        _footerLine = new Border
        {
            Height = 1,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
            Margin = new Thickness(18, 10, 18, 0)
        };

        var middleGrid = new Grid
        {
            Children = { _valueBlock }
        };

        var footerGrid = new Grid
        {
            Children = { _footerLine }
        };

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };

        Grid.SetRow(_labelBlock, 0);
        Grid.SetRow(middleGrid, 1);
        Grid.SetRow(footerGrid, 2);

        root.Children.Add(_labelBlock);
        root.Children.Add(middleGrid);
        root.Children.Add(footerGrid);

        Content = root;

        UpdateVisualState();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string DisplayText
    {
        get => GetValue(DisplayTextProperty);
        set => SetValue(DisplayTextProperty, value);
    }

    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public bool ShowArc
    {
        get => GetValue(ShowArcProperty);
        set => SetValue(ShowArcProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (!ShowArc)
        {
            return;
        }

        var accentBrush = AccentBrush ?? ResolveBrush("AccentBrush", Color.Parse("#39ff14"));
        var borderBrush = ResolveBrush("BorderBrush", Color.Parse("#1e2e1e"));
        var mutedBrush = ResolveBrush("MutedBrush", Color.Parse("#3a4a3a"));

        var bounds = Bounds.Deflate(new Thickness(14, 22, 14, 16));
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var center = new Point(bounds.Center.X, bounds.Bottom - 4);
        var radius = Math.Max(12, Math.Min(bounds.Width / 2d, bounds.Height) - 10);
        var startAngle = Math.PI;
        var endAngle = 0d;
        var sweepAngle = startAngle + (endAngle - startAngle) * Math.Clamp(Value / 100d, 0d, 1d);

        var basePen = new Pen(borderBrush, 1);
        var activePen = new Pen(accentBrush, 2);
        var tickPen = new Pen(mutedBrush, 1);

        context.DrawGeometry(null, basePen, CreateArcGeometry(center, radius, startAngle, endAngle));
        context.DrawGeometry(null, activePen, CreateArcGeometry(center, radius, startAngle, sweepAngle));

        for (var tick = 0; tick < 5; tick++)
        {
            var ratio = tick / 4d;
            var angle = startAngle + (endAngle - startAngle) * ratio;
            var outer = PointOnCircle(center, radius + 2, angle);
            var inner = PointOnCircle(center, radius - 8, angle);
            context.DrawLine(tickPen, outer, inner);
        }

        var needleAngle = sweepAngle;
        var needleStart = PointOnCircle(center, radius - 12, needleAngle);
        var needleEnd = PointOnCircle(center, radius + 4, needleAngle);
        context.DrawLine(activePen, needleStart, needleEnd);
        context.DrawEllipse(accentBrush, null, center, 2, 2);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LabelProperty
            || change.Property == DisplayTextProperty
            || change.Property == AccentBrushProperty
            || change.Property == ShowArcProperty
            || change.Property == ValueProperty)
        {
            UpdateVisualState();
        }
    }

    private static StreamGeometry CreateArcGeometry(Point center, double radius, double startAngle, double endAngle)
    {
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(PointOnCircle(center, radius, startAngle), false);

            const int segments = 32;
            for (var index = 1; index <= segments; index++)
            {
                var angle = startAngle + ((endAngle - startAngle) * index / segments);
                ctx.LineTo(PointOnCircle(center, radius, angle));
            }

            ctx.EndFigure(false);
        }

        return geometry;
    }

    private static Point PointOnCircle(Point center, double radius, double angle) =>
        new(center.X + radius * Math.Cos(angle), center.Y - radius * Math.Sin(angle));

    private void UpdateVisualState()
    {
        _labelBlock.Text = Label.ToUpperInvariant();
        _valueBlock.Text = string.IsNullOrWhiteSpace(DisplayText)
            ? $"{Math.Round(Value):0}%"
            : DisplayText;

        var accentBrush = AccentBrush ?? ResolveBrush("AccentBrush", Color.Parse("#39ff14"));
        _valueBlock.Foreground = accentBrush;
        _footerLine.Background = ShowArc
            ? ResolveBrush("BorderBrush", Color.Parse("#1e2e1e"))
            : accentBrush;

        InvalidateVisual();
    }

    private IBrush ResolveBrush(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true && resource is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }
}
