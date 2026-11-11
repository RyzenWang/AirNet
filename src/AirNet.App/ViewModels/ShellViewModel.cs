using AirNet.Core.Demo;
using AirNet.Core.Diagnostics;
using AirNet.Core.History;
using AirNet.Core.Probes;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirNet.App.ViewModels;

public enum ThemeMode
{
    System,
    Light,
    Dark,
}

/// <summary>
/// Owns navigation, the current snapshot and the scan lifecycle. Every page reads the same
/// <see cref="SnapshotView"/>, so a re-scan updates the whole window at once.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly MeasurementRunner _runner = new();
    private readonly HistoryStore _history = new();
    private readonly Avalonia.Threading.DispatcherTimer _scanTicker;
    private CancellationTokenSource? _scanCts;
    private SnapshotView _view;
    private int _pageIndex;
    private ThemeMode _theme = ThemeMode.Light;
    private double _scanTarget;
    private DateTimeOffset _scanStartedAt;

    public ShellViewModel()
    {
        var snapshot = DemoSnapshotFactory.CreateSnapshot();
        _view = new SnapshotView(snapshot);

        // Eases the progress bar toward the phase target so it glides instead of stepping.
        _scanTicker = new Avalonia.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(120),
            Avalonia.Threading.DispatcherPriority.Normal,
            (_, _) => OnScanTick());

        Overview = new OverviewViewModel(this);
        Monitor = new MonitorViewModel(this);
        Speed = new SpeedViewModel(this);
        Path = new PathViewModel(this);
        Settings = new SettingsViewModel(this);

        Overview.Refresh(_view);
        Monitor.Apply(_view);
        ApplyTheme(ThemeMode.Light);

        _ = Overview.LoadHistoryAsync();
    }

    public OverviewViewModel Overview { get; }
    public MonitorViewModel Monitor { get; }
    public SpeedViewModel Speed { get; }
    public PathViewModel Path { get; }
    public SettingsViewModel Settings { get; }

    public HistoryStore History => _history;

    public NetworkSnapshot Snapshot => _view.Source;

    public SnapshotView View
    {
        get => _view;
        private set
        {
            if (SetProperty(ref _view, value))
            {
                OnPropertyChanged(nameof(LastScanText));
                OnPropertyChanged(nameof(ConnectionChip));
                OnPropertyChanged(nameof(FooterLatencyText));
                OnPropertyChanged(nameof(FooterLossText));
                OnPropertyChanged(nameof(FooterTimestampText));
                OnPropertyChanged(nameof(SourceChipText));
                OnPropertyChanged(nameof(IsSample));
            }
        }
    }

    public int PageIndex
    {
        get => _pageIndex;
        private set
        {
            if (SetProperty(ref _pageIndex, value))
            {
                OnPropertyChanged(nameof(IsOverviewSelected));
                OnPropertyChanged(nameof(IsMonitorSelected));
                OnPropertyChanged(nameof(IsSpeedSelected));
                OnPropertyChanged(nameof(IsPathSelected));
                OnPropertyChanged(nameof(IsSettingsSelected));
            }
        }
    }

    public bool IsOverviewSelected => PageIndex == 0;
    public bool IsMonitorSelected => PageIndex == 1;
    public bool IsSpeedSelected => PageIndex == 2;
    public bool IsPathSelected => PageIndex == 3;
    public bool IsSettingsSelected => PageIndex == 4;

    /// <summary>The echo/TCP target, also used as the trace destination.</summary>
    public string ReferenceHost => _runner.ReferenceHost;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusMessage = "示例数据 · 点击「开始检测」运行真实测量";

    [ObservableProperty]
    private string _scanPhaseText = string.Empty;

    [ObservableProperty]
    private string _scanStatusText = string.Empty;

    [ObservableProperty]
    private string _scanElapsedText = "0.0 s";

    [ObservableProperty]
    private double _scanFraction;

    private void OnScanTick()
    {
        ScanElapsedText = $"{(DateTimeOffset.Now - _scanStartedAt).TotalSeconds:0.0} s";

        // Ease toward the phase target; snap when close so the last percent actually lands.
        ScanFraction += (_scanTarget - ScanFraction) * 0.22;
        if (Math.Abs(_scanTarget - ScanFraction) < 0.004)
        {
            ScanFraction = _scanTarget;
        }
    }

    public string ConnectionChip => View.InterfaceChip;
    public string LastScanText => $"上次检测 {View.CapturedAtText}";
    public string FooterLatencyText => $"延迟 {View.LatencyAvgText} ms";
    public string FooterLossText => $"丢包 {View.LossText}%";
    public string FooterTimestampText => View.IsSample ? "示例数据" : $"采集于 {View.CapturedAtText}";
    public string SourceChipText => View.IsSample ? "示例数据" : "本机实测";
    public bool IsSample => View.IsSample;

    public bool IsDarkMode => _theme == ThemeMode.Dark
        || (_theme == ThemeMode.System && Application.Current?.ActualThemeVariant == ThemeVariant.Dark);

    public bool IsSystemTheme => _theme == ThemeMode.System;
    public bool IsLightTheme => _theme == ThemeMode.Light;
    public bool IsDarkTheme => _theme == ThemeMode.Dark;

    public string ThemeGlyph => IsDarkMode
        ? "M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"
        : "M12 16a4 4 0 1 0 0-8 4 4 0 0 0 0 8ZM12 2v2M12 20v2M4.93 4.93l1.42 1.42M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41";

    [RelayCommand]
    private void Navigate(string? key)
    {
        PageIndex = key switch
        {
            "Overview" => 0,
            "Monitor" => 1,
            "Speed" => 2,
            "Path" => 3,
            "Settings" => 4,
            _ => PageIndex,
        };
    }

    [RelayCommand]
    private void ToggleTheme() => ApplyTheme(IsDarkMode ? ThemeMode.Light : ThemeMode.Dark);

    [RelayCommand]
    private void SetThemeMode(string? mode)
    {
        ApplyTheme(mode switch
        {
            "Light" => ThemeMode.Light,
            "Dark" => ThemeMode.Dark,
            _ => ThemeMode.System,
        });
    }

    [RelayCommand]
    private async Task RunScanAsync()
    {
        if (IsScanning)
        {
            return;
        }

        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();

        IsScanning = true;
        StatusMessage = "正在采集探针包…";
        _scanStartedAt = DateTimeOffset.Now;
        _scanTarget = 0;
        ScanFraction = 0;
        ScanPhaseText = "识别网卡";
        ScanStatusText = "正在检测 · 识别网卡";
        ScanElapsedText = "0.0 s";
        _scanTicker.Start();
        Navigate("Monitor");

        var progress = new Progress<ScanProgress>(report =>
        {
            _scanTarget = report.Fraction;
            ScanPhaseText = report.Phase;
            ScanStatusText = report.Phase == "完成" ? "检测完成" : $"正在检测 · {report.Phase}";
        });

        // Every probe sample drives the live tiles and the live curve.
        var live = new Progress<LiveSample>(sample => Monitor.PushLive(sample));
        Monitor.BeginLive();

        try
        {
            var snapshot = await _runner.RunAsync(
                Speed.DownloadMbps,
                Speed.UploadMbps,
                progress,
                live,
                _scanCts.Token).ConfigureAwait(true);

            View = new SnapshotView(snapshot);
            Overview.Refresh(View);
            Monitor.Apply(View);
            Path.RefreshFromSnapshot();
            Settings.RefreshDnsPlan();
            StatusMessage = $"检测完成 · 评分 {View.Score} / {View.GradeLabel}";

            // Only real measurements are persisted; the sample snapshot stays ephemeral.
            if (!snapshot.IsSample)
            {
                await _history.AppendAsync(snapshot).ConfigureAwait(true);
                await Overview.LoadHistoryAsync().ConfigureAwait(true);
                await Settings.RefreshHistoryCountAsync().ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "检测已取消";
            Monitor.Apply(View);
        }
        catch (Exception ex)
        {
            StatusMessage = $"检测失败：{ex.Message}";
            Monitor.Apply(View);
        }
        finally
        {
            _scanTicker.Stop();
            ScanFraction = 1;
            ScanStatusText = string.Empty;
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void StopScan()
    {
        _scanCts?.Cancel();
        StatusMessage = "正在停止…";
    }

    /// <summary>Writes the self-contained HTML report and returns the path it used.</summary>
    public async Task<string> WriteReportAsync(string path)
    {
        var html = Core.Reporting.ReportBuilder.Build(
            Snapshot,
            Path.Attribution,
            Path.LastMtu,
            maskAddresses: true);

        await File.WriteAllTextAsync(path, html, System.Text.Encoding.UTF8).ConfigureAwait(true);
        StatusMessage = $"报告已导出：{System.IO.Path.GetFileName(path)}";
        return path;
    }

    public void PublishSpeed(double downloadMbps, double uploadMbps, string message)
    {
        if (downloadMbps > 0 && uploadMbps > 0)
        {
            var updated = DiagnosisEngine.Evaluate(
                new MeasurementResult(
                    Snapshot.Interface,
                    Snapshot.Latency,
                    Snapshot.ProbeMode,
                    new ProbeDiagnostics(
                        Snapshot.ProbeMode == ProbeMode.Icmp,
                        true,
                        Snapshot.Latency.Sent,
                        Snapshot.Latency.Received,
                        Snapshot.Latency.Sent,
                        Snapshot.Latency.Received),
                    Snapshot.Timing,
                    Snapshot.LatencySeries,
                    Snapshot.Hops,
                    Snapshot.EgressAddress,
                    Snapshot.CapturedAt),
                downloadMbps,
                uploadMbps) with { IsSample = Snapshot.IsSample };

            View = new SnapshotView(updated);
            Overview.Refresh(View);
            Monitor.Apply(View);
            Path.RefreshFromSnapshot();
        }

        StatusMessage = message;
    }

    private void ApplyTheme(ThemeMode mode)
    {
        _theme = mode;
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = mode switch
            {
                ThemeMode.Light => ThemeVariant.Light,
                ThemeMode.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }

        OnPropertyChanged(nameof(IsDarkMode));
        OnPropertyChanged(nameof(IsSystemTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(ThemeGlyph));
    }
}
