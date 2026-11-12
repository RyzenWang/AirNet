namespace AirNet.Core.Probes;

/// <summary>
/// Converts measured bytes and elapsed time into Mbps. Kept separate from the sockets so the
/// arithmetic can be tested without a network.
/// </summary>
public static class SpeedMath
{
    /// <summary>Mbps from a byte count and a duration; 0 when nothing moved or time is unknown.</summary>
    public static double Mbps(long bytes, TimeSpan elapsed)
    {
        if (bytes <= 0 || elapsed <= TimeSpan.Zero)
        {
            return 0;
        }

        return Math.Round(bytes * 8 / elapsed.TotalSeconds / 1_000_000, 1);
    }
}
