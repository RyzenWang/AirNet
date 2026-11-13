using AirNet.Core.Demo;
using Xunit;

namespace AirNet.Core.Tests;

public sealed class SampleHistoryTests
{
    private static readonly DateTimeOffset Reference = new(2026, 11, 13, 20, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void A_week_holds_four_runs_a_day_for_seven_days()
    {
        var week = SampleHistory.Week(Reference);

        Assert.Equal(28, week.Count);
        Assert.Equal(7, week.Select(r => r.At.Date).Distinct().Count());
    }

    [Fact]
    public void Records_are_ordered_oldest_first_and_stay_in_the_past()
    {
        var week = SampleHistory.Week(Reference);

        Assert.True(week.Zip(week.Skip(1)).All(pair => pair.First.At <= pair.Second.At));
        Assert.All(week, record => Assert.True(record.At <= Reference.AddDays(1)));
    }

    [Fact]
    public void The_week_is_deterministic_so_screenshots_are_comparable()
    {
        var first = SampleHistory.Week(Reference);
        var second = SampleHistory.Week(Reference);

        Assert.Equal(
            first.Select(r => (r.Score, r.LatencyAvg, r.DownMbps)),
            second.Select(r => (r.Score, r.LatencyAvg, r.DownMbps)));
    }

    [Fact]
    public void Some_evenings_are_deliberately_worse_so_the_chart_shows_something()
    {
        var week = SampleHistory.Week(Reference);

        var evening = week.Where(r => r.At.Hour >= 19).Select(r => r.LatencyAvg).ToList();
        var daytime = week.Where(r => r.At.Hour < 19).Select(r => r.LatencyAvg).ToList();

        Assert.True(evening.Max() > daytime.Max() * 1.4);
        Assert.Contains(week, r => r.Loss >= 0.5);
    }

    [Fact]
    public void Scores_follow_the_measured_latency_instead_of_being_decorative()
    {
        var week = SampleHistory.Week(Reference);

        var worst = week.MaxBy(r => r.LatencyAvg)!;
        var best = week.MinBy(r => r.LatencyAvg)!;

        Assert.True(worst.Score < best.Score);
        Assert.All(week, r => Assert.InRange(r.Score, 40, 99));
    }

    [Fact]
    public async Task Writing_produces_one_json_line_per_record()
    {
        var directory = Path.Combine(Path.GetTempPath(), "airnet-sample-" + Guid.NewGuid().ToString("N"));

        try
        {
            await SampleHistory.WriteAsync(directory, Reference);
            var lines = await File.ReadAllLinesAsync(Path.Combine(directory, "history.jsonl"));

            Assert.Equal(28, lines.Length);
            Assert.All(lines, line => Assert.StartsWith("{", line));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
