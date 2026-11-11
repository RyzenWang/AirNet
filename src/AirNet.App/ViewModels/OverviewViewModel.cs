using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AirNet.App.ViewModels;

/// <summary>Landing page: what the product is, one primary action, and recent runs.</summary>
public partial class OverviewViewModel : ObservableObject
{
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

    public void Refresh(SnapshotView view)
    {
        LatestScoreText = view.ScoreText;
        LatestVerdict = view.Verdict;
        LatestTimeText = view.CapturedAtText;

        var runs = new List<RecentRun>
        {
            new()
            {
                Title = $"{view.InterfaceChip} · 本次检测",
                Subtitle = $"延迟 {view.LatencyAvgText} ms · 丢包 {view.LossText}% · 下行 {view.DownloadText} Mbps",
                ScoreText = view.ScoreText,
                TimeText = view.CapturedAtText,
                IsGood = view.IsGood,
                IsWarn = view.IsWarn,
                IsBad = view.IsBad,
            },
            new()
            {
                Title = "Wi-Fi 5 GHz · 昨天晚上",
                Subtitle = "延迟 31.4 ms · 丢包 0.0% · 下行 342 Mbps",
                ScoreText = "88",
                TimeText = "20:42",
                IsGood = true,
            },
            new()
            {
                Title = "有线以太网 · 11 月 9 日",
                Subtitle = "延迟 9.8 ms · 丢包 0.0% · 下行 941 Mbps",
                ScoreText = "97",
                TimeText = "10:06",
                IsGood = true,
            },
        };

        RecentRuns = runs;
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
