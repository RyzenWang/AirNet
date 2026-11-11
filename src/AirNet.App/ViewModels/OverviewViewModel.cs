using System.Globalization;
using AirNet.Core.History;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AirNet.App.ViewModels;

/// <summary>Landing page: what the product is, one primary action, and recent runs.</summary>
public partial class OverviewViewModel : ObservableObject
{
    private IReadOnlyList<RecentRun> _seedRuns = [];

    public OverviewViewModel(ShellViewModel shell)
    {
        Shell = shell;
    }

    public ShellViewModel Shell { get; }

    [ObservableProperty]
    private IReadOnlyList<RecentRun> _recentRuns = [];

    [ObservableProperty]
    private string _latestScoreText = "92";

    [ObservableProperty]
    private string _latestVerdict = "网络状态优秀";

    [ObservableProperty]
    private string _latestTimeText = "刚刚";

    [ObservableProperty]
    private string _recentCaption = "示例";

    public void Refresh(SnapshotView view)
    {
        LatestScoreText = view.ScoreText;
        LatestVerdict = view.Verdict;
        LatestTimeText = view.CapturedAtText;

        _seedRuns =
        [
            new RecentRun
            {
                Title = $"{view.SsidText} · 本次检测",
                Subtitle = $"延迟 {view.LatencyAvgText} ms · 丢包 {view.LossText}% · 下行 {view.DownloadText} Mbps",
                ScoreText = view.ScoreText,
                TimeText = view.CapturedAtText,
                IsGood = view.IsGood,
                IsWarn = view.IsWarn,
                IsBad = view.IsBad,
            },
            new RecentRun
            {
                Title = "Wi-Fi 5 GHz · 昨天晚上",
                Subtitle = "延迟 31.4 ms · 丢包 0.0% · 下行 342 Mbps",
                ScoreText = "88",
                TimeText = "20:42",
                IsGood = true,
            },
            new RecentRun
            {
                Title = "有线以太网 · 11 月 9 日",
                Subtitle = "延迟 9.8 ms · 丢包 0.0% · 下行 941 Mbps",
                ScoreText = "97",
                TimeText = "10:06",
                IsGood = true,
            },
        ];

        if (RecentRuns.Count == 0)
        {
            RecentRuns = _seedRuns;
            RecentCaption = "示例";
        }
    }

    /// <summary>Real runs replace the sample rows as soon as the store has anything in it.</summary>
    public async Task LoadHistoryAsync()
    {
        var records = await Shell.History.LoadAsync(5).ConfigureAwait(true);
        if (records.Count == 0)
        {
            RecentRuns = _seedRuns;
            RecentCaption = "示例";
            return;
        }

        RecentRuns = records.Select(ToRow).ToList();
        RecentCaption = "本机实测";
    }

    public void ResetRecentRuns()
    {
        RecentRuns = _seedRuns;
        RecentCaption = "示例";
    }

    private static RecentRun ToRow(HistoryRecord record)
    {
        var local = record.At.ToLocalTime();
        var isToday = local.Date == DateTimeOffset.Now.Date;
        var title = string.IsNullOrEmpty(record.Ssid) ? record.Kind : $"{record.Kind} · {record.Ssid}";

        return new RecentRun
        {
            Title = $"{title} · {(isToday ? "今天" : local.ToString("M月d日", CultureInfo.InvariantCulture))}",
            Subtitle = $"延迟 {Fmt.Fixed(record.LatencyAvg)} ms · 丢包 {Fmt.Fixed(record.Loss)}% · 下行 {Fmt.Compact(record.DownMbps)} Mbps",
            ScoreText = record.Score.ToString(CultureInfo.InvariantCulture),
            TimeText = local.ToString("HH:mm", CultureInfo.InvariantCulture),
            IsGood = record.Score >= 90,
            IsWarn = record.Score is >= 60 and < 90,
            IsBad = record.Score < 60,
        };
    }
}

/// <summary>Live measurement page: score, metrics, timeline, path and findings.</summary>
public partial class MonitorViewModel : ObservableObject
{
    public MonitorViewModel(ShellViewModel shell)
    {
        Shell = shell;
    }

    public ShellViewModel Shell { get; }

    [ObservableProperty]
    private SnapshotView _view = null!;

    public void Apply(SnapshotView view) => View = view;

    public string LiveWindowText => "60 秒窗口";
    public string ScanHintText => Shell.IsScanning ? "采集中…" : $"上次检测 {Shell.View.CapturedAtText}";

    public string PathSummary => $"{Shell.View.LocalAddress} → {Shell.View.GatewayAddress} → {Shell.View.DnsServer}";
}
