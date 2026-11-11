using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AirNet.App.Features;

namespace AirNet.App.Controls;

/// <summary>
/// Latency timeline: filled area, 2 px line, optional horizontal guides and a marker on the
/// newest sample. Drawn directly instead of pulling in a charting package, which matters for a
/// product whose first promise is that it stays light.
/// </summary>
public class Sparkline : Control
{
    private const double Inset = 4;

    private readonly DispatcherTimer _revealTimer;
    private double _reveal = 1;
    private double _revealElapsed;

    public Sparkline()
    {
        _revealTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnRevealTick);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SeriesProperty)
        {
            StartReveal();
        }
    }

    /// <summary>Sweeps the line in from the left so a new measurement reads as "just measured".</summary>
    private void StartReveal()
    {
        if (!Motion.Enabled || !AnimateReveal)
        {
            _reveal = 1;
            _revealTimer.Stop();
            InvalidateVisual();
            return;
        }

        _reveal = 0;
        _revealElapsed = 0;
        _revealTimer.Start();
    }

    private void OnRevealTick(object? sender, EventArgs e)
    {
        _revealElapsed += 16;
        _reveal = Math.Clamp(_revealElapsed / Motion.Reveal.TotalMilliseconds, 0, 1);
        if (_reveal >= 1)
        {
            _revealTimer.Stop();
        }

        InvalidateVisual();
    }

    public static readonly StyledProperty<IReadOnlyList<double>?> SeriesProperty =
        AvaloniaProperty.Register<Sparkline, IReadOnlyList<double>?>(nameof(Series));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<Sparkline, double>(nameof(StrokeThickness), 2d);

    public static readonly StyledProperty<bool> ShowGuidesProperty =
        AvaloniaProperty.Register<Sparkline, bool>(nameof(ShowGuides));

    public static readonly StyledProperty<double> GuideValueProperty =
        AvaloniaProperty.Register<Sparkline, double>(nameof(GuideValue), double.NaN);

    public static readonly StyledProperty<IBrush?> GuideBrushProperty =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(GuideBrush));

    public static readonly StyledProperty<int> GridLinesProperty =
        AvaloniaProperty.Register<Sparkline, int>(nameof(GridLines));

    public static readonly StyledProperty<IBrush?> GridLineBrushProperty =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(GridLineBrush));

    public static readonly StyledProperty<bool> ShowEndDotProperty =
        AvaloniaProperty.Register<Sparkline, bool>(nameof(ShowEndDot), true);

    /// <summary>
    /// When greater than the series length, the line grows into a fixed slot count instead of
    /// stretching across the whole width. Used while samples are still arriving.
    /// </summary>
    public static readonly StyledProperty<int> CapacityProperty =
        AvaloniaProperty.Register<Sparkline, int>(nameof(Capacity));

    /// <summary>
    /// False while samples are streaming in: the line should extend, not sweep in from the left
    /// again on every point.
    /// </summary>
    public static readonly StyledProperty<bool> AnimateRevealProperty =
        AvaloniaProperty.Register<Sparkline, bool>(nameof(AnimateReveal), true);

    static Sparkline()
    {
        AffectsRender<Sparkline>(
            SeriesProperty, StrokeProperty, FillProperty, StrokeThicknessProperty, ShowGuidesProperty,
            GuideValueProperty, GuideBrushProperty, GridLinesProperty, GridLineBrushProperty,
            ShowEndDotProperty, CapacityProperty);
    }

    public IReadOnlyList<double>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public bool ShowGuides
    {
        get => GetValue(ShowGuidesProperty);
        set => SetValue(ShowGuidesProperty, value);
    }

    public double GuideValue
    {
        get => GetValue(GuideValueProperty);
        set => SetValue(GuideValueProperty, value);
    }

    public IBrush? GuideBrush
    {
        get => GetValue(GuideBrushProperty);
        set => SetValue(GuideBrushProperty, value);
    }

    public int GridLines
    {
        get => GetValue(GridLinesProperty);
        set => SetValue(GridLinesProperty, value);
    }

    public IBrush? GridLineBrush
    {
        get => GetValue(GridLineBrushProperty);
        set => SetValue(GridLineBrushProperty, value);
    }

    public bool ShowEndDot
    {
        get => GetValue(ShowEndDotProperty);
        set => SetValue(ShowEndDotProperty, value);
    }

    public bool AnimateReveal
    {
        get => GetValue(AnimateRevealProperty);
        set => SetValue(AnimateRevealProperty, value);
    }

    public int Capacity
    {
        get => GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var series = Series;
        if (series is null || series.Count < 2 || Bounds.Width < 8 || Bounds.Height < 8)
        {
            return;
        }

        var left = Inset;
        var top = Inset;
        var width = Bounds.Width - (Inset * 2);
        var height = Bounds.Height - (Inset * 2);
        if (width <= 1 || height <= 1)
        {
            return;
        }

        double min = double.MaxValue;
        double max = double.MinValue;
        foreach (var value in series)
        {
            if (double.IsNaN(value))
            {
                continue;
            }

            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        if (min > max)
        {
            return;
        }

        // A flat series should still sit mid-height rather than collapse onto the baseline.
        var span = max - min;
        if (span < 1)
        {
            min -= 0.5;
            max += 0.5;
            span = max - min;
        }
        else
        {
            min -= span * 0.12;
            max += span * 0.12;
            span = max - min;
        }

        double MapY(double value) => top + (height * (1 - ((value - min) / span)));

        if (GridLines > 0 && GridLineBrush is { } gridBrush)
        {
            var gridPen = new Pen(gridBrush, 1);
            for (var i = 1; i < GridLines; i++)
            {
                var y = top + (height * i / GridLines);
                context.DrawLine(gridPen, new Point(left, y), new Point(left + width, y));
            }
        }

        if (ShowGuides && !double.IsNaN(GuideValue) && GuideBrush is { } guideBrush)
        {
            var guideY = MapY(Math.Clamp(GuideValue, min, max));
            var guidePen = new Pen(guideBrush, 1, new DashStyle([3, 4], 0));
            context.DrawLine(guidePen, new Point(left, guideY), new Point(left + width, guideY));
        }

        var step = width / (Math.Max(Capacity, series.Count) - 1);
        var visible = Math.Clamp((int)Math.Ceiling(series.Count * _reveal), 2, series.Count);
        var points = new Point[visible];
        for (var i = 0; i < visible; i++)
        {
            points[i] = new Point(left + (step * i), MapY(series[i]));
        }

        if (Fill is { } fillBrush)
        {
            var area = new StreamGeometry();
            using (var ctx = area.Open())
            {
                ctx.BeginFigure(new Point(points[0].X, top + height), true);
                foreach (var point in points)
                {
                    ctx.LineTo(point);
                }

                ctx.LineTo(new Point(points[^1].X, top + height));
                ctx.EndFigure(true);
            }

            context.DrawGeometry(fillBrush, null, area);
        }

        if (Stroke is { } strokeBrush)
        {
            var line = new StreamGeometry();
            using (var ctx = line.Open())
            {
                ctx.BeginFigure(points[0], false);
                for (var i = 1; i < points.Length; i++)
                {
                    ctx.LineTo(points[i]);
                }

                ctx.EndFigure(false);
            }

            var pen = new Pen(strokeBrush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            context.DrawGeometry(null, pen, line);

            if (ShowEndDot && _reveal >= 0.99)
            {
                var last = points[^1];
                context.DrawEllipse(strokeBrush, null, last, 3.2, 3.2);
            }
        }
    }
}
