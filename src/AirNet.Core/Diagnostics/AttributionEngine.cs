namespace AirNet.Core.Diagnostics;

/// <summary>
/// Decides where the problem sits. Two levels of confidence: a coarse split from the main scan
/// (gateway vs reference host) and a precise one from a traceroute, where each hop's delta shows
/// the segment that added the latency.
/// </summary>
public static class AttributionEngine
{
    public static Attribution FromSnapshot(NetworkSnapshot snapshot)
    {
        var gateway = snapshot.Hops.FirstOrDefault(h => !h.IsTerminal);
        var lanLatency = gateway?.Responded == true ? gateway.LatencyMs : 0;
        var total = snapshot.Latency.AverageMs;
        var wanDelta = Math.Max(0, total - lanLatency);

        var lanSeverity = lanLatency switch
        {
            > 12 => Severity.Bad,
            > 6 => Severity.Warn,
            _ => Severity.Ok,
        };

        var wanSeverity = wanDelta switch
        {
            > 80 => Severity.Warn,
            > 40 => Severity.Info,
            _ => Severity.Ok,
        };

        var segments = new List<PathSegment>
        {
            new("局域网", lanLatency > 0 ? $"+{lanLatency:0.0} ms" : "未测",
                $"本机 → {gateway?.Address ?? "网关"}",
                gateway?.Responded == true ? lanSeverity : Severity.Warn,
                gateway?.Responded == true ? "网关可达" : "网关未响应"),
            new("运营商", $"+{wanDelta:0.0} ms",
                "网关 → 参考主机",
                wanSeverity,
                wanDelta > 40 ? "这一段偏长" : "正常"),
            new("对端", "未测", "需逐跳追踪",
                Severity.Info,
                "运行追踪以定位最后一跳"),
        };

        var culprit = lanSeverity != Severity.Ok
            ? "局域网"
            : snapshot.Latency.LossPercent >= 2 && gateway?.Responded == true
                ? "运营商"
                : wanDelta > 60
                    ? "运营商"
                    : "无";

        return Build(culprit, segments, precise: false, total);
    }

    public static Attribution FromTrace(NetworkSnapshot snapshot, TraceResult trace, MtuResult? mtu = null)
    {
        if (!trace.Succeeded || trace.Hops.Count == 0)
        {
            return FromSnapshot(snapshot);
        }

        var responded = trace.Hops.Where(h => h.Responded && h.LatencyMs is not null).ToList();
        if (responded.Count == 0)
        {
            return FromSnapshot(snapshot);
        }

        // The gateway often refuses TTL-expired probes, so the local segment is judged by the
        // first few hops that do answer. A silent router is not evidence of a slow LAN.
        var lanHop = trace.Hops
            .Where(h => h.Index <= 3 && h.Responded && h.LatencyMs is not null)
            .OrderBy(h => h.Index)
            .FirstOrDefault();

        var last = responded[^1];
        var middleHops = responded
            .Where(h => h.Index > (lanHop?.Index ?? 0) && h.Index < last.Index)
            .ToList();

        var lanLatency = lanHop?.LatencyMs ?? 0;
        var middleLatency = middleHops.Count > 0 ? Median(middleHops.Select(h => h.LatencyMs!.Value)) : lanLatency;
        var targetLatency = last.LatencyMs!.Value;

        // A trace that never answers from the target only proves where the path stopped being
        // observable. Treating that last router as the far end is how a silent target becomes a
        // false "对端" verdict, so the far-end segment is only scored when the target answered.
        var reachedTarget = trace.ReachedTarget != false;

        var carrierDelta = Math.Max(0, middleLatency - lanLatency);
        var farEndDelta = reachedTarget ? Math.Max(0, targetLatency - middleLatency) : 0;
        // The trace measures its own path, so its last hop is the honest total even when the
        // dashboard is still showing an older (or sample) snapshot.
        var total = targetLatency;
        var loss = snapshot.Latency.LossPercent;

        // A 35 ms path that is spread over the carrier is not a problem; only flag a segment when
        // the whole path is slow enough for the user to feel it.
        var isSlow = total > 60 || loss >= 2;

        var lanSeverity = lanHop is null
            ? Severity.Info
            : lanLatency > 10 ? Severity.Bad : lanLatency > 5 ? Severity.Warn : Severity.Ok;
        var carrierSeverity = carrierDelta > 60
            ? Severity.Warn
            : carrierDelta > 30 && isSlow ? Severity.Warn : Severity.Ok;
        var farEndSeverity = !reachedTarget
            ? Severity.Info
            : farEndDelta > 60
                ? Severity.Warn
                : farEndDelta > 30 && isSlow ? Severity.Warn : Severity.Ok;

        var segments = new List<PathSegment>
        {
            new("局域网",
                $"+{lanLatency:0.0} ms",
                lanHop is null ? "网关不响应 TTL 探测" : $"本机 → {lanHop.Address}",
                lanSeverity,
                lanSeverity switch
                {
                    Severity.Ok => "接入段很快",
                    Severity.Warn => "接入段偏慢",
                    Severity.Bad => "网关本身慢，先看无线/路由",
                    _ => "无法判断接入段",
                }),
            new("运营商",
                $"+{carrierDelta:0.0} ms",
                reachedTarget ? $"接入 → 第 {last.Index} 跳前" : $"接入 → 第 {last.Index} 跳",
                carrierSeverity,
                carrierSeverity == Severity.Ok ? "骨干段正常" : "跨网段出现明显增量"),
            new("对端",
                reachedTarget ? $"+{farEndDelta:0.0} ms" : "未测",
                reachedTarget ? $"最后一跳 {last.Address}" : $"目标 {trace.Target} 未响应",
                farEndSeverity,
                reachedTarget
                    ? farEndSeverity == Severity.Ok ? "对端响应正常" : "增量出现在最后一跳"
                    : $"追踪止于第 {last.Index} 跳 {last.Address}"),
        };

        var culprit = lanSeverity == Severity.Bad
            ? "局域网"
            : carrierSeverity != Severity.Ok
                ? "运营商"
                : reachedTarget && farEndSeverity != Severity.Ok
                    ? "对端"
                    : "无";

        var attribution = Build(culprit, segments, precise: true, total);

        if (!reachedTarget)
        {
            var note = $"逐跳追踪到第 {last.Index} 跳（{last.Address}）后停止，目标 {trace.Target} 未响应，是否可达未知。";
            attribution = attribution with
            {
                Headline = culprit == "无" ? "前段无明显瓶颈，目标未响应" : attribution.Headline,
                Detail = culprit == "无"
                    ? $"{note}前段延迟均匀，问题更可能在目标侧或路径末段。"
                    : $"{attribution.Detail} {note}",
            };
        }

        if (mtu is { Succeeded: true, MtuBytes: < 1450 })
        {
            return attribution with
            {
                Detail = attribution.Detail + $" 另外 MTU 只有 {mtu.MtuBytes}，可能影响大包传输。",
            };
        }

        return attribution;
    }

    private static Attribution Build(
        string culprit,
        IReadOnlyList<PathSegment> segments,
        bool precise,
        double totalLatency)
    {
        var headline = culprit switch
        {
            "局域网" => "瓶颈在局域网",
            "运营商" => "瓶颈在运营商",
            "对端" => "瓶颈在对端服务",
            _ => "整条路径没有明显瓶颈",
        };

        var detail = culprit switch
        {
            "局域网" => "从本机到网关就已经变慢，先检查无线信号、路由负载或本机到路由的链路。",
            "运营商" => "网关很快，但出网后延迟陡增，属于运营商或跨网互联问题，可换 DNS/错峰/报障。",
            "对端" => "到目标之前的每一跳都正常，延迟增量出现在最后一跳，问题在对方。",
            _ => $"逐跳增量均匀（总计 {totalLatency:0.0} ms），网络本身健康，慢的原因更可能在应用侧。",
        };

        var severity = culprit == "无" ? Severity.Ok : Severity.Warn;
        var precision = precise ? "逐跳追踪" : "粗定位（未逐跳）";

        return new Attribution(
            culprit,
            headline,
            $"{detail}（{precision}）",
            severity,
            segments,
            precise);
    }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(v => v).ToArray();
        if (ordered.Length == 0)
        {
            return 0;
        }

        var mid = ordered.Length / 2;
        return ordered.Length % 2 == 0 ? (ordered[mid - 1] + ordered[mid]) / 2 : ordered[mid];
    }
}
