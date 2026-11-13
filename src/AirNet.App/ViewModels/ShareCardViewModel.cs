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
        NetworkText = view.SsidText;
        CapturedText = view.CapturedDateText;
        IsSample = view.IsSample;
        SourceText = view.IsSample ? "示例数据" : "本机实测";
        IsGood = view.IsGood;
        IsWarn = view.IsWarn;
        IsBad = view.IsBad;
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
    public string NetworkText { get; }
    public string CapturedText { get; }
    public string SourceText { get; }
    public bool IsSample { get; }
    public bool IsGood { get; }
    public bool IsWarn { get; }
    public bool IsBad { get; }
}
