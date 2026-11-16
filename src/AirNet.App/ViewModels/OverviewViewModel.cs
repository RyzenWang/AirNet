using System.Globalization;
using AirNet.App.Localization;
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
    private string _latestScoreText = "—";

    [ObservableProperty]
    private string _latestVerdict = T("L.WaitingForScan");

    [ObservableProperty]
    private string _latestTimeText = T("L.NotScanned");

    [ObservableProperty]
    private string _recentCaption = T("L.LocalMeasurement");

    public void Refresh(SnapshotView view)
    {
        LatestScoreText = view.ScoreText;
        LatestVerdict = view.Verdict;
        LatestTimeText = view.CapturedAtText;

        // Sample history is a screenshot-only affordance. A normal launch starts with no
        // recent-run values, and a scan never re-seeds them.
        if (!Shell.IsDemo)
        {
            if (RecentRuns.Count == 0)
            {
                RecentCaption = T("L.LocalMeasurement");
            }

            return;
        }

        _seedRuns =
        [
            new RecentRun
            {
                Title = $"{view.SsidText} · {T("L.ThisScan")}",
                Subtitle = $"{T("L.Latency")} {view.LatencyAvgText} ms · {T("L.Loss")} {view.LossText}% · {T("L.Download")} {view.DownloadText}",
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
                Title = $"Wi-Fi 5 GHz · {T("L.YesterdayEvening")}",
                Subtitle = "延迟 31.4 ms · 丢包 0.0% · 下行 342 Mbps",
                ScoreText = "88",
                TimeText = "20:42",
                IsGood = true,
                DeltaText = "较上次 评分 −2 · 延迟 +1.1",
                DeltaRegressed = true,
            },
            new RecentRun
            {
                Title = $"{T("L.Lan")} · {T("L.Nov9")}",
                Subtitle = "延迟 9.8 ms · 丢包 0.0% · 下行 941 Mbps",
                ScoreText = "97",
                TimeText = "10:06",
                IsGood = true,
            },
        ];

        if (RecentRuns.Count == 0)
        {
            RecentRuns = _seedRuns;
            RecentCaption = T("L.Example");
        }
    }

    /// <summary>Real runs replace the sample rows as soon as the store has anything in it.</summary>
    public async Task LoadHistoryAsync()
    {
        var records = await Shell.History.LoadAsync(5).ConfigureAwait(true);
        if (records.Count == 0)
        {
            RecentRuns = Shell.IsDemo ? _seedRuns : [];
            RecentCaption = Shell.IsDemo ? T("L.Example") : T("L.LocalMeasurement");
            return;
        }

        // Records arrive newest first, so each row compares against the one after it.
        var rows = new List<RecentRun>(records.Count);
        for (var i = 0; i < records.Count; i++)
        {
            rows.Add(ToRow(records[i], i + 1 < records.Count ? records[i + 1] : null));
        }

        RecentRuns = rows;
        RecentCaption = T("L.LocalMeasurement");
    }

    public void ResetRecentRuns()
    {
        RecentRuns = Shell.IsDemo ? _seedRuns : [];
        RecentCaption = Shell.IsDemo ? T("L.Example") : T("L.LocalMeasurement");
    }

    private static RecentRun ToRow(HistoryRecord record, HistoryRecord? previous)
    {
        var local = record.At.ToLocalTime();
        var isToday = local.Date == DateTimeOffset.Now.Date;
        var kind = LocalizeInterfaceKind(record.Kind);
        var title = string.IsNullOrEmpty(record.Ssid) ? kind : $"{kind} · {record.Ssid}";
        var delta = HistoryDelta.Compare(record, previous);
        var culture = LocalizationService.Current.Culture;
        var day = isToday
            ? T("L.Today")
            : local.ToString(LocalizationService.Current.IsChinese ? "M月d日" : "MMM d", culture);

        return new RecentRun
        {
            Title = $"{title} · {day}",
            Subtitle = $"{T("L.Latency")} {Fmt.Fixed(record.LatencyAvg)} ms · {T("L.Loss")} {Fmt.Fixed(record.Loss)}% · {T("L.Download")} {Fmt.Mbps(record.DownMbps)}",
            ScoreText = record.Score.ToString(CultureInfo.InvariantCulture),
            TimeText = local.ToString("HH:mm", CultureInfo.InvariantCulture),
            IsGood = record.Score >= 90,
            IsWarn = record.Score is >= 60 and < 90,
            IsBad = record.Score < 60,
            DeltaText = FormatDelta(delta),
            DeltaImproved = delta.IsImprovement,
            DeltaRegressed = delta.IsRegression,
        };
    }

    private static string T(string key) => LocalizationService.Current.T(key);
    private static string Lfmt(string key, params object[] args) => LocalizationService.Current.Format(key, args);

    private static string LocalizeInterfaceKind(string kind) => kind switch
    {
        "有线以太网" => T("L.Ethernet"),
        "无线" => T("L.WirelessGeneric"),
        _ => kind,
    };

    private static string FormatDelta(HistoryDelta delta)
    {
        var parts = new List<string>(4);
        if (delta.ScoreDelta != 0) parts.Add($"{T("L.Score")} {Signed(delta.ScoreDelta, "0")}");
        if (delta.LatencyDelta != 0) parts.Add($"{T("L.Latency")} {Signed(delta.LatencyDelta, "0.0")}");
        if (delta.JitterDelta != 0) parts.Add($"{T("L.Jitter")} {Signed(delta.JitterDelta, "0.0")}");
        if (delta.DownDelta != 0) parts.Add($"{T("L.Download")} {Signed(delta.DownDelta, "0.#")}");
        return parts.Count == 0 ? string.Empty : $"{T("L.VsLast")} {string.Join(" · ", parts)}";
    }

    private static string Signed(double value, string format) =>
        value > 0
            ? $"+{value.ToString(format, CultureInfo.InvariantCulture)}"
            : $"−{Math.Abs(value).ToString(format, CultureInfo.InvariantCulture)}";
}

/// <summary>
/// Live measurement page. During a scan it shows the samples as they land (real numbers, not a
/// decorative ticker), and settles into the result when the scan completes.
/// </summary>
public partial class MonitorViewModel : ObservableObject
{
    private static string T(string key) => LocalizationService.Current.T(key);
    private static string Lfmt(string key, params object[] args) => LocalizationService.Current.Format(key, args);

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
    private string _modeStatusText = T("L.ModeIdle");

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
        ModeStatusText = T("L.ProbingUdp");
        try
        {
            var result = await GameModeProbe.RunAsync().ConfigureAwait(true);
            HasGameModeResult = true;
            GameModeHeadline = result.UdpReachable ? T("L.UdpReachable") : T("L.UdpUnreachable");
            GameModeDetail = result.UdpReachable
                ? $"STUN RTT {result.RttMs:0.0} ms · {T("L.Jitter")} {result.JitterMs:0.0} ms · {T("L.Loss")} {result.LossPercent:0.0}% · {result.NatSummary}"
                : $"{result.NatSummary} · {T("L.Target")} {result.Target}";
            ModeStatusText = result.UdpReachable ? T("L.GameDoneOk") : T("L.GameDoneFail");
        }
        catch (Exception ex)
        {
            ModeStatusText = Lfmt("L.GameFailed", ex.Message);
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
        ModeStatusText = T("L.TestingUplink");
        try
        {
            var result = await MeetingModeProbe.RunAsync().ConfigureAwait(true);
            HasMeetingModeResult = true;
            MeetingModeHeadline = result.Verdict;
            MeetingModeDetail = result.HasUpload
                ? $"{T("L.Uplink")} {result.UploadMbps:0.0} Mbps · {T("L.Jitter")} {result.JitterMs:0.0} ms · {T("L.Loss")} {result.LossPercent:0.0}%"
                : T("L.UplinkNotMeasured");
            ModeStatusText = T("L.MeetingDone");
        }
        catch (Exception ex)
        {
            ModeStatusText = Lfmt("L.MeetingFailed", ex.Message);
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

    public string LiveWindowText => T("L.LiveWindow");
    public string ScanHintText => Shell.IsScanning
        ? T("L.Collecting")
        : Lfmt("L.LastScanFormat", Shell.View.CapturedAtText);

    /// <summary>Shown under the headline while samples are streaming in.</summary>
    public string MonitorLiveHint => Lfmt("L.Sampling", LiveSourceText);

    public string PathSummary => $"{Shell.View.LocalAddress} → {Shell.View.GatewayAddress} → {Shell.View.DnsServer}";
}
