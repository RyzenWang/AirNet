using AirNet.App.Localization;

namespace AirNet.App.ViewModels;

/// <summary>Read-only projection used by the share card, so the card never reaches into the shell.</summary>
public sealed class ShareCardViewModel
{
    public ShareCardViewModel(SnapshotView view)
    {
        Score = view.Score;
        ScoreText = view.ScoreText;
        GradeLabel = view.GradeLabel;
        Verdict = view.Verdict;
        Summary = view.Summary;
        LatencyText = view.LatencyAvgText;
        JitterText = view.JitterText;
        LossText = view.LossText;
        DownloadText = view.DownloadText;
        UploadText = view.UploadText;
        UploadFootText = view.HasBandwidth
            ? LocalizationService.Current.Format("L.UploadFormat", view.UploadText)
            : LocalizationService.Current.T("L.NotTested");
        NetworkText = view.SsidText;
        CapturedText = view.CapturedDateText;
        IsSample = view.IsSample;
        SourceText = LocalizationService.Current.T(view.IsSample ? "L.SampleRun" : "L.LocalMeasurement");
        IsGood = view.IsGood;
        IsWarn = view.IsWarn;
        IsBad = view.IsBad;
        Scenarios = view.Scenarios
            .Select(row => new ShareScenarioCard
            {
                Name = LocalizeScenarioName(row.Name),
                Hint = LocalizeScenarioHint(row.Hint),
                ScoreText = row.ScoreText,
                IsMeasured = !row.IsPlaceholder,
                IsWarn = row.IsWarn,
                IsBad = row.IsBad,
            })
            .ToList();
    }

    public double Score { get; }
    public string ScoreText { get; }
    public string GradeLabel { get; }
    public string Verdict { get; }
    public string Summary { get; }
    public string LatencyText { get; }
    public string JitterText { get; }
    public string LossText { get; }
    public string DownloadText { get; }
    public string UploadText { get; }
    public string UploadFootText { get; }
    public string NetworkText { get; }
    public string CapturedText { get; }
    public string SourceText { get; }
    public bool IsSample { get; }
    public bool IsGood { get; }
    public bool IsWarn { get; }
    public bool IsBad { get; }
    public IReadOnlyList<ShareScenarioCard> Scenarios { get; }

    private static string LocalizeScenarioName(string name) => name switch
    {
        "网页浏览" => LocalizationService.Current.T("L.WebBrowsing"),
        "4K 视频" => LocalizationService.Current.T("L.Video4K"),
        "在线游戏" => LocalizationService.Current.T("L.OnlineGaming"),
        "视频会议" => LocalizationService.Current.T("L.VideoMeeting"),
        "大文件下载" => LocalizationService.Current.T("L.LargeDownload"),
        _ => name,
    };

    private static string LocalizeScenarioHint(string hint) =>
        LocalizationService.Current.IsChinese ? hint : hint switch
    {
        "首屏加载与翻页" => "First paint and page turns",
        "拖拽与缓冲速度" => "Scrubbing and buffering",
        "操作延迟与命中" => "Input latency and hit registration",
        "语音连续性与画质" => "Voice continuity and quality",
        "上行未测，仅按延迟估算" => "Upload not measured; estimated from latency",
        "峰值吞吐能力" => "Peak throughput",
        "未测速" => "Not tested",
        _ => hint,
    };
}

public sealed class ShareScenarioCard
{
    public required string Name { get; init; }
    public required string Hint { get; init; }
    public required string ScoreText { get; init; }
    public bool IsMeasured { get; init; }
    public bool IsWarn { get; init; }
    public bool IsBad { get; init; }
}
