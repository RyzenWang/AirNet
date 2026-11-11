using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System.Globalization;
using AirNet.App.Features;

namespace AirNet.App.Controls;

/// <summary>
/// A number that counts up to its value instead of snapping. Used for the hero readings (score,
/// download, upload) where a hard swap next to an animating ring reads as a glitch.
/// </summary>
public class AnimatedNumber : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<AnimatedNumber, double>(nameof(Value));

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<AnimatedNumber, int>(nameof(Decimals));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<AnimatedNumber, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<AnimatedNumber, double>(nameof(FontSize), 14d);

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        AvaloniaProperty.Register<AnimatedNumber, FontWeight>(nameof(FontWeight), FontWeight.Normal);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<AnimatedNumber, FontFamily>(
            nameof(FontFamily), new FontFamily("Inter, Segoe UI, sans-serif"));

    public static readonly StyledProperty<double> LetterSpacingProperty =
        AvaloniaProperty.Register<AnimatedNumber, double>(nameof(LetterSpacing));

    private readonly DispatcherTimer _timer;
    private double _displayed;
    private double _from;
    private double _target;
    private double _elapsedMs;
    private bool _animating;

    public AnimatedNumber()
    {
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnTick);
        ClipToBounds = false;
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Decimals
    {
        get => GetValue(DecimalsProperty);
        set => SetValue(DecimalsProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double LetterSpacing
    {
        get => GetValue(LetterSpacingProperty);
        set => SetValue(LetterSpacingProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
        {
            AnimateTo(change.GetNewValue<double>());
        }
        else if (change.Property == DecimalsProperty || change.Property == ForegroundProperty
                 || change.Property == FontSizeProperty || change.Property == FontWeightProperty
                 || change.Property == FontFamilyProperty || change.Property == LetterSpacingProperty)
        {
            InvalidateVisual();
        }
    }

    private void AnimateTo(double target)
    {
        if (double.IsNaN(target))
        {
            return;
        }

        if (!Motion.Enabled)
        {
            _displayed = target;
            _animating = false;
            _timer.Stop();
            InvalidateVisual();
            return;
        }

        _from = _displayed;
        _target = target;
        _elapsedMs = 0;
        _animating = Math.Abs(target - _displayed) > 0.001;
        if (_animating)
        {
            _timer.Start();
        }
        else
        {
            InvalidateVisual();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var duration = Motion.Number.TotalMilliseconds;
        _elapsedMs += 16;

        var t = Math.Clamp(_elapsedMs / duration, 0, 1);
        var eased = 1 - Math.Pow(1 - t, 3);   // ease-out cubic
        _displayed = _from + ((_target - _from) * eased);

        if (t >= 1)
        {
            _displayed = _target;
            _animating = false;
            _timer.Stop();
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var text = _displayed.ToString("F" + Decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle.Normal, FontWeight),
            FontSize,
            Foreground ?? Brushes.Black);

        context.DrawText(formatted, new Point(0, (Bounds.Height - formatted.Height) / 2));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = Math.Max(_target, _displayed)
            .ToString("F" + Decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle.Normal, FontWeight),
            FontSize,
            Brushes.Black);

        return new Size(formatted.Width, formatted.Height);
    }
}
