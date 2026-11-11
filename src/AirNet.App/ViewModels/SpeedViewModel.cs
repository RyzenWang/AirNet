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
        Apply(shell.Snapshot.DownloadMbps, shell.Snapshot.UploadMbps);
    }

    public ShellViewModel Shell { get; }

    [ObservableProperty]
    private double _downloadMbps;

    [ObservableProperty]
    private double _uploadMbps;

    [ObservableProperty]
    private bool _isTesting;

    [ObservableProperty]
    private string _serverText = "Cloudflare · 上海";

    [ObservableProperty]
    private string _testedAtText = "-";

    public string DownloadText => Fmt.Compact(DownloadMbps);
    public string UploadText => Fmt.Compact(UploadMbps);
    public double DownloadGauge => Math.Clamp(DownloadMbps / DownloadScaleMbps * 100, 0, 100);
    public double UploadGauge => Math.Clamp(UploadMbps / UploadScaleMbps * 100, 0, 100);
    public string LatencyText => $"{Shell.View.LatencyAvgText} ms";
    public string JitterText => $"{Shell.View.JitterText} ms";
    public string ScaleText => "满量程 1 Gbps / 200 Mbps";
    public string TestButtonText => IsTesting ? "测速中…" : "开始测速";

    [ObservableProperty]
    private IReadOnlyList<SpeedHistoryBar> _history = BuildHistory(384);

    partial void OnDownloadMbpsChanged(double value) => OnPropertyChanged(nameof(DownloadText));

    partial void OnUploadMbpsChanged(double value) => OnPropertyChanged(nameof(UploadText));

    partial void OnIsTestingChanged(bool value) => OnPropertyChanged(nameof(TestButtonText));

    public void Apply(double downloadMbps, double uploadMbps)
    {
        DownloadMbps = downloadMbps;
        UploadMbps = uploadMbps;
        OnPropertyChanged(nameof(DownloadGauge));
        OnPropertyChanged(nameof(UploadGauge));
        History = BuildHistory(downloadMbps);
        if (TestedAtText == "-")
        {
            TestedAtText = Shell.View.CapturedAtText;
        }
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
                Shell.PublishSpeed(0, 0, "测速失败：目标服务器不可达或被拦截");
                return;
            }

            Apply(download, UploadMbps);
            TestedAtText = DateTimeOffset.Now.ToString("HH:mm:ss");
            Shell.PublishSpeed(download, UploadMbps, $"测速完成 · 下行 {DownloadText} Mbps");
        }
        finally
        {
            IsTesting = false;
        }
    }

    private static IReadOnlyList<SpeedHistoryBar> BuildHistory(double latest)
    {
        double[] values = [latest, 306, 412, 268, 341, 298];
        string[] labels = ["今天", "昨天", "周一", "周日", "周六", "周五"];
        var max = Math.Max(values.Max(), 1);

        return values.Select((value, index) => new SpeedHistoryBar
        {
            Label = labels[index],
            ValueText = Fmt.Compact(value),
            BarHeight = 12 + (64 * (value / max)),
            IsLatest = index == 0,
        }).ToList();
    }
}
