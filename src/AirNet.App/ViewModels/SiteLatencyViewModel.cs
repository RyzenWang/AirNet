using AirNet.Core.Probes;
using AirNet.Core.Sites;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirNet.App.ViewModels;

/// <summary>One row of the site table; a display projection so XAML stays format-free.</summary>
public sealed class SiteRow
{
    private const double BarWidth = 148;

    public required SiteTarget Target { get; init; }
    public required string Badge { get; init; }
    public bool IsInternational => Target.Group == SiteGroup.International;
    public bool IsCustom => Target.IsCustom;
    public string Name => Target.Name;
    public string Host => Target.Host;

    public bool HasResult { get; init; }
    public bool IsFailed { get; init; }
    public bool IsPending { get; init; }
    public bool IsGood { get; init; }
    public bool IsWarn { get; init; }
    public bool IsBad { get; init; }
    public string StatusText { get; init; } = "未测";
    public string TotalText { get; init; } = "—";
    public string UnitText { get; init; } = string.Empty;
    public string FailureText { get; init; } = string.Empty;
    public string DetailText => string.IsNullOrEmpty(FailureText) ? Host : FailureText;
    public string DeltaText { get; init; } = string.Empty;
    public bool HasDelta { get; init; }
    public bool IsDeltaDown { get; init; }
    public bool IsDeltaUp { get; init; }
    public bool IsDeltaBad { get; init; }
    public double DnsWidth { get; init; }
    public double TcpWidth { get; init; }
    public double TlsWidth { get; init; }
    public double WaitWidth { get; init; }

    public static SiteRow Pending(SiteTarget target) => new()
    {
        Target = target,
        Badge = SiteBadge.For(target),
        StatusText = "未测",
        IsPending = true,
    };

    public static SiteRow FromResult(SiteTarget target, SiteResult result, double? previousTotal)
    {
        var median = result.Median;
        var total = result.TotalMs;
        var httpBlocked = result.FailureKind == SiteFailureKind.HttpBlocked;
        var httpServerError = result.FailureKind == SiteFailureKind.HttpServerError;
        var status = !result.Success || httpServerError
            ? Severity.Bad
            : httpBlocked
                ? Severity.Warn
                : total > 200 ? Severity.Bad : total > 80 ? Severity.Warn : Severity.Ok;

        var delta = result.Success && !httpBlocked && !httpServerError && previousTotal is { } previous
            ? result.TotalMs - previous
            : (double?)null;
        var deltaValue = delta ?? 0;
        var hasDelta = delta is not null && Math.Abs(deltaValue) >= 0.5;
        var deltaText = hasDelta ? $"{(deltaValue < 0 ? "↓" : "↑")} {Math.Abs(deltaValue):0}" : string.Empty;

        return new SiteRow
        {
            Target = target,
            Badge = SiteBadge.For(target),
            HasResult = true,
            IsFailed = !result.Success,
            IsGood = status == Severity.Ok,
            IsWarn = status == Severity.Warn,
            IsBad = status == Severity.Bad,
            StatusText = !result.Success
                ? "异常"
                : httpBlocked
                    ? "HTTP 拦截"
                    : httpServerError
                        ? "HTTP 5xx"
                        : status == Severity.Ok ? "良好" : status == Severity.Warn ? "偏慢" : "异常",
            TotalText = result.Success ? Fmt.Compact(total) : "—",
            UnitText = result.Success ? "ms" : string.Empty,
            FailureText = !result.Success || httpBlocked || httpServerError
                ? result.FailureNote ?? "测试失败"
                : string.Empty,
            DeltaText = deltaText,
            HasDelta = hasDelta,
            IsDeltaDown = hasDelta && deltaValue < 0 && status != Severity.Bad,
            IsDeltaUp = hasDelta && deltaValue > 0 && status != Severity.Bad,
            IsDeltaBad = hasDelta && status == Severity.Bad,
            DnsWidth = Width(median?.DnsMs, total),
            TcpWidth = Width(median?.TcpMs, total),
            TlsWidth = Width(median?.TlsMs, total),
            WaitWidth = Width(median?.TtfbMs, total),
        };
    }

    private static double Width(double? value, double total) =>
        value is null || total <= 0 ? 0 : Math.Max(0, Math.Round(BarWidth * value.Value / total));

    private enum Severity
    {
        Ok,
        Warn,
        Bad,
    }
}

public sealed class SitePickRow
{
    public required SiteTarget Target { get; init; }
    public string Badge => SiteBadge.For(Target);
    public string Name => Target.Name;
    public string Host => Target.Host;
    public bool IsInternational => Target.Group == SiteGroup.International;
}

public sealed class CustomSiteRow
{
    public required SiteTarget Target { get; init; }
    public string Badge => SiteBadge.For(Target);
    public string Name => Target.Name;
    public string Host => Target.Host;
    public bool IsDeletePending { get; init; }
    public string DeleteLabel => IsDeletePending ? "确认删除" : "删除";
}

internal static class SiteBadge
{
    public static string For(SiteTarget target) => target.Id switch
    {
        "baidu" => "百",
        "tencent" => "腾",
        "douyin" => "抖",
        "xiaohongshu" => "小",
        "bilibili" => "哔",
        "zhihu" => "知",
        "weibo" => "微",
        "github" => "GH",
        "google" => "G",
        "youtube" => "Y",
        "x" => "X",
        _ => string.IsNullOrEmpty(target.Name) ? "?" : target.Name[..1].ToUpperInvariant(),
    };
}

/// <summary>Page state for the on-demand site latency test and its custom-site manager.</summary>
public partial class SiteLatencyViewModel : ObservableObject
{
    private const int SamplesPerSite = 3;
    private const int Concurrency = 4;
    private const int TimeoutMs = 4000;

    private readonly ShellViewModel _shell;
    private readonly CustomSiteStore _customStore = new();
    private readonly SiteHistoryStore _siteHistory = new();
    private readonly Dictionary<string, SiteResult> _results = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _previousTotals = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _ticker;
    private IReadOnlyList<SiteTarget> _customSites;
    private List<SiteTarget> _catalog;
    private CancellationTokenSource? _cts;
    private SiteLatencyReport? _lastReport;
    private DateTimeOffset _startedAt;
    private bool _sortByTotal;
    private string? _editingId;
    private string? _pendingDeleteId;

    public SiteLatencyViewModel(ShellViewModel shell)
    {
        _shell = shell;
        _customSites = _customStore.Load();
        _catalog = SiteCatalog.WithCustom(_customSites).ToList();
        DefaultDomestic = SiteCatalog.Default
            .Where(site => site.Group == SiteGroup.Domestic)
            .Select(site => new SitePickRow { Target = site })
            .ToList();
        DefaultInternational = SiteCatalog.Default
            .Where(site => site.Group == SiteGroup.International)
            .Select(site => new SitePickRow { Target = site })
            .ToList();
        _ticker = new DispatcherTimer(
            TimeSpan.FromMilliseconds(200),
            DispatcherPriority.Normal,
            (_, _) => OnTick());
        _ticker.Stop();
        UpdateCustomRows();
        RefreshRows();
        NotifyCounts();
        _ = LoadPreviousAsync();
    }

    public ShellViewModel Shell => _shell;
    public SiteLatencyReport? LastReport => _lastReport;

    [ObservableProperty]
    private bool _isTesting;

    /// <summary>True when the last run went through the system proxy, so the segment split is not direct.</summary>
    [ObservableProperty]
    private bool _isViaProxy;

    [ObservableProperty]
    private string _testStatusText = "还没有测试 · 点「开始测试」跑一轮";

    [ObservableProperty]
    private IReadOnlyList<SiteRow> _rows = [];

    [ObservableProperty]
    private string _filter = "All";

    [ObservableProperty]
    private bool _isManagerOpen;

    [ObservableProperty]
    private string _draftName = string.Empty;

    [ObservableProperty]
    private string _draftHost = string.Empty;

    [ObservableProperty]
    private string _draftError = string.Empty;

    [ObservableProperty]
    private string _draftTestText = string.Empty;

    [ObservableProperty]
    private bool _isTestingDraft;

    [ObservableProperty]
    private IReadOnlyList<CustomSiteRow> _customSiteRows = [];

    public IReadOnlyList<SitePickRow> DefaultDomestic { get; }
    public IReadOnlyList<SitePickRow> DefaultInternational { get; }

    public int AllCount => _catalog.Count;
    public int DomesticCount => _catalog.Count(site => site.Group == SiteGroup.Domestic);
    public int InternationalCount => _catalog.Count(site => site.Group == SiteGroup.International);
    public int CustomCount => _catalog.Count(site => site.IsCustom);
    public string AllCountText => $"全部 {AllCount}";
    public string DomesticCountText => $"国内 {DomesticCount}";
    public string InternationalCountText => $"国际 {InternationalCount}";
    public string CustomCountText => $"自定义 {CustomCount}";
    public string CustomLimitText => $"{CustomCount} / {CustomSiteRules.MaxCustomSites}";
    public string PageSubtitle =>
        $"默认 11 + 自定义 {CustomCount} · 3 次采样取中位数 · 只测首字节，不下载页面内容";
    public string TestButtonText => IsTesting ? "测试中…" : "重新测试";
    public bool IsFilterAll => Filter == "All";
    public bool IsFilterDomestic => Filter == "Domestic";
    public bool IsFilterInternational => Filter == "International";
    public bool IsFilterCustom => Filter == "Custom";
    public bool HasResults => _results.Count > 0;

    public string FastestValue
    {
        get
        {
            var fastest = Successful().MinBy(result => result.TotalMs);
            return fastest is null ? "—" : Fmt.Compact(fastest.TotalMs);
        }
    }

    public string FastestFoot
    {
        get
        {
            var fastest = Successful().MinBy(result => result.TotalMs);
            return fastest is null ? "等待测试" : $"{fastest.Target.Name} · {fastest.Target.Host}";
        }
    }

    public string SlowestValue
    {
        get
        {
            var slowest = Successful().MaxBy(result => result.TotalMs);
            return slowest is null ? "—" : Fmt.Compact(slowest.TotalMs);
        }
    }

    public string SlowestFoot
    {
        get
        {
            var slowest = Successful().MaxBy(result => result.TotalMs);
            return slowest is null ? "等待测试" : $"{slowest.Target.Name} · {slowest.Target.Host}";
        }
    }

    public string AverageValue
    {
        get
        {
            var results = Successful().ToList();
            return results.Count == 0 ? "—" : Math.Round(results.Average(result => result.TotalMs)).ToString("0");
        }
    }

    public string AverageFoot => Successful().Any()
        ? $"成功 {Successful().Count()} 站均值（含跨境）"
        : "等待测试";

    public string DoneValue
    {
        get
        {
            var completed = _results.Count;
            return completed == 0 ? "—" : $"{completed}/{_catalog.Count}";
        }
    }

    public string DoneFoot
    {
        get
        {
            var failed = _results.Values.Count(result =>
                !result.Success
                || result.FailureKind is SiteFailureKind.HttpBlocked or SiteFailureKind.HttpServerError);
            var duration = _lastReport?.Duration.TotalSeconds;
            return _results.Count == 0
                ? "失败 0 · 等待测试"
                : duration is null
                    ? $"失败 {failed}"
                    : $"失败 {failed} · 用时 {duration:0.0} s";
        }
    }

    public bool HasManagerError => !string.IsNullOrWhiteSpace(DraftError);
    public bool HasCustomSites => CustomSiteRows.Count > 0;
    public string DraftTitle => _editingId is null ? "新增自定义站点" : "编辑自定义站点";
    public string CustomSummary => $"默认 11 · 自定义 {CustomCount} / {CustomSiteRules.MaxCustomSites}";

    public string JudgementText
    {
        get
        {
            var domestic = Successful().Where(result => result.Target.Group == SiteGroup.Domestic).ToList();
            var international = Successful().Where(result => result.Target.Group == SiteGroup.International).ToList();
            if (domestic.Count == 0 && international.Count == 0)
            {
                return "跑一轮后，这里会把国内与国际分开判断；跨境慢通常不是本机网络的问题。";
            }

            var domesticTotal = _catalog.Count(site => site.Group == SiteGroup.Domestic);
            var internationalTotal = _catalog.Count(site => site.Group == SiteGroup.International);
            var domesticText = domestic.Count == 0
                ? $"国内 0/{domesticTotal} 站成功"
                : $"国内 {domestic.Count}/{domesticTotal} 站平均 {domestic.Average(result => result.TotalMs):0} ms";
            var internationalText = international.Count == 0
                ? $"国际 0/{internationalTotal} 站成功"
                : $"国际 {international.Count}/{internationalTotal} 站平均 {international.Average(result => result.TotalMs):0} ms";
            return $"{domesticText}；{internationalText}。国际站明显慢或不可达时优先看跨境/CDN，不直接归因本机。";
        }
    }

    partial void OnIsTestingChanged(bool value)
    {
        OnPropertyChanged(nameof(TestButtonText));

        if (value)
        {
            _startedAt = DateTimeOffset.Now;
            _ticker.Start();
        }
        else
        {
            _ticker.Stop();
        }
    }

    partial void OnFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterDomestic));
        OnPropertyChanged(nameof(IsFilterInternational));
        OnPropertyChanged(nameof(IsFilterCustom));
        RefreshRows();
    }

    partial void OnDraftErrorChanged(string value) => OnPropertyChanged(nameof(HasManagerError));

    partial void OnDraftNameChanged(string value)
    {
        if (!string.IsNullOrEmpty(DraftError))
        {
            DraftError = string.Empty;
        }
    }

    partial void OnDraftHostChanged(string value)
    {
        if (!string.IsNullOrEmpty(DraftError))
        {
            DraftError = string.Empty;
        }
    }

    [RelayCommand]
    private async Task RunTestAsync()
    {
        if (IsTesting)
        {
            return;
        }

        foreach (var result in _results.Values.Where(IsUsableResult))
        {
            _previousTotals[result.Target.Host] = result.TotalMs;
        }

        _results.Clear();
        _lastReport = null;
        _sortByTotal = false;
        TestStatusText = $"正在测试 {_catalog.Count} 个站点…";
        RefreshRows();
        NotifySummary();
        IsTesting = true;

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var progress = new Progress<SiteResult>(OnSiteResult);
        using var overall = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        overall.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            var report = await SiteLatencyProbe.RunAsync(
                _catalog,
                SamplesPerSite,
                Concurrency,
                TimeoutMs,
                progress,
                overall.Token).ConfigureAwait(true);

            _lastReport = report;
            IsViaProxy = report.ViaProxy;
            foreach (var result in report.Results)
            {
                _results[result.Target.Id] = result;
            }

            _sortByTotal = true;
            var timedOut = overall.IsCancellationRequested && !_cts.IsCancellationRequested;
            TestStatusText = _cts.IsCancellationRequested
                ? "测试已取消"
                : timedOut
                    ? $"部分站点超时 · {report.Succeeded}/{report.Results.Count} 成功 · 用时 {report.Duration.TotalSeconds:0.0} s"
                    : $"{report.Succeeded}/{report.Results.Count} 站点成功 · 用时 {report.Duration.TotalSeconds:0.0} s";
            RefreshRows();
            NotifySummary();

            if (_shell.Preferences.KeepHistory)
            {
                try
                {
                    await _siteHistory.AppendAsync(report).ConfigureAwait(true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A history write failure must not turn a valid measurement into an error.
                }
            }

            foreach (var result in report.Results.Where(IsUsableResult))
            {
                _previousTotals[result.Target.Host] = result.TotalMs;
            }
        }
        catch (OperationCanceledException)
        {
            TestStatusText = "测试已取消";
        }
        catch (Exception ex)
        {
            TestStatusText = $"测试失败：{ex.Message}";
        }
        finally
        {
            IsTesting = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void StopTest()
    {
        _cts?.Cancel();
        TestStatusText = "正在停止…";
    }

    [RelayCommand]
    private void SetFilter(string? filter) => Filter = filter ?? "All";

    [RelayCommand]
    private void OpenManager()
    {
        IsManagerOpen = true;
        DraftError = string.Empty;
        DraftTestText = string.Empty;
    }

    [RelayCommand]
    private void CloseManager()
    {
        IsManagerOpen = false;
        _editingId = null;
        _pendingDeleteId = null;
        DraftName = string.Empty;
        DraftHost = string.Empty;
        DraftError = string.Empty;
        DraftTestText = string.Empty;
        UpdateCustomRows();
    }

    [RelayCommand]
    private async Task TestDraftAsync()
    {
        if (!CustomSiteRules.TryNormalizeHost(DraftHost, out var host, out var error))
        {
            DraftError = error;
            return;
        }

        IsTestingDraft = true;
        DraftError = string.Empty;
        DraftTestText = "测试中…";
        try
        {
            var timing = await ApplicationTimingProbe.MeasureAsync(host, timeoutMs: 6000).ConfigureAwait(true);
            DraftTestText = timing.Succeeded
                ? $"DNS {timing.DnsMs:0} · TCP {timing.TcpMs:0} · TLS {timing.TlsMs:0} · 首包 {timing.TtfbMs:0} = {timing.TotalMs:0} ms"
                : timing.FailureNote ?? "测试失败";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            DraftTestText = $"测试失败：{ex.Message}";
        }
        finally
        {
            IsTestingDraft = false;
        }
    }

    [RelayCommand]
    private void SaveDraft()
    {
        if (ValidateDraft(out var name, out var host) is { } error)
        {
            DraftError = error;
            return;
        }

        var next = _customSites.ToList();

        if (_editingId is null && next.Count >= CustomSiteRules.MaxCustomSites)
        {
            DraftError = $"最多只能添加 {CustomSiteRules.MaxCustomSites} 个自定义站点";
            return;
        }

        var duplicate = CustomSiteRules.ValidateDuplicate(
            host,
            _catalog.Where(site => site.Id != _editingId));
        if (duplicate is not null)
        {
            DraftError = duplicate;
            return;
        }

        var addedAt = _editingId is { } id
            ? _customSites.FirstOrDefault(site => site.Id == id)?.AddedAt ?? DateTimeOffset.Now
            : DateTimeOffset.Now;
        var target = new SiteTarget(
            _editingId ?? Guid.NewGuid().ToString("N"),
            name,
            host,
            SiteGroup.Custom,
            IsCustom: true,
            AddedAt: addedAt);

        if (_editingId is { } editing)
        {
            var index = next.FindIndex(site => site.Id == editing);
            if (index >= 0)
            {
                next[index] = target;
            }
            else
            {
                next.Add(target);
            }
        }
        else
        {
            next.Add(target);
        }

        var persisted = PersistCustom(next);
        _editingId = null;
        DraftName = string.Empty;
        DraftHost = string.Empty;
        DraftError = persisted ? string.Empty : "站点已加入本次列表，但写入 sites.json 失败；重启后会丢失。";
        DraftTestText = string.Empty;
        _shell.StatusMessage = persisted
            ? $"已保存自定义站点：{target.Name}"
            : $"自定义站点已加入列表，但写入 sites.json 失败：{target.Name}";
    }

    public void EditCustom(SiteTarget target)
    {
        _editingId = target.Id;
        _pendingDeleteId = null;
        DraftName = target.Name;
        DraftHost = target.Host;
        DraftError = string.Empty;
        DraftTestText = string.Empty;
        IsManagerOpen = true;
        UpdateCustomRows();
    }

    public void RequestDeleteCustom(SiteTarget target)
    {
        if (_pendingDeleteId == target.Id)
        {
            var remaining = _customSites.Where(site => site.Id != target.Id).ToList();
            _pendingDeleteId = null;
            var persisted = PersistCustom(remaining);
            _shell.StatusMessage = persisted
                ? $"已删除自定义站点：{target.Name}"
                : $"自定义站点已从列表移除，但写入 sites.json 失败：{target.Name}";
            if (!persisted)
            {
                DraftError = "站点已从本次列表移除，但写入 sites.json 失败；重启后会恢复。";
            }

            return;
        }

        _pendingDeleteId = target.Id;
        UpdateCustomRows();
    }

    private string? ValidateDraft(out string name, out string host)
    {
        name = string.Empty;
        host = string.Empty;

        if (CustomSiteRules.ValidateName(DraftName) is { } nameError)
        {
            return nameError;
        }

        if (!CustomSiteRules.TryNormalizeHost(DraftHost, out host, out var hostError))
        {
            return hostError;
        }

        name = DraftName.Trim();
        return null;
    }

    private bool PersistCustom(IReadOnlyList<SiteTarget> sites)
    {
        var persisted = _customStore.Save(sites);
        _customSites = sites;
        _catalog = SiteCatalog.WithCustom(sites).ToList();
        foreach (var removed in _results.Keys.Where(id => _catalog.All(site => site.Id != id)).ToList())
        {
            _results.Remove(removed);
        }

        UpdateCustomRows();
        NotifyCounts();
        RefreshRows();
        NotifySummary();
        return persisted;
    }

    private void UpdateCustomRows()
    {
        CustomSiteRows = _customSites
            .Select(site => new CustomSiteRow
            {
                Target = site,
                IsDeletePending = _pendingDeleteId == site.Id,
            })
            .ToList();
        OnPropertyChanged(nameof(CustomLimitText));
        OnPropertyChanged(nameof(CustomSummary));
        OnPropertyChanged(nameof(HasCustomSites));
        OnPropertyChanged(nameof(DraftTitle));
    }

    private void NotifyCounts()
    {
        OnPropertyChanged(nameof(AllCount));
        OnPropertyChanged(nameof(DomesticCount));
        OnPropertyChanged(nameof(InternationalCount));
        OnPropertyChanged(nameof(CustomCount));
        OnPropertyChanged(nameof(AllCountText));
        OnPropertyChanged(nameof(DomesticCountText));
        OnPropertyChanged(nameof(InternationalCountText));
        OnPropertyChanged(nameof(CustomCountText));
        OnPropertyChanged(nameof(CustomLimitText));
        OnPropertyChanged(nameof(CustomSummary));
        OnPropertyChanged(nameof(PageSubtitle));
    }

    private void NotifySummary()
    {
        OnPropertyChanged(nameof(FastestValue));
        OnPropertyChanged(nameof(FastestFoot));
        OnPropertyChanged(nameof(SlowestValue));
        OnPropertyChanged(nameof(SlowestFoot));
        OnPropertyChanged(nameof(AverageValue));
        OnPropertyChanged(nameof(AverageFoot));
        OnPropertyChanged(nameof(DoneValue));
        OnPropertyChanged(nameof(DoneFoot));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(JudgementText));
    }

    private void OnSiteResult(SiteResult result)
    {
        _results[result.Target.Id] = result;
        RefreshRows();
        NotifySummary();
    }

    private void OnTick()
    {
        if (!IsTesting)
        {
            return;
        }

        TestStatusText = $"正在测试 · {(DateTimeOffset.Now - _startedAt).TotalSeconds:0.0} s";
    }

    private IEnumerable<SiteResult> Successful() =>
        _results.Values
            .Where(IsUsableResult)
            .OrderBy(result => result.TotalMs);

    private async Task LoadPreviousAsync()
    {
        if (!_shell.Preferences.KeepHistory)
        {
            return;
        }

        try
        {
            var report = await _siteHistory.LoadLatestAsync().ConfigureAwait(true);
            if (report is null)
            {
                return;
            }

            _lastReport ??= report;
            foreach (var result in report.Results.Where(IsUsableResult))
            {
                _previousTotals[result.Target.Host] = result.TotalMs;
            }

            if (_results.Count > 0)
            {
                RefreshRows();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No previous run is not an error; the page simply starts without a delta.
        }
    }

    private static bool IsUsableResult(SiteResult result) =>
        result.Success
        && result.FailureKind is not (SiteFailureKind.HttpBlocked or SiteFailureKind.HttpServerError);

    /// <summary>
    /// Fills the table from a sample report instead of a live run. Used only by the screenshot
    /// pipeline: the page hydrates history for its delta column but never renders history rows,
    /// so without this its documentation would be eleven empty lines.
    /// </summary>
    public void ApplySampleReport(SiteLatencyReport report)
    {
        _lastReport = report;
        foreach (var result in report.Results)
        {
            _results[result.Target.Id] = result;
        }

        _sortByTotal = true;
        TestStatusText = $"{report.Succeeded}/{report.Results.Count} 站点成功 · 示例数据";
        RefreshRows();
        NotifySummary();
    }

    private void RefreshRows()
    {
        var targets = _catalog.Where(MatchesFilter).ToList();
        if (_sortByTotal)
        {
            targets = targets
                .OrderBy(target => _results.TryGetValue(target.Id, out var result) && IsUsableResult(result)
                    ? result.TotalMs
                    : double.MaxValue)
                .ThenBy(target => _results.TryGetValue(target.Id, out var result)
                    ? IsUsableResult(result) ? 0 : result.Success ? 1 : 2
                    : 3)
                .ThenBy(target => _catalog.IndexOf(target))
                .ToList();
        }

        Rows = targets.Select(target =>
        {
            if (!_results.TryGetValue(target.Id, out var result))
            {
                return SiteRow.Pending(target);
            }

            double? previous = _previousTotals.TryGetValue(target.Host, out var value) ? value : null;
            return SiteRow.FromResult(target, result, previous);
        }).ToList();
    }

    private bool MatchesFilter(SiteTarget target) => Filter switch
    {
        "Domestic" => target.Group == SiteGroup.Domestic,
        "International" => target.Group == SiteGroup.International,
        "Custom" => target.IsCustom,
        _ => true,
    };
}
