using AirNet.Core.Diagnostics;
using AirNet.Core.Probes;
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
    public PathViewModel(ShellViewModel shell)
    {
        Shell = shell;
        _attribution = AttributionEngine.FromSnapshot(shell.Snapshot);
        Segments = BuildSegments(_attribution);
    }

    public ShellViewModel Shell { get; }

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
    private string _statusText = "先用上次检测做粗定位；追踪会给出逐跳证据";
    [ObservableProperty]
    private string _mtuText = "—";

    [ObservableProperty]
    private string _mtuNote = "与追踪一起测量";

    [ObservableProperty]
    private string _reachedText = "—";

    [ObservableProperty]
    private string _spikeText = "—";

    [ObservableProperty]
    private string _traceModeText = "粗定位";

    public bool HasHops => Hops.Count > 0;

    public string TargetText => Shell.ReferenceHost;

    public string Verdict => Attribution.Headline;

    public string VerdictDetail => Attribution.Detail;

    public bool IsWarn => Attribution.Severity == Severity.Warn;

    public bool IsGood => Attribution.Severity == Severity.Ok;

    /// <summary>Called by the shell after every scan so the coarse verdict follows the data.</summary>
    public void RefreshFromSnapshot()
    {
        if (Attribution.IsPrecise)
        {
            return;   // a precise trace outranks a coarse re-read
        }

        Attribution = AttributionEngine.FromSnapshot(Shell.Snapshot);
        Segments = BuildSegments(Attribution);
        OnPropertyChanged(nameof(Verdict));
        OnPropertyChanged(nameof(VerdictDetail));
        OnPropertyChanged(nameof(IsWarn));
        OnPropertyChanged(nameof(IsGood));
    }

    [RelayCommand]
    private async Task RunTraceAsync()
    {
        if (IsTracing)
        {
            return;
        }

        IsTracing = true;
        StatusText = $"正在追踪 {TargetText}（最多 16 跳，约 8–20 秒）…";

        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var trace = await TraceProbe.TraceAsync(TargetText).ConfigureAwait(true);
            var mtu = await MtuProbe.MeasureAsync(TargetText).ConfigureAwait(true);
            watch.Stop();

            Apply(trace, mtu);

            StatusText = trace.Succeeded
                ? $"已追踪 {trace.Hops.Count} 跳 · 用时 {watch.Elapsed.TotalSeconds:0.0} s"
                : trace.Note ?? "追踪失败";
        }
        catch (Exception ex)
        {
            StatusText = $"追踪失败：{ex.Message}";
        }
        finally
        {
            IsTracing = false;
        }
    }

    private void Apply(TraceResult trace, MtuResult mtu)
    {
        Attribution = AttributionEngine.FromTrace(Shell.Snapshot, trace, mtu);
        Segments = BuildSegments(Attribution);
        Hops = BuildHops(trace);
        MtuText = mtu.MtuBytes is { } mtuValue ? mtuValue.ToString() : "—";
        MtuNote = mtu.Note;
        ReachedText = trace.Hops.Count == 0 ? "—" : $"{trace.ReachedHop} 跳";
        SpikeText = trace.SpikeIndex is { } spike ? $"第 {spike} 跳" : "无明显突变";
        TraceModeText = trace.Succeeded ? "逐跳追踪" : "粗定位";

        OnPropertyChanged(nameof(Verdict));
        OnPropertyChanged(nameof(VerdictDetail));
        OnPropertyChanged(nameof(IsWarn));
        OnPropertyChanged(nameof(IsGood));
    }

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
            LatencyText = hop.Responded ? $"{hop.LatencyMs:0.0} ms" : "超时",
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
