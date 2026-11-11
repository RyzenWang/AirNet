using AirNet.Core.Demo;
using AirNet.Core.Diagnostics;
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
    private CancellationTokenSource? _scanCts;
    private SnapshotView _view;
    private int _pageIndex;
    private ThemeMode _theme = ThemeMode.Light;

    public ShellViewModel()
    {
        var snapshot = DemoSnapshotFactory.CreateSnapshot();
        _view = new SnapshotView(snapshot);

        Overview = new OverviewViewModel(this);
        Monitor = new MonitorViewModel(this);
        Speed = new SpeedViewModel(this);
        Settings = new SettingsViewModel(this);

        Overview.Refresh(_view);
        Monitor.Apply(_view);
        ApplyTheme(ThemeMode.Light);
    }

    public OverviewViewModel Overview { get; }
    public MonitorViewModel Monitor { get; }
    public SpeedViewModel Speed { get; }
    public SettingsViewModel Settings { get; }

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
                OnPropertyChanged(nameof(IsSettingsSelected));
            }
        }
    }

    public bool IsOverviewSelected => PageIndex == 0;
    public bool IsMonitorSelected => PageIndex == 1;
    public bool IsSpeedSelected => PageIndex == 2;
    public bool IsSettingsSelected => PageIndex == 3;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusMessage = "示例数据 · 点击「开始检测」运行真实测量";

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
            "Settings" => 3,
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
        Navigate("Monitor");

        try
        {
            var snapshot = await _runner.RunAsync(
                Speed.DownloadMbps,
                Speed.UploadMbps,
                _scanCts.Token).ConfigureAwait(true);

            View = new SnapshotView(snapshot);
            Overview.Refresh(View);
            Monitor.Apply(View);
            StatusMessage = $"检测完成 · 评分 {View.Score} / {View.GradeLabel}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "检测已取消";
        }
        catch (Exception ex)
        {
            StatusMessage = $"检测失败：{ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void StopScan()
    {
        _scanCts?.Cancel();
        StatusMessage = "正在停止…";
    }

    public void PublishSpeed(double downloadMbps, double uploadMbps, string message)
    {
        if (downloadMbps > 0 && uploadMbps > 0)
        {
            var updated = DiagnosisEngine.Evaluate(
                new MeasurementResult(
                    Snapshot.Interface,
                    Snapshot.Latency,
                    Snapshot.DnsMs,
                    Snapshot.LatencySeries,
                    Snapshot.Hops,
                    Snapshot.EgressAddress,
                    Snapshot.CapturedAt),
                downloadMbps,
                uploadMbps) with { IsSample = Snapshot.IsSample };

            View = new SnapshotView(updated);
            Overview.Refresh(View);
            Monitor.Apply(View);
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
