using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AirNet.App.Controls;

/// <summary>Four ascending bars, filled from the left, for radio strength.</summary>
public class SignalBars : Control
{
    private static readonly double[] BarHeights = [4, 7, 10, 13];

    public static readonly StyledProperty<double> PercentProperty =
        AvaloniaProperty.Register<SignalBars, double>(nameof(Percent));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<SignalBars, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> EmptyBrushProperty =
        AvaloniaProperty.Register<SignalBars, IBrush?>(nameof(EmptyBrush));

    static SignalBars()
    {
        AffectsRender<SignalBars>(PercentProperty, FillBrushProperty, EmptyBrushProperty);
    }

    public double Percent
    {
        get => GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public IBrush? FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public IBrush? EmptyBrush
    {
        get => GetValue(EmptyBrushProperty);
        set => SetValue(EmptyBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 4 || Bounds.Height <= 4 || FillBrush is not { } fill)
        {
            return;
        }

        const double barWidth = 3;
        const double gap = 2;
        var filled = (int)Math.Round(Math.Clamp(Percent, 0, 100) / 25);
        var baseline = Bounds.Height;

        for (var i = 0; i < BarHeights.Length; i++)
        {
            var height = BarHeights[i];
            var x = i * (barWidth + gap);
            var rect = new Rect(x, baseline - height, barWidth, height);
            var brush = i < filled ? fill : EmptyBrush;
            if (brush is null)
            {
                continue;
            }

            context.DrawRectangle(brush, null, new RoundedRect(rect, new CornerRadius(1.2)));
        }
    }
}
