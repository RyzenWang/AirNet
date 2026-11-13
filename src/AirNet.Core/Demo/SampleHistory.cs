using AirNet.Core.History;
using System.Globalization;
using System.Text.Json;

namespace AirNet.Core.Demo;

/// <summary>
/// A deterministic week of runs for the screenshot pipeline only.
/// </summary>
/// <remarks>
/// The trend page deliberately ignores <see cref="HistoryRecord.IsSample"/> rows so the demo
/// snapshot cannot pollute real history. That guard is right, which is why this generator is
/// used in exactly one place: the isolated data directory that <c>--export</c> creates, so the
/// trend page can be documented with a chart instead of an empty state. Nothing here is random —
/// every export produces the same week, so screenshots stay comparable.
/// </remarks>
public static class SampleHistory
{
    /// <summary>Rubber-stamp values keyed by hour of day: 早高峰 / 午间 / 晚高峰 / 深夜.</summary>
    private static readonly (int Hour, double Latency, double Jitter, double Loss, double Down, double Up)[] DayShape =
    [
        (8, 16.4, 1.8, 0.0, 412.0, 21.5),
        (13, 14.9, 1.6, 0.0, 438.0, 22.8),
        (19, 24.7, 3.1, 0.0, 356.0, 19.4),
        (21, 38.2, 5.4, 0.4, 241.0, 15.2),
    ];

    /// <summary>Seven days ending at <paramref name="reference"/>, oldest first.</summary>
    public static IReadOnlyList<HistoryRecord> Week(DateTimeOffset reference)
    {
        var records = new List<HistoryRecord>(DayShape.Length * 7);

        for (var back = 6; back >= 0; back--)
        {
            var day = reference.Date.AddDays(-back);

            // Two of the seven evenings are deliberately bad: a trend where every day looks the
            // same proves nothing, and "周日晚高峰变差" is what the page is meant to reveal.
            var badEvening = back is 1 or 4;

            foreach (var (hour, latency, jitter, loss, down, up) in DayShape)
            {
                var evening = hour >= 19;
                var factor = evening && badEvening ? 1.55 : 1.0;
                var latencyValue = Math.Round(latency * factor, 1);
                var lossValue = evening && badEvening ? Math.Round(loss + 0.8, 1) : loss;

                records.Add(new HistoryRecord
                {
                    At = new DateTimeOffset(day.AddHours(hour), reference.Offset),
                    Score = ScoreOf(latencyValue, lossValue),
                    Grade = GradeOf(ScoreOf(latencyValue, lossValue)),
                    Kind = "Wi-Fi 6",
                    Ssid = "AIRNET-5G",
                    LatencyAvg = latencyValue,
                    Jitter = Math.Round(jitter * factor, 1),
                    Loss = lossValue,
                    DownMbps = evening && badEvening ? Math.Round(down * 0.62, 1) : down,
                    UpMbps = up,
                    LocalAddress = "192.0.2.24",
                    Verdict = "示例",
                    IsSample = false,
                });
            }
        }

        return records;
    }

    /// <summary>Writes the week as JSONL into <paramref name="directory"/> (creates it).</summary>
    public static async Task WriteAsync(
        string directory,
        DateTimeOffset reference,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "history.jsonl");

        var lines = Week(reference).Select(record =>
            JsonSerializer.Serialize(record, DemoHistoryJsonContext.Default.HistoryRecord));

        await File.WriteAllLinesAsync(path, lines, cancellationToken).ConfigureAwait(false);
    }

    private static int ScoreOf(double latency, double loss)
    {
        var score = 98 - Math.Max(0, latency - 14) * 0.85 - loss * 9;
        return (int)Math.Clamp(Math.Round(score), 40, 99);
    }

    private static string GradeOf(int score) => score switch
    {
        >= 90 => "优秀",
        >= 75 => "良好",
        >= 60 => "一般",
        _ => "较差",
    };
}

/// <summary>
/// Its own context: the store's one is internal to the history namespace, and trimming needs a
/// declared shape for every serialized type.
/// </summary>
[System.Text.Json.Serialization.JsonSourceGenerationOptions(WriteIndented = false)]
[System.Text.Json.Serialization.JsonSerializable(typeof(HistoryRecord))]
internal sealed partial class DemoHistoryJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
