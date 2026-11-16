using System.Globalization;
using AirNet.App.Localization;
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

/// <summary>One selectable history record for the A/B comparison.</summary>
public sealed class HistoryChoice
{
    public required HistoryRecord Record { get; init; }
    public required string Label { get; init; }
}

/// <summary>One metric row in the A/B comparison.</summary>
public sealed class CompareMetricRow
{
    public required string Name { get; init; }
    public required string AText { get; init; }
    public required string BText { get; init; }
    public required string DeltaText { get; init; }
    public bool IsBetter { get; init; }
    public bool IsWorse { get; init; }
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
    private bool _suppressCompare;

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

    [ObservableProperty]
    private IReadOnlyList<HistoryChoice> _compareOptions = [];

    [ObservableProperty]
    private HistoryChoice? _compareA;

    [ObservableProperty]
    private HistoryChoice? _compareB;

    [ObservableProperty]
    private IReadOnlyList<CompareMetricRow> _compareRows = [];

    [ObservableProperty]
    private string _compareSummary = "至少完成两次检测后，可选两条记录做 A/B 对比。";

    [ObservableProperty]
    private bool _hasComparison;

    [ObservableProperty]
    private bool _showCompareEmpty;

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
            SetCompareOptions(records);
            var summary = HistoryTrendAnalyzer.Analyze(records, RangeDays);
            RangeCaption = T("L.LastNDays", RangeDays);
            HasData = summary.HasData;
            IsEmpty = !summary.HasData;

            if (!summary.HasData)
            {
                AverageScoreText = "—";
                AverageLatencyText = "—";
                AverageJitterText = "—";
                AverageLossText = "—";
                RunCountText = T("L.RunsCount", 0);
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
            RunCountText = T("L.RunsCount", summary.Count);

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
                Label = T(BucketKey(bucket.Label)),
                RangeText = bucket.RangeText,
                CountText = bucket.HasData ? T("L.BucketRuns", bucket.Count) : "—",
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
                ? T("L.InsightWorst", T(BucketKey(worst.Label)), worst.RangeText,
                    worst.AverageLatencyMs.ToString("0.0", CultureInfo.InvariantCulture),
                    summary.AverageLatencyMs.ToString("0.0", CultureInfo.InvariantCulture))
                : T("L.InsightFlat");
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnCompareAChanged(HistoryChoice? value) => RebuildComparison();
    partial void OnCompareBChanged(HistoryChoice? value) => RebuildComparison();

    private void SetCompareOptions(IReadOnlyList<HistoryRecord> records)
    {
        CompareOptions = records
            .Take(20)
            .Select(record => new HistoryChoice
            {
                Record = record,
                Label = $"{record.At.ToLocalTime():M/d HH:mm} · {record.Score} {T("L.Points")}",
            })
            .ToList();

        _suppressCompare = true;
        CompareA = CompareOptions.Count > 0 ? CompareOptions[0] : null;
        CompareB = CompareOptions.Count > 1 ? CompareOptions[1] : null;
        _suppressCompare = false;
        RebuildComparison();
    }

    private void RebuildComparison()
    {
        if (_suppressCompare)
        {
            return;
        }

        HasComparison = CompareA is not null && CompareB is not null;
        ShowCompareEmpty = !HasComparison;
        if (!HasComparison)
        {
            CompareRows = [];
            CompareSummary = "至少完成两次检测后，可选两条记录做 A/B 对比。";
            return;
        }

        var a = CompareA!.Record;
        var b = CompareB!.Record;
        CompareSummary = T("L.CompareSummary", CompareA!.Label, CompareB!.Label);
        CompareRows =
        [
            Compare(T("L.Score"), a.Score, b.Score, T("L.Points"), higherIsBetter: true),
            Compare(T("L.Latency"), a.LatencyAvg, b.LatencyAvg, "ms", higherIsBetter: false),
            Compare(T("L.Jitter"), a.Jitter, b.Jitter, "ms", higherIsBetter: false),
            Compare(T("L.Loss"), a.Loss, b.Loss, "%", higherIsBetter: false),
            Compare(T("L.Download"), a.DownMbps, b.DownMbps, "Mbps", higherIsBetter: true, zeroIsMissing: true),
            Compare(T("L.Upload"), a.UpMbps, b.UpMbps, "Mbps", higherIsBetter: true, zeroIsMissing: true),
        ];
    }

    private static string T(string key, params object[] args) =>
        args.Length == 0
            ? LocalizationService.Current.T(key)
            : LocalizationService.Current.Format(key, args);

    private static string BucketKey(string label) => label switch
    {
        "凌晨" => "L.LateNight",
        "上午" => "L.Morning",
        "下午" => "L.Afternoon",
        "晚间" => "L.Evening",
        _ => label,
    };

    private static CompareMetricRow Compare(
        string name,
        double a,
        double b,
        string unit,
        bool higherIsBetter,
        bool zeroIsMissing = false)
    {
        var missing = zeroIsMissing && (a <= 0 || b <= 0);
        var diff = b - a;
        var better = !missing && Math.Abs(diff) >= 0.05 && (higherIsBetter ? diff > 0 : diff < 0);
        var worse = !missing && Math.Abs(diff) >= 0.05 && (higherIsBetter ? diff < 0 : diff > 0);
        var digits = unit == T("L.Points") ? "0" : "0.0";
        var valueFormat = "{0:" + digits + "}";
        var delta = missing
            ? "—"
            : Math.Abs(diff) < 0.05
                ? T("L.Flat")
                : $"{(diff > 0 ? "+" : "−")}{Math.Abs(diff).ToString(digits, CultureInfo.InvariantCulture)} {unit}";

        return new CompareMetricRow
        {
            Name = name,
            AText = missing ? T("L.NotMeasured") : string.Format(CultureInfo.InvariantCulture, valueFormat, a) + " " + unit,
            BText = missing ? T("L.NotMeasured") : string.Format(CultureInfo.InvariantCulture, valueFormat, b) + " " + unit,
            DeltaText = delta,
            IsBetter = better,
            IsWorse = worse,
        };
    }
}
