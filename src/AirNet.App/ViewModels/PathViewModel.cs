using AirNet.App.Localization;
using AirNet.Core.Diagnostics;
using AirNet.Core.Probes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirNet.App.ViewModels;

/// <summary>One row of the hop table.</summary>
public sealed class TraceHopRow
{
    public required string IndexText { get; init; }
    public required string Address { get; init; }
    public required string LatencyText { get; init; }
    public required double Bar { get; init; }
    public bool IsSpike { get; init; }
    public bool IsTimeout { get; init; }
}

/// <summary>One link of the chain, rendered as a column.</summary>
public sealed class SegmentRow
{
    public required string Name { get; init; }
    public required string LatencyText { get; init; }
    public required string DeltaText { get; init; }
    public required string Note { get; init; }
    public bool IsGood { get; init; }
    public bool IsWarn { get; init; }
    public bool IsBad { get; init; }
    public bool IsInfo { get; init; }
}

/// <summary>
/// "Where is the problem" page. Starts with the coarse split from the last scan, and sharpens
/// into a per-hop verdict once the user asks for a trace (which takes seconds, so it is never
/// part of the 3-second scan).
/// </summary>
public partial class PathViewModel : ObservableObject
{
    private readonly DispatcherTimer _traceTicker;
    private DateTimeOffset _traceStartedAt;

    public PathViewModel(ShellViewModel shell)
    {
        Shell = shell;
        _attribution = shell.View.IsPending
            ? PendingAttribution()
            : LocalizeAttribution(AttributionEngine.FromSnapshot(shell.Snapshot));
        Segments = BuildSegments(_attribution);
        StatusText = shell.View.IsPending ? T("L.PathPending") : T("L.PathCoarseHint");
        TraceModeText = shell.View.IsPending ? T("L.PathWaiting") : T("L.Coarse");
        _traceTicker = new DispatcherTimer(
            TimeSpan.FromMilliseconds(200),
            DispatcherPriority.Normal,
            (_, _) => OnTraceTick());
        _traceTicker.Stop();
    }

    public ShellViewModel Shell { get; }

    /// <summary>Last MTU measurement, reused by the report exporter.</summary>
    public MtuResult LastMtu { get; private set; } = MtuResult.Unavailable(T("L.NotMeasured"));

    [ObservableProperty]
    private Attribution _attribution;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHops))]
    private IReadOnlyList<TraceHopRow> _hops = [];

    [ObservableProperty]
    private IReadOnlyList<SegmentRow> _segments = [];

    [ObservableProperty]
    private bool _isTracing;

    [ObservableProperty]
    private string _statusText = T("L.PathCoarseHint");
    [ObservableProperty]
    private string _mtuText = "—";

    [ObservableProperty]
    private string _mtuNote = T("L.MtuWithTrace");

    [ObservableProperty]
    private string _reachedText = "—";

    [ObservableProperty]
    private string _spikeText = "—";

    [ObservableProperty]
    private string _traceModeText = T("L.Coarse");

    public bool HasHops => Hops.Count > 0;

    public string TargetText => Shell.ReferenceHost;

    public string Verdict => Attribution.Headline;

    public string VerdictDetail => Attribution.Detail;

    public bool IsWarn => Attribution.Severity == Severity.Warn;

    public bool IsGood => Attribution.Severity == Severity.Ok;

    /// <summary>Called by the shell after every scan so the coarse verdict follows the data.</summary>
    public void RefreshFromSnapshot()
    {
        if (Shell.View.IsPending)
        {
            Attribution = PendingAttribution();
            Segments = BuildSegments(Attribution);
            StatusText = T("L.PathPending");
            TraceModeText = T("L.PathWaiting");
            OnPropertyChanged(nameof(Verdict));
            OnPropertyChanged(nameof(VerdictDetail));
            OnPropertyChanged(nameof(IsWarn));
            OnPropertyChanged(nameof(IsGood));
            return;
        }

        if (Attribution.IsPrecise)
        {
            return;   // a precise trace outranks a coarse re-read
        }

        Attribution = LocalizeAttribution(AttributionEngine.FromSnapshot(Shell.Snapshot));
        Segments = BuildSegments(Attribution);
        StatusText = T("L.PathCoarseHint");
        TraceModeText = T("L.Coarse");
        OnPropertyChanged(nameof(Verdict));
        OnPropertyChanged(nameof(VerdictDetail));
        OnPropertyChanged(nameof(IsWarn));
        OnPropertyChanged(nameof(IsGood));
    }

    private static Attribution PendingAttribution() => new(
        "pending",
        T("L.PathWaiting"),
        T("L.PathPendingDetail"),
        Severity.Info,
        [
            new PathSegment(T("L.SegmentLan"), T("L.NotMeasured"), T("L.LocalToGateway"), Severity.Info, T("L.PathWaiting")),
            new PathSegment(T("L.SegmentIsp"), T("L.NotMeasured"), T("L.GatewayToRef"), Severity.Info, T("L.PathWaiting")),
            new PathSegment(T("L.SegmentFar"), T("L.NotMeasured"), T("L.TraceNeeded"), Severity.Info, T("L.PathWaiting")),
        ],
        IsPrecise: false);

    partial void OnIsTracingChanged(bool value)
    {
        if (value)
        {
            _traceStartedAt = DateTimeOffset.Now;
            _traceTicker.Start();
        }
        else
        {
            _traceTicker.Stop();
        }
    }

    private void OnTraceTick()
    {
        if (!IsTracing)
        {
            return;
        }

        StatusText = Fmt("L.Tracing", TargetText, (DateTimeOffset.Now - _traceStartedAt).TotalSeconds);
    }

    [RelayCommand]
    private async Task RunTraceAsync()
    {
        if (IsTracing)
        {
            return;
        }

        IsTracing = true;
        StatusText = Fmt("L.TracingStart", TargetText);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var trace = await TraceProbe.TraceAsync(TargetText).ConfigureAwait(true);
            var mtu = await MtuProbe.MeasureAsync(TargetText).ConfigureAwait(true);
            watch.Stop();

            Apply(trace, mtu);

            StatusText = !trace.Succeeded
                ? trace.Note ?? T("L.TraceFailed")
                : trace.ReachedTarget == false
                    ? Fmt("L.TraceDoneUnreached", trace.Hops.Count, watch.Elapsed.TotalSeconds)
                    : Fmt("L.TraceDone", trace.Hops.Count, watch.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            StatusText = Fmt("L.TraceFailedDetail", ex.Message);
        }
        finally
        {
            IsTracing = false;
        }
    }

    private void Apply(TraceResult trace, MtuResult mtu)
    {
        LastMtu = mtu;
        Attribution = LocalizeAttribution(AttributionEngine.FromTrace(Shell.Snapshot, trace, mtu));
        Segments = BuildSegments(Attribution);
        Hops = BuildHops(trace);
        MtuText = mtu.MtuBytes is { } mtuValue ? mtuValue.ToString() : "—";
        MtuNote = LocalizationService.Current.IsChinese ? mtu.Note : T("L.MtuWithTrace");
        ReachedText = trace.Hops.Count == 0 ? "—" : Fmt("L.HopsFormat", trace.ReachedHop);
        SpikeText = trace.SpikeIndex is { } spike ? Fmt("L.SpikeHop", spike) : T("L.NoSpike");
        TraceModeText = !trace.Succeeded
            ? T("L.Coarse")
            : trace.ReachedTarget == false ? T("L.HopByHopUnreached") : T("L.HopByHopTrace");

        OnPropertyChanged(nameof(Verdict));
        OnPropertyChanged(nameof(VerdictDetail));
        OnPropertyChanged(nameof(IsWarn));
        OnPropertyChanged(nameof(IsGood));
    }

    private static string T(string key) => LocalizationService.Current.T(key);

    private static string Fmt(string key, params object[] args) =>
        LocalizationService.Current.Format(key, args);

    private static Attribution LocalizeAttribution(Attribution attribution)
    {
        if (LocalizationService.Current.IsChinese)
        {
            return attribution;
        }

        var (headline, detail) = attribution.Culprit switch
        {
            "局域网" => (T("L.LanBottleneck"), T("L.LanDetail")),
            "运营商" => (T("L.IspBottleneck"), T("L.IspDetail")),
            "对端" => (T("L.FarBottleneck"), T("L.FarDetail")),
            _ => (T("L.NoObviousBottleneck"), T("L.PathHealthyDetail")),
        };

        return attribution with
        {
            Culprit = attribution.Culprit switch
            {
                "局域网" => "LAN",
                "运营商" => "ISP",
                "对端" => "Far end",
                "无" => "none",
                _ => attribution.Culprit,
            },
            Headline = headline,
            Detail = detail,
            Segments = attribution.Segments.Select(segment => new PathSegment(
                LocalizeSegmentName(segment.Name),
                segment.LatencyText == "未测" ? T("L.NotMeasured") : segment.LatencyText,
                LocalizeSegmentDelta(segment.DeltaText),
                segment.Severity,
                LocalizeSegmentNote(segment.Note))).ToList(),
        };
    }

    private static string LocalizeSegmentName(string name) => name switch
    {
        "局域网" => T("L.SegmentLan"),
        "运营商" => T("L.SegmentIsp"),
        "对端" => T("L.SegmentFar"),
        _ => name,
    };

    private static string LocalizeSegmentDelta(string delta)
    {
        if (delta.StartsWith("本机 → ", StringComparison.Ordinal))
        {
            return "Local → " + delta["本机 → ".Length..];
        }

        if (delta.StartsWith("网关 → ", StringComparison.Ordinal))
        {
            return "Gateway → " + delta["网关 → ".Length..];
        }

        if (delta.StartsWith("接入 → ", StringComparison.Ordinal))
        {
            return "Access → " + delta["接入 → ".Length..];
        }

        if (delta.StartsWith("最后一跳 ", StringComparison.Ordinal))
        {
            return "Last hop " + delta["最后一跳 ".Length..];
        }

        if (delta.StartsWith("追踪止于第 ", StringComparison.Ordinal))
        {
            return "Trace stopped at hop " + delta["追踪止于第 ".Length..];
        }

        if (delta.StartsWith("目标 ", StringComparison.Ordinal) && delta.EndsWith(" 未响应", StringComparison.Ordinal))
        {
            return "Target " + delta["目标 ".Length..^" 未响应".Length] + " did not respond";
        }

        return delta switch
        {
            "需逐跳追踪" => T("L.TraceNeeded"),
            "未测" => T("L.NotMeasured"),
            _ => delta,
        };
    }

    private static string LocalizeSegmentNote(string note) => note switch
    {
        "网关可达" => T("L.GatewayReachable"),
        "网关未响应" => T("L.GatewayDown"),
        "正常" => T("L.Normal"),
        "这一段偏长" => T("L.LongSegment"),
        "运行追踪以定位最后一跳" => T("L.TraceLastHop"),
        "等待检测" => T("L.PathWaiting"),
        _ when note.StartsWith("追踪止于第 ", StringComparison.Ordinal) =>
            "Trace stopped at hop " + note["追踪止于第 ".Length..],
        "接入段很快" => T("L.AccessFast"),
        "骨干段正常" => T("L.BackboneNormal"),
        "接入段偏慢" => T("L.AccessSlow"),
        "网关本身慢，先看无线/路由" => T("L.GatewaySlow"),
        "无法判断接入段" => T("L.AccessUnknown"),
        "跨网段出现明显增量" => T("L.BackboneJump"),
        "对端响应正常" => T("L.FarNormal"),
        "增量出现在最后一跳" => T("L.FarJump"),
        _ => note,
    };

    private static IReadOnlyList<TraceHopRow> BuildHops(TraceResult trace)
    {
        var max = trace.Hops
            .Where(h => h.LatencyMs is not null)
            .Select(h => h.LatencyMs!.Value)
            .DefaultIfEmpty(1)
            .Max();

        return trace.Hops.Select(hop => new TraceHopRow
        {
            IndexText = hop.Index.ToString("00"),
            Address = hop.Responded ? hop.Address : "—",
            LatencyText = hop.Responded ? $"{hop.LatencyMs:0.0} ms" : T("L.Timeout"),
            Bar = hop.Responded ? Math.Clamp(hop.LatencyMs!.Value / max, 0.04, 1) : 0,
            IsSpike = trace.SpikeIndex == hop.Index,
            IsTimeout = !hop.Responded,
        }).ToList();
    }

    private static IReadOnlyList<SegmentRow> BuildSegments(Attribution attribution)
    {
        return attribution.Segments.Select(segment => new SegmentRow
        {
            Name = segment.Name,
            LatencyText = segment.LatencyText,
            DeltaText = segment.DeltaText,
            Note = segment.Note,
            IsGood = segment.Severity == Severity.Ok,
            IsWarn = segment.Severity == Severity.Warn,
            IsBad = segment.Severity == Severity.Bad,
            IsInfo = segment.Severity == Severity.Info,
        }).ToList();
    }
}
