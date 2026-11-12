using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AirNet.App.Features;

namespace AirNet.App.Controls;

/// <summary>
/// The "instrument sweep": a soft band of accent light that travels down the page while a scan is
/// running. It is an overlay, so it never touches layout and never reflows a card.
/// </summary>
public class ScanSweep : Control
{
    private const double BandHeight = 150;
    private static readonly TimeSpan Period = TimeSpan.FromMilliseconds(1600);

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<ScanSweep, bool>(nameof(IsActive));

    private readonly DispatcherTimer _timer;
    private readonly LinearGradientBrush _band;
    private double _phase;

    public ScanSweep()
    {
        _band = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x00, 0x2E, 0x6F, 0x63), 0),
                new GradientStop(Color.FromArgb(0x1E, 0x2E, 0x6F, 0x63), 0.42),
                new GradientStop(Color.FromArgb(0x30, 0x2E, 0x6F, 0x63), 0.5),
                new GradientStop(Color.FromArgb(0x1E, 0x2E, 0x6F, 0x63), 0.58),
                new GradientStop(Color.FromArgb(0x00, 0x2E, 0x6F, 0x63), 1),
            },
        };

        IsHitTestVisible = false;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnTick);
        _timer.Stop();
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != IsActiveProperty)
        {
            return;
        }

        if (IsActive && Motion.Enabled)
        {
            _phase = 0;
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            _phase = 0;
        }

        InvalidateVisual();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _phase += 16d / Period.TotalMilliseconds;
        if (_phase > 1)
        {
            _phase -= 1;
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (!IsActive || !Motion.Enabled || Bounds.Height <= 0 || Bounds.Width <= 0)
        {
            return;
        }

        // Travels from just above the top edge to just below the bottom edge.
        var travel = Bounds.Height + BandHeight;
        var y = (_phase * travel) - BandHeight;
        var rect = new Rect(0, y, Bounds.Width, BandHeight);
        context.DrawRectangle(_band, null, rect);
    }
}
