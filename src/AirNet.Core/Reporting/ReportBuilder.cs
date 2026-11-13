using System.Globalization;
using System.Net;
using System.Text;
using AirNet.Core.Diagnostics;
using AirNet.Core.History;
using AirNet.Core.Sites;

namespace AirNet.Core.Reporting;

/// <summary>
/// Builds a self-contained HTML report: one file, no external assets, printable to PDF from any
/// browser. The layout mirrors the app's paper palette so a report and the window look related.
/// </summary>
public static class ReportBuilder
{
    public const string FormatVersion = "1";

    public static string Build(
        NetworkSnapshot snapshot,
        Attribution? attribution = null,
        MtuResult? mtu = null,
        bool maskAddresses = true,
        SiteLatencyReport? siteLatency = null,
        HistoryTrendSummary? trends = null)
    {
        var html = new StringBuilder();
        var captured = snapshot.CapturedAt.ToLocalTime();
        var accent = snapshot.Grade switch
        {
            HealthGrade.Excellent => "#2E6F63",
            HealthGrade.Good => "#2E6F63",
            HealthGrade.Fair => "#A9701A",
            _ => "#B23A2B",
        };

        html.AppendLine("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">");
        html.AppendLine($"<title>AirNet 网络报告 · {captured:yyyy-MM-dd HH:mm}</title>");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.AppendLine($"<style>{Css.Replace("__ACCENT__", accent)}</style>");
        html.AppendLine("</head><body><main>");

        AppendHeader(html, captured);
        AppendHero(html, snapshot, maskAddresses);
        AppendInterference(html, snapshot);
        AppendMetrics(html, snapshot);
        AppendChart(html, snapshot);
        AppendFindings(html, snapshot);
        if (attribution is not null)
        {
            AppendPath(html, attribution, mtu, snapshot.DualStack);
        }

        AppendTrends(html, trends);
        AppendSiteLatency(html, siteLatency);
        AppendFooter(html, maskAddresses);
        html.AppendLine("</main></body></html>");
        return html.ToString();
    }

    /// <summary>
    /// A proxy or VPN changes what the numbers mean, so it belongs above the measurements rather
    /// than in a footnote.
    /// </summary>
    private static void AppendInterference(StringBuilder html, NetworkSnapshot snapshot)
    {
        if (!snapshot.Interference.IsInterfering)
        {
            return;
        }

        html.AppendLine("<section class=\"card\">");
        html.AppendLine(
            $"<p class=\"lead\"><strong>{Encode(snapshot.Interference.ChipText)}</strong> · "
            + $"{Encode(snapshot.Interference.Note)}</p>");
        html.AppendLine("</section>");
    }

    private static void AppendHeader(StringBuilder html, DateTimeOffset captured)
    {
        html.AppendLine("<header><div class=\"brand\"><span class=\"mark\"></span>AirNet</div>");
        html.AppendLine($"<div class=\"stamp\">{captured:yyyy 年 M 月 d 日 HH:mm:ss}</div></header>");
    }

    private static void AppendHero(StringBuilder html, NetworkSnapshot snapshot, bool mask)
    {
        html.AppendLine("<section class=\"hero\">");
        html.AppendLine("<div class=\"scorewrap\">");
        html.AppendLine($"<div class=\"score\">{snapshot.Score}</div>");
        html.AppendLine($"<div class=\"grade\">{Encode(snapshot.GradeLabel)}</div>");
        html.AppendLine("</div>");
        html.AppendLine("<div class=\"verdict\">");
        html.AppendLine($"<h1>{Encode(snapshot.Verdict)}</h1>");
        html.AppendLine($"<p>{Encode(snapshot.Summary)}</p>");
        html.AppendLine("<div class=\"chips\">");
        html.AppendLine($"<span class=\"chip\">{Encode(snapshot.Interface.Kind)}</span>");
        if (!string.IsNullOrEmpty(snapshot.Interface.Ssid))
        {
            html.AppendLine($"<span class=\"chip\">{Encode(snapshot.Interface.Ssid)}</span>");
        }

        if (snapshot.Interface.SignalPercent is { } signal)
        {
            var rssi = snapshot.Interface.RssiDbm is { } dbm ? $" · {dbm} dBm" : string.Empty;
            html.AppendLine($"<span class=\"chip\">信号 {signal}%{rssi}</span>");
        }

        html.AppendLine($"<span class=\"chip mono\">{Encode(Mask(snapshot.Interface.LocalAddress, mask))}</span>");
        if (!string.IsNullOrWhiteSpace(snapshot.EgressAddress))
        {
            html.AppendLine($"<span class=\"chip mono\">出口 {Encode(Mask(snapshot.EgressAddress, mask))}</span>");
        }
        html.AppendLine("</div></div></section>");
    }

    private static void AppendMetrics(StringBuilder html, NetworkSnapshot snapshot)
    {
        var s = snapshot.Latency;
        html.AppendLine("<section class=\"grid\">");
        AppendMetric(html, "延迟 / LATENCY", $"{s.AverageMs:0.0}", "ms", $"P95 {s.P95Ms:0.0} ms");
        AppendMetric(html, "抖动 / JITTER", $"{s.JitterMs:0.0}", "ms", "相邻包差均值");
        AppendMetric(html, "丢包 / LOSS", $"{s.LossPercent:0.0}", "%", $"{s.Received}/{s.Sent} 已返回");
        AppendMetric(html, "下载 / DOWNLOAD",
            snapshot.HasBandwidth ? $"{snapshot.DownloadMbps:0}" : "—",
            "Mbps",
            snapshot.HasUpload ? $"上行 {snapshot.UploadMbps:0} Mbps" : "未测速");
        AppendMetric(html, "DNS", snapshot.Timing.Succeeded ? $"{snapshot.Timing.DnsMs:0.0}" : "—", "ms",
            snapshot.Timing.Succeeded ? $"总首包 {snapshot.Timing.TotalMs:0} ms" : "未测");
        AppendMetric(html, "计时方式", snapshot.ProbeMode == ProbeMode.Tcp ? "TCP" : "ICMP", string.Empty,
            "延迟与抖动来源");
        html.AppendLine("</section>");
    }

    private static void AppendMetric(StringBuilder html, string label, string value, string unit, string foot)
    {
        html.AppendLine("<div class=\"metric\">");
        html.AppendLine($"<div class=\"label\">{Encode(label)}</div>");
        html.AppendLine($"<div class=\"value\">{Encode(value)}<span class=\"unit\">{Encode(unit)}</span></div>");
        html.AppendLine($"<div class=\"foot\">{Encode(foot)}</div>");
        html.AppendLine("</div>");
    }

    private static void AppendChart(StringBuilder html, NetworkSnapshot snapshot)
    {
        var series = snapshot.LatencySeries;
        if (series.Count < 2)
        {
            return;
        }

        html.AppendLine("<section class=\"card\"><div class=\"cardhead\">");
        html.AppendLine("<h2>延迟时间线</h2>");
        html.AppendLine($"<span class=\"muted\">平均 {snapshot.Latency.AverageMs:0.0} ms · P95 {snapshot.Latency.P95Ms:0.0} ms · {series.Count} 个样本</span>");
        html.AppendLine("</div>");
        html.AppendLine(BuildSvg(series, snapshot.Latency.AverageMs));
        html.AppendLine("</section>");
    }

    private static void AppendFindings(StringBuilder html, NetworkSnapshot snapshot)
    {
        html.AppendLine("<section class=\"card\"><div class=\"cardhead\"><h2>诊断结论</h2>");
        html.AppendLine($"<span class=\"muted\">{snapshot.Findings.Count} 项</span></div><ul class=\"findings\">");

        foreach (var finding in snapshot.Findings)
        {
            var tone = finding.Severity switch
            {
                Severity.Bad => "bad",
                Severity.Warn => "warn",
                Severity.Info => "info",
                _ => "ok",
            };

            html.AppendLine($"<li><span class=\"dot {tone}\"></span><div><strong>{Encode(finding.Title)}</strong>");
            html.AppendLine($"<p>{Encode(finding.Detail)}</p></div></li>");
        }

        html.AppendLine("</ul></section>");
    }

    private static void AppendPath(
        StringBuilder html,
        Attribution attribution,
        MtuResult? mtu,
        DualStackResult dualStack)
    {
        html.AppendLine("<section class=\"card\"><div class=\"cardhead\"><h2>链路归因</h2>");
        html.AppendLine($"<span class=\"muted\">{Encode(attribution.Headline)}</span></div>");
        html.AppendLine($"<p class=\"lead\">{Encode(attribution.Detail)}</p>");
        html.AppendLine("<div class=\"segments\">");

        foreach (var segment in attribution.Segments)
        {
            html.AppendLine("<div class=\"segment\">");
            html.AppendLine($"<div class=\"segname\">{Encode(segment.Name)}</div>");
            html.AppendLine($"<div class=\"segvalue\">{Encode(segment.LatencyText)}</div>");
            html.AppendLine($"<div class=\"foot\">{Encode(segment.DeltaText)}</div>");
            html.AppendLine($"<div class=\"foot\">{Encode(segment.Note)}</div>");
            html.AppendLine("</div>");
        }

        html.AppendLine("</div>");
        if (mtu is { Succeeded: true } mtuValue)
        {
            html.AppendLine($"<p class=\"lead\">MTU {mtuValue.MtuBytes} B · {Encode(mtuValue.Note)}</p>");
        }

        if (dualStack.Verdict != DualStackVerdict.Unavailable)
        {
            html.AppendLine(
                $"<p class=\"lead\">双栈 {Encode(dualStack.VerdictText)} · IPv4 {Encode(dualStack.V4Text)} / "
                + $"IPv6 {Encode(dualStack.V6Text)} · {Encode(dualStack.Note)}</p>");
        }

        html.AppendLine("</section>");
    }

    private static void AppendFooter(StringBuilder html, bool masked)
    {
        html.AppendLine("<footer>");
        html.AppendLine("<p>口径：延迟、P95 与抖动取自定时 TCP 握手；丢包取自 ICMP 回显。首包分解为单连接 DNS → TCP → TLS → 首字节。</p>");
        html.AppendLine(masked
            ? "<p>本报告已对局域网地址做掩码处理。所有测量在本机完成，未上传任何数据。</p>"
            : "<p>本报告包含完整地址。所有测量在本机完成，未上传任何数据。</p>");
        html.AppendLine($"<p class=\"muted\">AirNet {Encode(FormatVersion)} · 报告由本机生成</p>");
        html.AppendLine("</footer>");
    }

    private static string BuildSvg(IReadOnlyList<double> series, double average)
    {
        const double width = 660;
        const double height = 150;
        const double pad = 12;

        var min = series.Min();
        var max = series.Max();
        var span = Math.Max(1, max - min);
        var step = (width - (pad * 2)) / (series.Count - 1);

        double MapX(int i) => pad + (step * i);
        double MapY(double v) => pad + ((height - (pad * 2)) * (1 - ((v - min) / span)));

        var points = new StringBuilder();
        for (var i = 0; i < series.Count; i++)
        {
            points.Append(FormattableString.Invariant($"{MapX(i):0.#},{MapY(series[i]):0.#} "));
        }

        var area = FormattableString.Invariant(
            $"M {MapX(0):0.#},{height - pad} L {points.ToString().Trim().Replace(" ", " L ")} L {MapX(series.Count - 1):0.#},{height - pad} Z");
        var averageY = MapY(Math.Clamp(average, min, max));

        var svg = new StringBuilder();
        svg.AppendLine($"<svg viewBox=\"0 0 {width} {height}\" width=\"100%\" height=\"{height}\" role=\"img\" aria-label=\"延迟时间线\">");
        svg.AppendLine("<defs><linearGradient id=\"fill\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\">");
        svg.AppendLine("<stop offset=\"0%\" stop-color=\"var(--accent)\" stop-opacity=\"0.28\"/>");
        svg.AppendLine("<stop offset=\"100%\" stop-color=\"var(--accent)\" stop-opacity=\"0\"/>");
        svg.AppendLine("</linearGradient></defs>");
        svg.AppendLine($"<path d=\"{area}\" fill=\"url(#fill)\"/>");
        svg.AppendLine($"<polyline points=\"{points}\" fill=\"none\" stroke=\"var(--accent)\" stroke-width=\"2\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>");
        svg.AppendLine($"<line x1=\"{pad}\" y1=\"{averageY:0.#}\" x2=\"{width - pad}\" y2=\"{averageY:0.#}\" stroke=\"#9A9286\" stroke-width=\"1\" stroke-dasharray=\"4 4\"/>");
        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    /// <summary>Keeps private ranges out of a file that is meant to be shared.</summary>
    public static string Mask(string address, bool mask)
    {
        if (!mask || string.IsNullOrWhiteSpace(address) || address == "未获取")
        {
            return address;
        }

        if (!IPAddress.TryParse(address, out var parsed))
        {
            return address;
        }

        var octets = parsed.ToString().Split('.');
        if (octets.Length != 4)
        {
            return address;   // IPv6 stays as-is; masking it would break the usefulness
        }

        var a = int.Parse(octets[0], CultureInfo.InvariantCulture);
        var b = int.Parse(octets[1], CultureInfo.InvariantCulture);
        var isPrivate = a == 10
                        || (a == 192 && b == 168)
                        || (a == 172 && b is >= 16 and <= 31)
                        || a == 127;

        return isPrivate
            ? $"{octets[0]}.{octets[1]}.x.x"
            : $"{octets[0]}.{octets[1]}.{octets[2]}.x";
    }

    /// <summary>
    /// A report of one run is not enough to answer "is it always like this", so the last week of
    /// measurements rides along when the caller has history.
    /// </summary>
    private static void AppendTrends(StringBuilder html, HistoryTrendSummary? trends)
    {
        if (trends is null || !trends.HasData)
        {
            return;
        }

        html.AppendLine("<section class=\"card\"><div class=\"cardhead\">");
        html.AppendLine("<h2>近期趋势</h2>");
        html.AppendLine($"<span class=\"muted\">近 7 天 · {trends.Count} 次检测</span></div>");
        html.AppendLine("<div class=\"segments\">");

        void Cell(string name, string value, string foot)
        {
            html.AppendLine("<div class=\"segment\">");
            html.AppendLine($"<div class=\"segname\">{Encode(name)}</div>");
            html.AppendLine($"<div class=\"segvalue\">{Encode(value)}</div>");
            html.AppendLine($"<div class=\"foot\">{Encode(foot)}</div>");
            html.AppendLine("</div>");
        }

        Cell("平均评分", $"{trends.AverageScore:0.0}", "近 7 天");
        Cell("平均延迟", $"{trends.AverageLatencyMs:0.0} ms", "每次检测的平均");
        Cell("平均抖动", $"{trends.AverageJitterMs:0.0} ms", "相邻样本差");
        Cell("平均丢包", $"{trends.AverageLossPercent:0.0}%", "ICMP 探测");
        html.AppendLine("</div>");

        if (trends.WorstBucket is { HasData: true } worst)
        {
            html.AppendLine(
                $"<p class=\"lead\">最差时段 {Encode(worst.Label)}（{Encode(worst.RangeText)}）"
                + $"平均 {worst.AverageLatencyMs:0.0} ms，共 {worst.Count} 次。</p>");
        }

        html.AppendLine("</section>");
    }

    private static void AppendSiteLatency(StringBuilder html, SiteLatencyReport? report)
    {
        if (report is null || report.Results.Count == 0)
        {
            return;
        }

        html.AppendLine("<section class=\"card\"><div class=\"cardhead\">");
        html.AppendLine("<h2>网站延迟</h2><span class=\"muted\">首字节 · 3 次中位数</span></div>");
        html.AppendLine("<table class=\"sitetable\"><thead><tr><th>站点</th><th class=\"num\">总耗时</th><th>分段</th><th></th></tr></thead><tbody>");
        foreach (var result in report.Results)
        {
            var httpBlocked = result.FailureKind == SiteFailureKind.HttpBlocked;
            var httpServerError = result.FailureKind == SiteFailureKind.HttpServerError;
            var status = !result.Success || httpServerError || result.TotalMs > 200
                ? "bad"
                : httpBlocked || result.TotalMs > 80 ? "warn" : "ok";
            var total = result.Success ? $"{result.TotalMs:0} ms" : "—";
            var segments = result.Success && result.Median is { } median
                ? $"DNS {median.DnsMs:0} · TCP {median.TcpMs:0} · TLS {median.TlsMs:0} · 等待 {median.TtfbMs:0}"
                : result.FailureNote ?? "未完成";
            if ((httpBlocked || httpServerError) && !string.IsNullOrWhiteSpace(result.FailureNote))
            {
                segments = $"{segments} · {result.FailureNote}";
            }

            html.AppendLine($"<tr><td><strong>{Encode(result.Target.Name)}</strong><div class=\"muted\">{Encode(result.Target.Host)}</div></td>");
            html.AppendLine($"<td class=\"num mono\">{Encode(total)}</td><td class=\"segments-cell\">{Encode(segments)}</td>");
            html.AppendLine($"<td><span class=\"dot {status}\"></span></td></tr>");
        }

        html.AppendLine("</tbody></table></section>");
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private const string Css = """
        :root{--accent:__ACCENT__;--paper:#F1EEE7;--card:#FBFAF7;--ink:#1D232C;--muted:#69645C;--line:#DED7CB;--soft:#F6F2EB}
        *{box-sizing:border-box}
        body{margin:0;background:var(--paper);color:var(--ink);font:14px/1.55 -apple-system,"Segoe UI","Microsoft YaHei UI",sans-serif}
        main{max-width:720px;margin:0 auto;padding:40px 28px 56px}
        header{display:flex;justify-content:space-between;align-items:center;margin-bottom:26px}
        .brand{display:flex;align-items:center;gap:8px;font-weight:700;letter-spacing:-.2px}
        .mark{width:18px;height:18px;border-radius:5px;background:var(--accent);display:inline-block}
        .stamp{color:var(--muted);font-size:12px}
        .hero{display:flex;gap:24px;align-items:center;margin-bottom:22px}
        .scorewrap{flex:0 0 auto;text-align:center}
        .score{font-size:56px;font-weight:700;letter-spacing:-2px;line-height:1}
        .grade{font-size:11px;letter-spacing:1.2px;color:var(--muted);text-transform:uppercase;margin-top:4px}
        .verdict h1{margin:0 0 6px;font-size:22px;letter-spacing:-.3px}
        .verdict p{margin:0;color:var(--muted);font-size:13px}
        .chips{margin-top:10px;display:flex;flex-wrap:wrap;gap:6px}
        .chip{border:1px solid var(--line);background:var(--card);border-radius:20px;padding:4px 10px;font-size:11px}
        .mono{font-family:"Cascadia Mono",Consolas,"SF Mono",monospace}
        .grid{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin-bottom:18px}
        .metric{background:var(--soft);border:1px solid var(--line);border-radius:10px;padding:12px}
        .metric .label{font-size:9px;letter-spacing:1.1px;color:var(--muted);text-transform:uppercase}
        .metric .value{font-size:24px;font-weight:600;letter-spacing:-.6px;margin:2px 0}
        .metric .unit{font-size:11px;color:var(--muted);margin-left:3px;font-weight:600}
        .metric .foot,.foot{font-size:10px;color:#9A9286}
        .card{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:18px;margin-bottom:16px}
        .cardhead{display:flex;justify-content:space-between;align-items:baseline;margin-bottom:10px}
        .card h2{font-size:14px;margin:0;letter-spacing:-.1px}
        .muted{color:#9A9286;font-size:11px}
        .lead{margin:6px 0;color:var(--muted);font-size:12.5px}
        ul.findings{list-style:none;margin:0;padding:0}
        ul.findings li{display:flex;gap:10px;padding:10px 0;border-top:1px solid var(--line)}
        ul.findings li:first-child{border-top:none}
        ul.findings strong{font-size:12.5px}
        ul.findings p{margin:2px 0 0;color:var(--muted);font-size:11.5px}
        .dot{width:7px;height:7px;border-radius:50%;margin-top:6px;flex:0 0 auto}
        .dot.ok{background:#2E6F63}.dot.warn{background:#A9701A}.dot.bad{background:#B23A2B}.dot.info{background:#9A9286}
        .segments{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-top:10px}
        .segment{background:var(--soft);border:1px solid var(--line);border-radius:10px;padding:12px}
        .segname{font-size:11px;font-weight:600}
        .segvalue{font-size:20px;font-weight:600;letter-spacing:-.5px;margin:2px 0}
        .sitetable{width:100%;border-collapse:collapse;font-size:11px}
        .sitetable th{text-align:left;font-size:9px;letter-spacing:.7px;color:var(--muted);padding:0 0 7px;border-bottom:1px solid var(--line)}
        .sitetable td{padding:7px 0;border-bottom:1px solid var(--line);vertical-align:top}
        .sitetable tr:last-child td{border-bottom:none}
        .sitetable .num{text-align:right;white-space:nowrap}
        .sitetable .segments-cell{color:var(--muted);font-size:10px}
        .sitetable .dot{margin:4px 0 0 auto}
        footer{border-top:1px solid var(--line);padding-top:14px;margin-top:22px;color:var(--muted);font-size:10.5px;line-height:1.7}
        svg{display:block;margin-top:6px}
        @media print{body{background:#fff}.card,.metric,.segment{break-inside:avoid}}
        """;
}
