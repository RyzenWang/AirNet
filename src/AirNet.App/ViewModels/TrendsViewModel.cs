using System.Globalization;
using AirNet.Core.History;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirNet.App.ViewModels;

/// <summary>One six-hour row in the "时段观察" card.</summary>
public sealed class TrendBucketRow
{
    public required string Label { get; init; }
    public required string RangeText { get; init; }
    public required string CountText { get; init; }
    public required string LatencyText { get; init; }
    public required string ScoreText { get; init; }
    public required double Bar { get; init; }
    public bool HasData { get; init; }
    public bool IsWorst { get; init; }
}

/// <summary>A compact row from the history, newest first.</summary>
public sealed class TrendRunRow
{
    public required string DateText { get; init; }
    public required string TimeText { get; init; }
    public required string ScoreText { get; init; }
    public required string LatencyText { get; init; }
    public required string LossText { get; init; }
    public bool IsGood { get; init; }
    public bool IsWarn { get; init; }
    public bool IsBad { get; init; }
}

/// <summary>
/// History trend page. Aggregation lives in <see cref="HistoryTrendAnalyzer"/>; this class only
/// formats chart series and rows so the six-hour buckets and date filtering stay testable.
/// </summary>
public partial class TrendsViewModel : ObservableObject
{
    private bool _loading;

    public TrendsViewModel(ShellViewModel shell)
    {
        Shell = shell;
        _ = LoadAsync();
    }

    public ShellViewModel Shell { get; }

    [ObservableProperty]
    private int _rangeDays = 7;

    [ObservableProperty]
    private bool _hasData;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private string _rangeCaption = "近 7 天";

    [ObservableProperty]
    private string _averageScoreText = "—";

    [ObservableProperty]
    private string _averageLatencyText = "—";

    [ObservableProperty]
    private string _averageJitterText = "—";

    [ObservableProperty]
    private string _averageLossText = "—";

    [ObservableProperty]
    private string _runCountText = "0 次检测";

    [ObservableProperty]
    private IReadOnlyList<double> _latencySeries = [];

    [ObservableProperty]
    private IReadOnlyList<double> _scoreSeries = [];

    [ObservableProperty]
    private string _latencyMinText = "—";

    [ObservableProperty]
    private string _latencyMaxText = "—";

    [ObservableProperty]
    private string _scoreMinText = "—";

    [ObservableProperty]
    private string _scoreMaxText = "—";

    [ObservableProperty]
    private string _insightText = "开始检测后，这里会按天和时段汇总趋势。";

    [ObservableProperty]
    private IReadOnlyList<TrendBucketRow> _buckets = [];

    [ObservableProperty]
    private IReadOnlyList<TrendRunRow> _recentRuns = [];

    public bool IsSevenDayRange => RangeDays == 7;
    public bool IsThirtyDayRange => RangeDays == 30;

    [RelayCommand]
    private async Task SetRangeAsync(string? raw)
    {
        if (!int.TryParse(raw, out var days) || days is not (7 or 30))
        {
            return;
        }

        if (RangeDays == days)
        {
            return;
        }

        RangeDays = days;
        OnPropertyChanged(nameof(IsSevenDayRange));
        OnPropertyChanged(nameof(IsThirtyDayRange));
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);

    public async Task LoadAsync()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        try
        {
            var records = await Shell.History.LoadAsync(500).ConfigureAwait(true);
            var summary = HistoryTrendAnalyzer.Analyze(records, RangeDays);
            RangeCaption = $"近 {RangeDays} 天";
            HasData = summary.HasData;
            IsEmpty = !summary.HasData;

            if (!summary.HasData)
            {
                AverageScoreText = "—";
                AverageLatencyText = "—";
                AverageJitterText = "—";
                AverageLossText = "—";
                RunCountText = "0 次检测";
                LatencySeries = [];
                ScoreSeries = [];
                LatencyMinText = "—";
                LatencyMaxText = "—";
                ScoreMinText = "—";
                ScoreMaxText = "—";
                Buckets = [];
                RecentRuns = [];
                InsightText = "开始检测后，这里会按天和时段汇总趋势。";
                return;
            }

            AverageScoreText = summary.AverageScore.ToString("0.#", CultureInfo.InvariantCulture);
            AverageLatencyText = summary.AverageLatencyMs.ToString("0.0", CultureInfo.InvariantCulture);
            AverageJitterText = summary.AverageJitterMs.ToString("0.0", CultureInfo.InvariantCulture);
            AverageLossText = summary.AverageLossPercent.ToString("0.0", CultureInfo.InvariantCulture);
            RunCountText = $"{summary.Count} 次检测";

            LatencySeries = summary.Points.Select(point => point.LatencyMs).ToList();
            ScoreSeries = summary.Points.Select(point => (double)point.Score).ToList();
            LatencyMinText = $"{summary.Points.Min(point => point.LatencyMs):0.0} ms";
            LatencyMaxText = $"{summary.Points.Max(point => point.LatencyMs):0.0} ms";
            ScoreMinText = summary.Points.Min(point => point.Score).ToString(CultureInfo.InvariantCulture);
            ScoreMaxText = summary.Points.Max(point => point.Score).ToString(CultureInfo.InvariantCulture);

            var maxBucketLatency = Math.Max(1, summary.Buckets.Where(bucket => bucket.HasData)
                .Select(bucket => bucket.AverageLatencyMs).DefaultIfEmpty(1).Max());
            Buckets = summary.Buckets.Select(bucket => new TrendBucketRow
            {
                Label = bucket.Label,
                RangeText = bucket.RangeText,
                CountText = bucket.HasData ? $"{bucket.Count} 次" : "—",
                LatencyText = bucket.HasData ? $"{bucket.AverageLatencyMs:0.0} ms" : "—",
                ScoreText = bucket.HasData ? bucket.AverageScore.ToString("0", CultureInfo.InvariantCulture) : "—",
                Bar = bucket.HasData ? Math.Clamp(bucket.AverageLatencyMs / maxBucketLatency, 0.08, 1) : 0,
                HasData = bucket.HasData,
                IsWorst = bucket.HasData && summary.WorstBucket?.Label == bucket.Label,
            }).ToList();

            RecentRuns = summary.Points
                .AsEnumerable()
                .Reverse()
                .Take(8)
                .Select(point => new TrendRunRow
                {
                    DateText = point.At.ToLocalTime().ToString("M/d", CultureInfo.InvariantCulture),
                    TimeText = point.At.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture),
                    ScoreText = point.Score.ToString(CultureInfo.InvariantCulture),
                    LatencyText = $"{point.LatencyMs:0.0} ms",
                    LossText = $"{point.LossPercent:0.0}%",
                    IsGood = point.Score >= 90,
                    IsWarn = point.Score is >= 60 and < 90,
                    IsBad = point.Score < 60,
                })
                .ToList();

            var worst = summary.WorstBucket;
            InsightText = worst is { HasData: true, Count: >= 2 }
                          && worst.AverageLatencyMs > summary.AverageLatencyMs * 1.15
                ? $"{worst.Label}（{worst.RangeText}）平均 {worst.AverageLatencyMs:0.0} ms，" +
                  $"高于整体 {summary.AverageLatencyMs:0.0} ms；网络拥塞更可能出现在这个时段。"
                : "当前时段差异不明显；样本越多，晚间拥塞和偶发丢包越容易看出来。";
        }
        finally
        {
            _loading = false;
        }
    }
}
