namespace AirNet.Core.Diagnostics;

/// <summary>
/// Turns raw measurements into a 0-100 health score, graded scenario estimates and a plain
/// language diagnosis. Deliberately rule based: every number the UI shows can be explained,
/// and the thresholds can be recalibrated against field data without a model server.
/// </summary>
public static class DiagnosisEngine
{
    private const int MaxFindings = 4;

    public static NetworkSnapshot Evaluate(
        MeasurementResult raw,
        double downloadMbps,
        double uploadMbps)
    {
        var latency = raw.Latency;
        var score = Score(latency, raw.Interface, raw.Timing);
        var grade = GradeOf(score);
        var scenarios = Scenarios(latency, downloadMbps, uploadMbps);
        var findings = Findings(latency, raw.Interface, raw.Timing, raw.Diagnostics);

        return new NetworkSnapshot(
            score,
            grade,
            GradeLabel(grade),
            VerdictOf(grade),
            SummaryOf(latency, raw.Interface, raw.Timing, downloadMbps),
            raw.Interface,
            raw.EgressAddress,
            latency,
            raw.ProbeMode,
            raw.Timing,
            raw.LatencySeries,
            raw.Hops,
            scenarios,
            findings,
            downloadMbps,
            uploadMbps,
            raw.CapturedAt,
            raw.Diagnostics);
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

    private static int Score(LatencyStats stats, InterfaceInfo nic, LayerTiming timing)
    {
        // Each penalty is capped so one bad dimension cannot zero the whole score; the
        // weights follow what users feel: loss > jitter > latency > radio > app layer.
        var latencyPenalty = Clamp((stats.AverageMs - 12) * 0.42, 0, 26);
        var jitterPenalty = Clamp((stats.JitterMs - 1.5) * 1.1, 0, 14);
        var lossPenalty = Clamp(stats.LossPercent * 9, 0, 38);
        var radioPenalty = nic.IsWireless && nic.SignalPercent is { } signal
            ? Clamp((70 - signal) * 0.12, 0, 12)
            : 0;
        var appPenalty = timing.Succeeded
            ? Clamp((timing.TtfbMs - 400) * 0.012, 0, 6) + Clamp((timing.TlsMs - 400) * 0.004, 0, 3)
            : 0;

        var raw = 100 - latencyPenalty - jitterPenalty - lossPenalty - radioPenalty - appPenalty;
        return (int)Math.Round(Clamp(raw, 0, 100), MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyList<ScenarioReading> Scenarios(LatencyStats s, double down, double up)
    {
        var latency = s.AverageMs;
        var jitter = s.JitterMs;
        var loss = s.LossPercent;

        // An unmeasured uplink must not be treated as a 0 Mbps link: that would turn a missing
        // number into a false meeting/streaming penalty.
        var upstreamPenalty = up > 0 && up < 8 ? (8 - up) * 2 : 0;

        return
        [
            new("网页浏览", Pct(100 - (latency * 0.22) - (jitter * 0.7) - (loss * 5)),
                "首屏加载与翻页"),
            new("4K 视频", Pct(100 - (loss * 8) - (latency > 80 ? (latency - 80) * 0.3 : 0)),
                "拖拽与缓冲速度"),
            new("在线游戏", Pct(96 - (latency * 0.5) - (jitter * 1.5) - (loss * 7)),
                "操作延迟与命中"),
            new("视频会议", Pct(100 - (jitter * 1.4) - (loss * 7) - upstreamPenalty),
                up > 0 ? "语音连续性与画质" : "上行未测，仅按延迟估算"),
            down > 0
                ? new("大文件下载", Pct(Math.Min(100, 52 + (down * 0.12))), "峰值吞吐能力")
                : new("大文件下载", 0, "未测速", IsMeasured: false),
        ];
    }

    private static IReadOnlyList<Finding> Findings(
        LatencyStats s,
        InterfaceInfo nic,
        LayerTiming timing,
        ProbeDiagnostics diagnostics)
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
        else if (s.JitterMs >= 6)
        {
            list.Add(new Finding(Severity.Info, $"抖动 {s.JitterMs:0.0} ms",
                "轻微抖动，会议通话尚可，竞技游戏建议有线连接。", "查看建议"));
        }

        if (nic.IsWireless && nic.SignalPercent is { } signal && signal < 62)
        {
            var rssi = nic.RssiDbm is { } dbm ? $"，约 {dbm} dBm" : string.Empty;
            list.Add(new Finding(Severity.Warn, "无线信号偏弱",
                $"信号强度 {signal}%{rssi}，隔墙后速率会明显下降。靠近路由器或改用 6 GHz 可显著改善。",
                "优化信号"));
        }

        if (!diagnostics.IcmpAnswered && diagnostics.TcpAnswered)
        {
            list.Add(new Finding(Severity.Info, "ICMP 被拦截",
                $"Echo 无应答，延迟改由 {diagnostics.TcpReceived} 次 TCP 握手计时，结果依然可信。"));
        }

        if (timing.Succeeded)
        {
            if (timing.DnsMs >= 25)
            {
                list.Add(new Finding(Severity.Warn, $"DNS 解析 {timing.DnsMs:0.0} ms",
                    "解析偏慢会拖慢每个新连接的首包时间，建议改用低延迟公共 DNS。", "优化 DNS"));
            }
            else
            {
                list.Add(new Finding(Severity.Ok, $"DNS 解析 {timing.DnsMs:0.0} ms",
                    $"首包共 {timing.TotalMs:0} ms：TCP {timing.TcpMs:0} · TLS {timing.TlsMs:0} · 等待 {timing.TtfbMs:0}。"));
            }

            if (timing.TtfbMs >= 600 || timing.TlsMs >= 400)
            {
                list.Add(new Finding(Severity.Warn, $"首包 {timing.TtfbMs:0} ms",
                    $"TLS 握手 {timing.TlsMs:0} ms、等待首字节 {timing.TtfbMs:0} ms，服务端或线路存在排队。",
                    "查看建议"));
            }
        }
        else if (!string.IsNullOrEmpty(timing.FailureNote))
        {
            list.Add(new Finding(Severity.Info, "首包分解不可用", timing.FailureNote));
        }

        return list
            .OrderBy(f => f.Severity switch
            {
                Severity.Bad => 0,
                Severity.Warn => 1,
                Severity.Info => 2,
                _ => 3,
            })
            .Take(MaxFindings)
            .ToList();
    }

    private static string SummaryOf(LatencyStats s, InterfaceInfo nic, LayerTiming timing, double down)
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

        if (timing.Succeeded && timing.DnsMs >= 25)
        {
            return $"链路本身正常，DNS 解析花了 {timing.DnsMs:0} ms，换一个解析器就能提速。";
        }

        if (s.AverageMs <= 35 && s.JitterMs <= 6 && s.LossPercent <= 0.5)
        {
            var bandwidthNote = down <= 0 ? " 带宽尚未测速。" : string.Empty;
            return $"延迟低、抖动小、无丢包，适合游戏与 4K 会议；{link}余量充足。{bandwidthNote}";
        }

        if (down <= 0)
        {
            return $"{link}延迟与稳定性正常；带宽尚未测速，大文件场景暂不评价。";
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
