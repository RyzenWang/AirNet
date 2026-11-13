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
        UploadFootText = view.HasBandwidth ? $"上行 {view.UploadText}" : "未测速";
        NetworkText = view.SsidText;
        CapturedText = view.CapturedDateText;
        IsSample = view.IsSample;
        SourceText = view.IsSample ? "示例数据" : "本机实测";
        IsGood = view.IsGood;
        IsWarn = view.IsWarn;
        IsBad = view.IsBad;
        Scenarios = view.Scenarios
            .Select(row => new ShareScenarioCard
            {
                Name = row.Name,
                Hint = row.Hint,
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
