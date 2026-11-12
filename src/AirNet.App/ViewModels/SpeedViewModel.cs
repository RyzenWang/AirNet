using System.Globalization;
using AirNet.Core.Probes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirNet.App.ViewModels;

/// <summary>Bandwidth page. Rings are scaled to 1 Gbps down / 200 Mbps up, shown in the caption.</summary>
public partial class SpeedViewModel : ObservableObject
{
    private const double DownloadScaleMbps = 1000;
    private const double UploadScaleMbps = 200;

    public SpeedViewModel(ShellViewModel shell)
    {
        Shell = shell;
        _ = RefreshHistoryAsync();
    }

    public ShellViewModel Shell { get; }

    [ObservableProperty]
    private double _downloadMbps;

    [ObservableProperty]
    private double _uploadMbps;

    [ObservableProperty]
    private bool _isTesting;

    [ObservableProperty]
    private string _serverText = "Cloudflare · 自动就近";

    [ObservableProperty]
    private string _testedAtText = "未测速";

    [ObservableProperty]
    private IReadOnlyList<SpeedHistoryBar> _history = [];

    [ObservableProperty]
    private bool _hasHistory;

    public bool HasDownload => DownloadMbps > 0;
    public bool HasUpload => UploadMbps > 0;

    public string DownloadText => Fmt.Mbps(DownloadMbps);
    public string UploadText => Fmt.Mbps(UploadMbps);
    public double DownloadGauge => Math.Clamp(DownloadMbps / DownloadScaleMbps * 100, 0, 100);
    public double UploadGauge => Math.Clamp(UploadMbps / UploadScaleMbps * 100, 0, 100);
    public string LatencyText => $"{Shell.View.LatencyAvgText} ms";
    public string JitterText => $"{Shell.View.JitterText} ms";
    public string ScaleText => "满量程 1 Gbps / 200 Mbps";
    public string TestButtonText => IsTesting ? "测速中…" : "开始测速";

    partial void OnDownloadMbpsChanged(double value)
    {
        OnPropertyChanged(nameof(DownloadText));
        OnPropertyChanged(nameof(DownloadGauge));
        OnPropertyChanged(nameof(HasDownload));
    }

    partial void OnUploadMbpsChanged(double value)
    {
        OnPropertyChanged(nameof(UploadText));
        OnPropertyChanged(nameof(UploadGauge));
        OnPropertyChanged(nameof(HasUpload));
    }

    partial void OnIsTestingChanged(bool value) => OnPropertyChanged(nameof(TestButtonText));

    /// <summary>Latency and jitter are read from the shell snapshot, so refresh them after a scan.</summary>
    public void RefreshFromSnapshot()
    {
        OnPropertyChanged(nameof(LatencyText));
        OnPropertyChanged(nameof(JitterText));
    }

    /// <summary>Applies only values that were actually measured; zero means "not tested".</summary>
    public void Apply(double downloadMbps, double uploadMbps, string? testedAt = null)
    {
        DownloadMbps = downloadMbps;
        UploadMbps = uploadMbps;

        if (testedAt is not null)
        {
            TestedAtText = testedAt;
        }
    }

    /// <summary>Rebuilds the six-bar trend from measured history, not from a fixed sample array.</summary>
    public async Task RefreshHistoryAsync()
    {
        var records = await Shell.History.LoadAsync(12).ConfigureAwait(true);
        var measured = records.Where(r => r.DownMbps > 0).Take(6).ToList();
        if (measured.Count == 0)
        {
            History = [];
            HasHistory = false;
            return;
        }

        var max = Math.Max(measured.Max(r => r.DownMbps), 1);
        History = measured.Select((record, index) => new SpeedHistoryBar
        {
            Label = record.At.ToLocalTime().ToString("M/d", CultureInfo.InvariantCulture),
            ValueText = Fmt.Compact(record.DownMbps),
            BarHeight = 12 + (64 * (record.DownMbps / max)),
            IsLatest = index == 0,
        }).ToList();
        HasHistory = true;
    }

    [RelayCommand]
    private async Task RunTestAsync()
    {
        if (IsTesting)
        {
            return;
        }

        IsTesting = true;
        try
        {
            var download = await ThroughputProbe.MeasureDownloadMbpsAsync(
                "https://speed.cloudflare.com/__down?bytes=12582912", bytes: 12 * 1024 * 1024);

            if (download <= 0)
            {
                await Shell.PublishSpeedAsync(0, 0, "测速失败：目标服务器不可达或被拦截").ConfigureAwait(true);
                return;
            }

            Apply(download, UploadMbps, DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            await Shell.PublishSpeedAsync(download, UploadMbps, $"测速完成 · 下行 {DownloadText}").ConfigureAwait(true);
            await RefreshHistoryAsync().ConfigureAwait(true);
        }
        finally
        {
            IsTesting = false;
        }
    }
}
