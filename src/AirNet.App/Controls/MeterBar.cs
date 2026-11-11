using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AirNet.App.Features;

namespace AirNet.App.Controls;

/// <summary>Rounded 0..1 meter used for scenario scores and per-hop latency.</summary>
public class MeterBar : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<MeterBar, double>(nameof(Value));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<MeterBar, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<MeterBar, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<double> MinFillProperty =
        AvaloniaProperty.Register<MeterBar, double>(nameof(MinFill), 0.04);

    static MeterBar()
    {
        AffectsRender<MeterBar>(ValueProperty, TrackBrushProperty, FillBrushProperty, MinFillProperty);
    }

    public MeterBar()
    {
        if (Motion.Enabled)
        {
            Transitions = new Avalonia.Animation.Transitions
            {
                new Avalonia.Animation.DoubleTransition
                {
                    Property = ValueProperty,
                    Duration = TimeSpan.FromMilliseconds(520),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                },
            };
        }
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public double MinFill
    {
        get => GetValue(MinFillProperty);
        set => SetValue(MinFillProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 1 || height <= 1)
        {
            return;
        }

        var radius = new CornerRadius(height / 2);

        if (TrackBrush is { } track)
        {
            context.DrawRectangle(track, null, new RoundedRect(new Rect(0, 0, width, height), radius));
        }

        var ratio = Math.Clamp(Value, 0, 1);
        if (ratio <= 0 || FillBrush is not { } fill)
        {
            return;
        }

        var fillWidth = Math.Max(width * ratio, Math.Max(MinFill * width, height));
        fillWidth = Math.Min(fillWidth, width);
        context.DrawRectangle(fill, null, new RoundedRect(new Rect(0, 0, fillWidth, height), radius));
    }
}
