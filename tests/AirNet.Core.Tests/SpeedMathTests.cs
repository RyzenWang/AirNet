using AirNet.Core.Probes;
using Xunit;

namespace AirNet.Core.Tests;

public sealed class SpeedMathTests
{
    [Fact]
    public void One_megabyte_per_second_is_eight_megabits()
    {
        var mbps = SpeedMath.Mbps(1024 * 1024, TimeSpan.FromSeconds(1));

        Assert.Equal(8.4, mbps);
    }

    [Fact]
    public void Twelve_and_a_half_megabytes_in_a_second_is_one_hundred_megabits()
    {
        var mbps = SpeedMath.Mbps(12_500_000, TimeSpan.FromSeconds(1));

        Assert.Equal(100, mbps);
    }

    [Fact]
    public void Zero_bytes_reports_zero_not_infinity()
    {
        Assert.Equal(0, SpeedMath.Mbps(0, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Zero_duration_reports_zero()
    {
        Assert.Equal(0, SpeedMath.Mbps(1024, TimeSpan.Zero));
    }

    [Fact]
    public void Half_the_bytes_in_the_same_time_is_half_the_speed()
    {
        var full = SpeedMath.Mbps(8_000_000, TimeSpan.FromSeconds(2));
        var half = SpeedMath.Mbps(4_000_000, TimeSpan.FromSeconds(2));

        Assert.Equal(full / 2, half);
    }
}
