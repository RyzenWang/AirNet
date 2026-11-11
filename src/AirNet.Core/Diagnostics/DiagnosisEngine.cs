namespace AirNet.Core.Diagnostics;

/// <summary>
/// Turns raw measurements into a 0-100 health score, graded scenario estimates and a plain
/// language diagnosis. Deliberately rule based: every number the UI shows can be explained,
/// and the thresholds can be recalibrated against field data without a model server.
/// </summary>
public static class DiagnosisEngine
{
    public static NetworkSnapshot Evaluate(
        MeasurementResult raw,
        double downloadMbps,
        double uploadMbps)
    {
        var latency = raw.Latency;
        var score = Score(latency, raw.Interface);
        var grade = GradeOf(score);
        var scenarios = Scenarios(latency, downloadMbps, uploadMbps);
        var findings = Findings(latency, raw.Interface, raw.DnsMs);

        return new NetworkSnapshot(
            score,
            grade,
            GradeLabel(grade),
            VerdictOf(grade),
            SummaryOf(latency, raw.Interface, downloadMbps),
            raw.Interface,
            raw.EgressAddress,
            latency,
            raw.DnsMs,
            raw.LatencySeries,
            raw.Hops,
            scenarios,
            findings,
            downloadMbps,
            uploadMbps,
            raw.CapturedAt);
    }

    public static HealthGrade GradeOf(int score) => score switch
    {
        >= 90 => HealthGrade.Excellent,
        >= 75 => HealthGrade.Good,
        >= 60 => HealthGrade.Fair,
        _ => HealthGrade.Poor,
    };

    public static string GradeLabel(HealthGrade grade) => grade switch
    {
        HealthGrade.Excellent => "优秀",
        HealthGrade.Good => "良好",
        HealthGrade.Fair => "一般",
        _ => "较差",
    };

    public static string VerdictOf(HealthGrade grade) => grade switch
    {
        HealthGrade.Excellent => "网络状态优秀",
        HealthGrade.Good => "网络状态良好",
        HealthGrade.Fair => "网络状态一般",
        _ => "网络状态较差",
    };

    private static int Score(LatencyStats stats, InterfaceInfo nic)
    {
        // Each penalty is capped so one bad dimension cannot zero the whole score; the
        // weights follow what users feel: loss > jitter > latency > radio strength.
        var latencyPenalty = Clamp((stats.AverageMs - 12) * 0.42, 0, 26);
        var jitterPenalty = Clamp((stats.JitterMs - 1.5) * 1.1, 0, 14);
        var lossPenalty = Clamp(stats.LossPercent * 9, 0, 38);
        var radioPenalty = nic.IsWireless && nic.SignalPercent is { } signal
            ? Clamp((70 - signal) * 0.12, 0, 12)
            : 0;

        var raw = 100 - latencyPenalty - jitterPenalty - lossPenalty - radioPenalty;
        return (int)Math.Round(Clamp(raw, 0, 100), MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyList<ScenarioReading> Scenarios(LatencyStats s, double down, double up)
    {
        var latency = s.AverageMs;
        var jitter = s.JitterMs;
        var loss = s.LossPercent;

        return
        [
            new("网页浏览", Pct(100 - (latency * 0.22) - (jitter * 0.7) - (loss * 5)),
                "首屏加载与翻页"),
            new("4K 视频", Pct(100 - (loss * 8) - (latency > 80 ? (latency - 80) * 0.3 : 0)),
                "拖拽与缓冲速度"),
            new("在线游戏", Pct(96 - (latency * 0.5) - (jitter * 1.5) - (loss * 7)),
                "操作延迟与命中"),
            new("视频会议", Pct(100 - (jitter * 1.4) - (loss * 7) - (up < 8 ? (8 - up) * 2 : 0)),
                "语音连续性与画质"),
            new("大文件下载", Pct(Math.Min(100, 52 + (down * 0.12))),
                "峰值吞吐能力"),
        ];
    }

    private static IReadOnlyList<Finding> Findings(LatencyStats s, InterfaceInfo nic, double dnsMs)
    {
        var list = new List<Finding>();

        if (s.LossPercent >= 2)
        {
            list.Add(new Finding(Severity.Bad, $"丢包 {s.LossPercent:0.0}%",
                "持续丢包会让视频卡顿、通话断续。优先检查无线干扰或更换网线。", "排查链路"));
        }
        else if (s.LossPercent >= 0.5)
        {
            list.Add(new Finding(Severity.Warn, $"丢包 {s.LossPercent:0.0}%",
                "少量丢包已能被应用感知，长连接会偶发重传。", "排查链路"));
        }
        else
        {
            list.Add(new Finding(Severity.Ok, $"丢包 {s.LossPercent:0.0}%",
                $"{s.Sent} 个探针包全部返回，链路稳定。"));
        }

        if (s.JitterMs >= 20)
        {
            list.Add(new Finding(Severity.Warn, $"抖动 {s.JitterMs:0.0} ms 偏高",
                "相邻包延迟差过大，实时语音与游戏会明显抖动。", "查看建议"));
        }
        else if (s.JitterMs >= 8)
        {
            list.Add(new Finding(Severity.Info, $"抖动 {s.JitterMs:0.0} ms",
                "轻微抖动，会议通话尚可，竞技游戏建议有线连接。", "查看建议"));
        }

        if (nic.IsWireless && nic.SignalPercent is { } signal && signal < 62)
        {
            list.Add(new Finding(Severity.Warn, "无线信号偏弱",
                $"信号强度约 {signal}%，隔墙后速率会明显下降。靠近路由器或改用 6 GHz 可显著改善。", "优化信号"));
        }

        if (dnsMs > 0 && dnsMs >= 25)
        {
            list.Add(new Finding(Severity.Warn, $"DNS 解析 {dnsMs:0.0} ms",
                "解析偏慢会拖慢每个新连接的首包时间。", "优化 DNS"));
        }
        else if (dnsMs > 0)
        {
            list.Add(new Finding(Severity.Info, $"DNS 解析 {dnsMs:0.0} ms",
                "解析正常；启用本地缓存仍可再省 3–5 ms。", "优化 DNS"));
        }

        return list;
    }

    private static string SummaryOf(LatencyStats s, InterfaceInfo nic, double down)
    {
        var link = nic.IsWireless ? "无线链路" : "有线链路";
        if (s.LossPercent >= 2)
        {
            return $"{link}存在丢包，先检查物理层再谈优化。";
        }

        if (nic.IsWireless && nic.SignalPercent is { } signal && signal < 62)
        {
            return $"延迟与丢包表现良好，但无线信号只有 {signal}%，距离一远会先掉速。";
        }

        if (s.AverageMs <= 35 && s.JitterMs <= 6 && s.LossPercent <= 0.5)
        {
            return $"延迟低、抖动小、无丢包，适合游戏与 4K 会议；{link}余量充足。";
        }

        if (down >= 100)
        {
            return $"{link}带宽充裕，延迟与抖动决定了实际体感。";
        }

        return $"{link}可用，但在高并发场景下会出现排队延迟。";
    }

    private static int Pct(double value) => (int)Math.Round(Clamp(value, 0, 100), MidpointRounding.AwayFromZero);

    private static double Clamp(double value, double min, double max) => Math.Min(max, Math.Max(min, value));
}
