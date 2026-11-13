using System.Globalization;
using AirNet.Core.History;
using AirNet.Core.Probes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
                Subtitle = $"延迟 {view.LatencyAvgText} ms · 丢包 {view.LossText}% · 下行 {view.DownloadText}",
                ScoreText = view.ScoreText,
                TimeText = view.CapturedAtText,
                IsGood = view.IsGood,
                IsWarn = view.IsWarn,
                IsBad = view.IsBad,
                DeltaText = "较上次 评分 +5 · 延迟 −8.3 · 抖动 −1.6",
                DeltaImproved = true,
            },
            new RecentRun
            {
                Title = "Wi-Fi 5 GHz · 昨天晚上",
                Subtitle = "延迟 31.4 ms · 丢包 0.0% · 下行 342 Mbps",
                ScoreText = "88",
                TimeText = "20:42",
                IsGood = true,
                DeltaText = "较上次 评分 −2 · 延迟 +1.1",
                DeltaRegressed = true,
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

        // Records arrive newest first, so each row compares against the one after it.
        var rows = new List<RecentRun>(records.Count);
        for (var i = 0; i < records.Count; i++)
        {
            rows.Add(ToRow(records[i], i + 1 < records.Count ? records[i + 1] : null));
        }

        RecentRuns = rows;
        RecentCaption = "本机实测";
    }

    public void ResetRecentRuns()
    {
        RecentRuns = _seedRuns;
        RecentCaption = "示例";
    }

    private static RecentRun ToRow(HistoryRecord record, HistoryRecord? previous)
    {
        var local = record.At.ToLocalTime();
        var isToday = local.Date == DateTimeOffset.Now.Date;
        var title = string.IsNullOrEmpty(record.Ssid) ? record.Kind : $"{record.Kind} · {record.Ssid}";
        var delta = HistoryDelta.Compare(record, previous);

        return new RecentRun
        {
            Title = $"{title} · {(isToday ? "今天" : local.ToString("M月d日", CultureInfo.InvariantCulture))}",
            Subtitle = $"延迟 {Fmt.Fixed(record.LatencyAvg)} ms · 丢包 {Fmt.Fixed(record.Loss)}% · 下行 {Fmt.Mbps(record.DownMbps)}",
            ScoreText = record.Score.ToString(CultureInfo.InvariantCulture),
            TimeText = local.ToString("HH:mm", CultureInfo.InvariantCulture),
            IsGood = record.Score >= 90,
            IsWarn = record.Score is >= 60 and < 90,
            IsBad = record.Score < 60,
            DeltaText = delta.Text,
            DeltaImproved = delta.IsImprovement,
            DeltaRegressed = delta.IsRegression,
        };
    }
}

/// <summary>
/// Live measurement page. During a scan it shows the samples as they land (real numbers, not a
/// decorative ticker), and settles into the result when the scan completes.
/// </summary>
public partial class MonitorViewModel : ObservableObject
{
    private readonly List<double> _liveSeries = new();
    private string _liveSource = string.Empty;
    private readonly Avalonia.Threading.DispatcherTimer _flashTimer;

    private void OnFlashElapsed()
    {
        _flashTimer.Stop();
        IsResultFlash = false;
    }

    public MonitorViewModel(ShellViewModel shell)
    {
        Shell = shell;
        _flashTimer = new Avalonia.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(480),
            Avalonia.Threading.DispatcherPriority.Normal,
            (_, _) => OnFlashElapsed());

        var tiles = SnapshotView.DemoTiles();
        _latencyTile = tiles[0];
        _jitterTile = tiles[1];
        _lossTile = tiles[2];
        _downloadTile = tiles[3];
    }

    public ShellViewModel Shell { get; }

    [ObservableProperty]
    private SnapshotView _view = null!;

    [ObservableProperty]
    private MetricTile _latencyTile;

    [ObservableProperty]
    private MetricTile _jitterTile;

    [ObservableProperty]
    private MetricTile _lossTile;

    [ObservableProperty]
    private MetricTile _downloadTile;

    [ObservableProperty]
    private IReadOnlyList<double> _chartSeries = [];

    /// <summary>Slot count the live curve grows into; 0 means "stretch to fit" for a finished run.</summary>
    [ObservableProperty]
    private int _chartCapacity;

    [ObservableProperty]
    private bool _isLive;

    [ObservableProperty]
    private bool _isResultFlash;

    [ObservableProperty]
    private bool _isModeTesting;

    [ObservableProperty]
    private string _modeStatusText = "按需深测游戏或会议路径；不会自动运行。";

    [ObservableProperty]
    private string _gameModeHeadline = string.Empty;

    [ObservableProperty]
    private string _gameModeDetail = string.Empty;

    [ObservableProperty]
    private string _meetingModeHeadline = string.Empty;

    [ObservableProperty]
    private string _meetingModeDetail = string.Empty;

    [ObservableProperty]
    private bool _hasGameModeResult;

    [ObservableProperty]
    private bool _hasMeetingModeResult;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonitorLiveHint))]
    private string _liveSourceText = string.Empty;

    public void Apply(SnapshotView view)
    {
        View = view;
        IsLive = false;
        _liveSeries.Clear();
        ChartCapacity = 0;

        var tiles = view.Metrics;
        LatencyTile = tiles[0];
        JitterTile = tiles[1];
        LossTile = tiles[2];
        DownloadTile = tiles[3];
        ChartSeries = view.Series;

        // One short highlight so the landed numbers read as "this just updated".
        if (Features.Motion.Enabled)
        {
            IsResultFlash = true;
            _flashTimer.Stop();
            _flashTimer.Start();
        }
    }

    public void BeginLive()
    {
        _liveSeries.Clear();
        _liveSource = string.Empty;
        ChartSeries = [];
        ChartCapacity = MeasurementRunner.IcmpSampleCount;
        IsLive = true;
        LiveSourceText = "等待首个样本…";
    }

    /// <summary>Called for every real probe sample; the numbers below come straight from them.</summary>
    public void PushLive(LiveSample sample)
    {
        if (!IsLive)
        {
            return;
        }

        // ICMP and TCP samples are different measurements; mixing them in one curve would draw a
        // line that never existed. Each phase starts its own.
        if (sample.Source != _liveSource)
        {
            _liveSource = sample.Source;
            _liveSeries.Clear();
            ChartCapacity = sample.Source == "TCP" ? MeasurementRunner.TcpSampleCount : MeasurementRunner.IcmpSampleCount;
        }

        if (sample.LatencyMs is { } latency)
        {
            _liveSeries.Add(latency);
        }

        ChartSeries = _liveSeries.ToArray();
        LiveSourceText = $"{sample.Source} · 第 {sample.Count} 个样本";

        LatencyTile = Tile("延迟 / LATENCY", sample.AverageMs, 1, "ms", LiveSourceText,
            sample.AverageMs <= 40);

        // ICMP reports whole milliseconds, so jitter computed from it is always 0 — showing that
        // would look like a broken gauge. It appears once the TCP phase starts.
        JitterTile = sample.Source == "TCP"
            ? Tile("抖动 / JITTER", sample.JitterMs, 1, "ms", "相邻包差均值", sample.JitterMs <= 5)
            : Pending("抖动 / JITTER", "ms", "ICMP 分辨率 1ms");

        LossTile = Tile("丢包 / LOSS", sample.LossPercent, 1, "%", $"{sample.Count} 个样本", sample.LossPercent <= 0.5);
    }

    [RelayCommand]
    private async Task RunGameModeAsync()
    {
        if (IsModeTesting)
        {
            return;
        }

        IsModeTesting = true;
        ModeStatusText = "正在探测 UDP / STUN 路径…";
        try
        {
            var result = await GameModeProbe.RunAsync().ConfigureAwait(true);
            HasGameModeResult = true;
            GameModeHeadline = result.UdpReachable ? "UDP 可达" : "UDP 不可达";
            GameModeDetail = result.UdpReachable
                ? $"STUN 参考 RTT {result.RttMs:0.0} ms · 抖动 {result.JitterMs:0.0} ms · 丢包 {result.LossPercent:0.0}% · {result.NatSummary}"
                : $"{result.NatSummary} · 目标 {result.Target}";
            ModeStatusText = result.UdpReachable
                ? "游戏模式完成：UDP 路径可用；NAT 结论是映射行为启发式。"
                : "游戏模式完成：UDP 路径不可用，可能是网络/防火墙拦截。";
        }
        catch (Exception ex)
        {
            ModeStatusText = $"游戏模式失败：{ex.Message}";
        }
        finally
        {
            IsModeTesting = false;
        }
    }

    [RelayCommand]
    private async Task RunMeetingModeAsync()
    {
        if (IsModeTesting)
        {
            return;
        }

        IsModeTesting = true;
        ModeStatusText = "正在测上行、抖动和丢包…";
        try
        {
            var result = await MeetingModeProbe.RunAsync().ConfigureAwait(true);
            HasMeetingModeResult = true;
            MeetingModeHeadline = result.Verdict;
            MeetingModeDetail = result.HasUpload
                ? $"上行 {result.UploadMbps:0.0} Mbps · 抖动 {result.JitterMs:0.0} ms · 丢包 {result.LossPercent:0.0}%"
                : "上行未测出；不能只凭缺失的数字判定会议可用。";
            ModeStatusText = "会议模式完成：切换模式不会改动网络配置。";
        }
        catch (Exception ex)
        {
            ModeStatusText = $"会议模式失败：{ex.Message}";
        }
        finally
        {
            IsModeTesting = false;
        }
    }

    private static MetricTile Tile(string label, double value, int decimals, string unit, string foot, bool good) => new()
    {
        Label = label,
        Value = value.ToString("F" + decimals, System.Globalization.CultureInfo.InvariantCulture),
        Number = value,
        Decimals = decimals,
        Unit = unit,
        Foot = foot,
        IsGood = good,
        IsWarn = !good,
    };

    private static MetricTile Pending(string label, string unit, string foot) => new()
    {
        Label = label,
        Value = "—",
        Unit = unit,
        Foot = foot,
        IsPlaceholder = true,
        IsGood = true,
    };

    public string LiveWindowText => "60 秒窗口";
    public string ScanHintText => Shell.IsScanning ? "采集中…" : $"上次检测 {Shell.View.CapturedAtText}";

    /// <summary>Shown under the headline while samples are streaming in.</summary>
    public string MonitorLiveHint => $"正在采样：{LiveSourceText}";

    public string PathSummary => $"{Shell.View.LocalAddress} → {Shell.View.GatewayAddress} → {Shell.View.DnsServer}";
}
