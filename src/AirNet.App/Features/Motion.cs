namespace AirNet.App.Features;

/// <summary>
/// One switch for every motion in the app. Screenshot export and CI turn it off so captures stay
/// deterministic, and it doubles as the hook for a future "reduce motion" preference.
/// </summary>
public static class Motion
{
    public static bool Enabled { get; set; } = true;

    /// <summary>Page switch fade.</summary>
    public static TimeSpan Page => TimeSpan.FromMilliseconds(160);

    /// <summary>Rings and bars easing to a new value.</summary>
    public static TimeSpan Fill => TimeSpan.FromMilliseconds(600);

    /// <summary>Counting numbers.</summary>
    public static TimeSpan Number => TimeSpan.FromMilliseconds(420);

    /// <summary>Latency line sweeping in.</summary>
    public static TimeSpan Reveal => TimeSpan.FromMilliseconds(520);
}
