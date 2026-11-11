using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AirNet.App.Controls;

/// <summary>
/// A 270° gauge arc with rounded caps. The value text is laid over it in XAML so the same
/// control serves the health score, the speed gauges and the stat rings without variants.
/// </summary>
public class ScoreRing : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ScoreRing, double>(nameof(Value));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<ScoreRing, double>(nameof(Maximum), 100d);

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<ScoreRing, double>(nameof(Thickness), 9d);

    public static readonly StyledProperty<double> StartAngleProperty =
        AvaloniaProperty.Register<ScoreRing, double>(nameof(StartAngle), -90d);

    public static readonly StyledProperty<double> SweepAngleProperty =
        AvaloniaProperty.Register<ScoreRing, double>(nameof(SweepAngle), 360d);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<ScoreRing, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> ProgressBrushProperty =
        AvaloniaProperty.Register<ScoreRing, IBrush?>(nameof(ProgressBrush));

    public static readonly StyledProperty<bool> ShowTipProperty =
        AvaloniaProperty.Register<ScoreRing, bool>(nameof(ShowTip));

    static ScoreRing()
    {
        AffectsRender<ScoreRing>(
            ValueProperty, MaximumProperty, ThicknessProperty, StartAngleProperty,
            SweepAngleProperty, TrackBrushProperty, ProgressBrushProperty, ShowTipProperty);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public double StartAngle
    {
        get => GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    public double SweepAngle
    {
        get => GetValue(SweepAngleProperty);
        set => SetValue(SweepAngleProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? ProgressBrush
    {
        get => GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    public bool ShowTip
    {
        get => GetValue(ShowTipProperty);
        set => SetValue(ShowTipProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 4)
        {
            return;
        }

        var thickness = Math.Min(Thickness, size / 3);
        var radius = (size - thickness) / 2 - 0.5;
        if (radius <= 1)
        {
            return;
        }

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var sweep = Math.Clamp(SweepAngle, 0, 359.99);

        if (TrackBrush is { } track)
        {
            // The track is capped flat: a round cap would poke out past the progress arc and
            // read as a detached dot when the ring is nearly full.
            DrawArc(context, center, radius, StartAngle, sweep, track, thickness, PenLineCap.Flat);
        }

        var ratio = Maximum <= 0 ? 0 : Math.Clamp(Value / Maximum, 0, 1);
        if (ProgressBrush is { } progress && ratio > 0)
        {
            var progressSweep = sweep * ratio;
            if (progressSweep > 0.4)
            {
                DrawArc(context, center, radius, StartAngle, progressSweep, progress, thickness, PenLineCap.Round);

                if (ShowTip)
                {
                    var tip = PointAt(center, radius, StartAngle + progressSweep);
                    context.DrawEllipse(progress, null, tip, thickness * 0.5 - 1, thickness * 0.5 - 1);
                }
            }
        }
    }

    private static void DrawArc(
        DrawingContext context,
        Point center,
        double radius,
        double startAngle,
        double sweep,
        IBrush brush,
        double thickness,
        PenLineCap cap)
    {
        var pen = new Pen(brush, thickness, lineCap: cap);

        if (sweep >= 359.9)
        {
            context.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        var start = PointAt(center, radius, startAngle);
        var end = PointAt(center, radius, startAngle + sweep);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false);
            ctx.ArcTo(end, new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private static Point PointAt(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180;
        return new Point(
            center.X + (radius * Math.Cos(radians)),
            center.Y + (radius * Math.Sin(radians)));
    }
}
