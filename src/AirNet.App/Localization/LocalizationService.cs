using System.Globalization;
using Avalonia;
using Avalonia.Controls;

namespace AirNet.App.Localization;

/// <summary>
/// Runtime UI localization. Static XAML text uses <c>{DynamicResource L.Key}</c>; text built in
/// a ViewModel goes through <see cref="T"/> / <see cref="Format"/>. A language change replaces
/// one merged resource dictionary, so DynamicResource consumers update without rebuilding views.
/// </summary>
public sealed class LocalizationService
{
    public const string English = "en";
    public const string ChineseSimplified = "zh-CN";

    public static LocalizationService Current { get; } = new();

    private static readonly IReadOnlyDictionary<string, (string En, string Zh)> Catalog =
        new Dictionary<string, (string En, string Zh)>(StringComparer.Ordinal)
        {
            ["L.Workspace"] = ("WORKSPACE", "WORKSPACE"),
            ["L.Overview"] = ("Overview", "总览"),
            ["L.Monitor"] = ("Live monitor", "实时检测"),
            ["L.Speed"] = ("Speed test", "带宽测速"),
            ["L.Sites"] = ("Site latency", "网站延迟"),
            ["L.Path"] = ("Path", "链路路径"),
            ["L.History"] = ("History", "历史趋势"),
            ["L.Settings"] = ("Settings", "设置"),
            ["L.SettingsNote"] = ("Changes apply immediately and stay on this machine", "所有选项即时生效，配置保存在本机"),
            ["L.StartScan"] = ("Start scan", "开始检测"),
            ["L.Stop"] = ("Stop", "停止"),
            ["L.AutoRefresh"] = ("Auto refresh", "自动刷新"),
            ["L.ExportReport"] = ("Export report", "导出报告"),
            ["L.LocalLink"] = ("LOCAL LINK", "本机链路"),
            ["L.Lan"] = ("LAN", "局域网"),
            ["L.Gateway"] = ("Gateway", "网关"),
            ["L.Dns"] = ("DNS", "DNS"),
            ["L.Egress"] = ("Egress", "出口"),
            ["L.LightweightMode"] = ("Lightweight mode", "轻量模式"),
            ["L.LightweightNote"] = ("No daemon · no driver · online only while scanning", "无常驻 · 无驱动 · 仅在检测时联网"),
            ["L.Language"] = ("Language", "语言"),
            ["L.Chinese"] = ("中文", "中文"),
            ["L.English"] = ("English", "English"),
            ["L.Theme"] = ("Appearance", "外观"),
            ["L.FollowSystem"] = ("System", "跟随系统"),
            ["L.Light"] = ("Light", "浅色"),
            ["L.Dark"] = ("Dark", "深色"),
            ["L.About"] = ("About", "关于"),
            ["L.AppearanceNote"] = ("Warm paper palette; changes apply immediately.", "暖白纸面色板；修改即时生效。"),
            ["L.HeroTitle1"] = ("Is this network good,", "网络好不好，"),
            ["L.HeroTitle2"] = ("and is it fast enough?", "带宽够不够。"),
            ["L.HeroSubtitle"] = ("Measure latency, jitter, loss and bandwidth layer by layer, then name the likely culprit: router, ISP or far end.", "逐层测量延迟、抖动、丢包与带宽，用一句话说清问题出在路由器、运营商还是对端。"),
            ["L.ShareCard"] = ("Share card", "分享卡片"),
            ["L.NoDriverNote"] = ("No driver · no resident process · bandwidth is included", "无驱动 · 无常驻进程 · 检测含上下行测速"),
            ["L.RecentScans"] = ("RECENT SCANS", "最近检测"),
            ["L.OneScan"] = ("One scan, readable at a glance.", "一次检测，读懂网络。"),
            ["L.Latency"] = ("Latency", "延迟"),
            ["L.Loss"] = ("Loss", "丢包"),
            ["L.Bandwidth"] = ("Bandwidth", "带宽"),
            ["L.MonitorSubtitle"] = ("60 s window · 20 samples · hop attribution", "60 秒窗口 · 20 次采样 · 逐跳定位"),
            ["L.ScanAgain"] = ("Scan again", "重新检测"),
            ["L.SixtySecondsAgo"] = ("60 s ago", "60 秒前"),
            ["L.Diagnosis"] = ("Diagnosis", "诊断结论"),
            ["L.ByImpact"] = ("By impact", "按影响排序"),
            ["L.NextNote"] = ("Export a shareable report, or compare with yesterday at the same time.", "导出可分享的报告，或与昨天同一时段对比。"),
            ["L.KeyMetrics"] = ("KEY METRICS", "关键指标"),
            ["L.Scenarios"] = ("Scenarios", "场景体验"),
            ["L.Predicted"] = ("Predicted", "预测"),
            ["L.ScenarioNote"] = ("Estimated from latency, jitter, loss and bandwidth", "由延迟、抖动、丢包与带宽共同估算"),
            ["L.DeepModes"] = ("Deep tests", "模式深测"),
            ["L.OnDemand"] = ("On demand", "按需"),
            ["L.GameMeetingNote"] = ("Game: UDP/STUN. Meeting: upload, jitter and loss.", "游戏测 UDP/STUN；会议测上行、抖动与丢包。"),
            ["L.NatNote"] = ("NAT result is a STUN mapping heuristic, not a full NAT type.", "NAT 结论是 STUN 映射行为启发式，不等于完整 NAT 类型。"),
            ["L.HopByHop"] = ("Hop by hop", "逐跳"),
            ["L.FirstPacket"] = ("First-packet split", "首包分解"),
            ["L.SpeedSubtitle"] = ("Single-stream ranged GET · nearby server · 12 MB sample", "单连接 ranged GET · 就近服务器 · 12 MB 样本"),
            ["L.SpeedTest"] = ("SPEED TEST", "SPEED TEST"),
            ["L.Download"] = ("DOWNLOAD", "下载 / DOWNLOAD"),
            ["L.Upload"] = ("UPLOAD", "上传 / UPLOAD"),
            ["L.SpeedHistory"] = ("Speed history", "历史趋势"),
            ["L.LastSixDown"] = ("Last 6 · download", "近 6 次 · 下行"),
            ["L.NoSpeedHistory"] = ("No speed history yet. A real scan will record the next result here.", "还没有可对比的测速记录；完成一次真实检测后测速会记录在这里。"),
            ["L.ThisRun"] = ("THIS RUN", "本次结果"),
            ["L.CompletedAt"] = ("Completed", "完成于"),
            ["L.SpeedNotes"] = ("Speed test notes", "测速说明"),
            ["L.SingleStreamNote"] = ("Single-stream test; results are conservative. Parallel streams usually get closer to the line rate.", "单连接测试，结果偏保守；多流并发更接近家宽峰值。"),
            ["L.RandomBytesNote"] = ("Transfers random bytes only; no local information is uploaded.", "只传输随机字节，不上传任何本地信息。"),
            ["L.CongestedNote"] = ("If another app saturates the link, the result will underestimate the real bandwidth.", "链路已被其他应用占满时，测速会低估真实带宽。"),
            ["L.MakeItAccurate"] = ("Make it accurate", "让数字更准"),
            ["L.AccuracyTip"] = ("Pause downloads and video calls before testing; 5 GHz or Ethernet gets closer to the line rate.", "测速前暂停下载与视频通话，优先使用 5 GHz 或有线，结果更接近家宽上限。"),
            ["L.Jitter"] = ("Jitter", "抖动"),
            ["L.ManageSites"] = ("Manage sites", "管理站点"),
            ["L.Fastest"] = ("FASTEST", "最快"),
            ["L.Slowest"] = ("SLOWEST", "最慢"),
            ["L.Average"] = ("AVERAGE", "平均"),
            ["L.Done"] = ("DONE", "完成"),
            ["L.Wait"] = ("Wait", "等待"),
            ["L.SortByTotal"] = ("Sort by total time", "按总耗时排序"),
            ["L.Site"] = ("Site", "站点"),
            ["L.Total"] = ("Total", "总耗时"),
            ["L.Breakdown"] = ("Breakdown", "分段"),
            ["L.VsLast"] = ("vs last", "较上次"),
            ["L.NotInHealthScore"] = ("Not in the health score · measured locally", "不并入健康分 · 仅本机测量"),
            ["L.ThisVerdict"] = ("This verdict", "本次判断"),
            ["L.SeparateFromScore"] = ("Site latency is not part of the health score", "站点延迟不并入健康分"),
            ["L.LookSeparately"] = ("Read separately", "单独看"),
            ["L.Scope"] = ("Scope", "口径"),
            ["L.SiteMethod"] = ("Three independent HTTPS requests per site, median taken; the four segments add up to the total. Only first byte is read; no page body, no cookies.", "每站 3 次独立 HTTPS 请求取中位数；四段相加等于总耗时。只读首字节，不下载页面、不带 Cookie。"),
            ["L.SiteThresholds"] = ("≤80 good · 80–200 slow · >200 cross-border/error", "≤80 良好 · 80–200 偏慢 · >200 跨境/异常"),
            ["L.SiteList"] = ("Site list", "站点清单"),
            ["L.DefaultSites"] = ("Default sites", "默认站点"),
            ["L.CustomSites"] = ("Custom sites", "自定义站点"),
            ["L.DefaultFixedNote"] = ("Defaults are fixed; up to 8 custom sites.", "默认站点固定；自定义最多 8 个。"),
            ["L.Proxied"] = ("via proxy", "经代理"),
            ["L.SiteLatency"] = ("Site latency", "网站延迟"),
            ["L.StartTrace"] = ("Start trace", "开始追踪"),
            ["L.MaxHops"] = ("Up to 16 hops · DF/ICMP", "最多 16 跳 · DF/ICMP"),
            ["L.NoHops"] = ("No hop data yet", "还没有逐跳数据"),
            ["L.NoHopsNote"] = ("Click Start trace; the system traceroute runs once (about 8–20 s) and you can switch pages meanwhile.", "点击右上角「开始追踪」，会用系统 traceroute 跑一次（约 8–20 秒，期间可切换页面）。"),
            ["L.PathSummary"] = ("PATH SUMMARY", "路径摘要"),
            ["L.Target"] = ("Target", "目标"),
            ["L.Reached"] = ("Reached", "到达"),
            ["L.Spike"] = ("Spike hop", "突变跳"),
            ["L.Basis"] = ("Basis", "判定依据"),
            ["L.DualStack"] = ("Dual stack", "双栈"),
            ["L.HowToRead"] = ("How to read", "怎么读"),
            ["L.HowToReadNote"] = ("Grey hop = node does not answer TTL probes, which is normal; amber = a latency jump. Trace uses the system traceroute and needs no admin rights.", "灰色跳 = 节点不响应 TTL 探测，属正常；琥珀标记 = 延迟突变点。追踪使用系统 traceroute，无需管理员权限。"),
            ["L.PathTitle"] = ("Path", "链路路径"),
            ["L.HistorySubtitle"] = ("Connect every scan to see latency, score and time-of-day patterns.", "把每次检测连起来，看延迟、评分和时段规律。"),
            ["L.Days7"] = ("7 days", "7 天"),
            ["L.Days30"] = ("30 days", "30 天"),
            ["L.Refresh"] = ("Refresh", "刷新"),
            ["L.NoHistoryTitle"] = ("No history yet", "还没有历史记录"),
            ["L.NoHistoryNote"] = ("Complete a real scan and the trend starts here.", "完成一次真实检测后，趋势会从这次开始累积。"),
            ["L.AverageScore"] = ("Average score", "平均评分"),
            ["L.AverageLatency"] = ("Average latency", "平均延迟"),
            ["L.AverageJitter"] = ("Average jitter", "平均抖动"),
            ["L.AverageLoss"] = ("Average loss", "平均丢包"),
            ["L.PerScanAverage"] = ("Average of each scan", "每次检测的平均值"),
            ["L.JitterDefinition"] = ("Mean difference of adjacent samples", "相邻样本差均值"),
            ["L.FromIcmp"] = ("From ICMP probes", "来自 ICMP 探测"),
            ["L.LatencyTrend"] = ("Latency trend", "延迟趋势"),
            ["L.TimeLeftToRight"] = ("Scan time, left to right", "按检测时间从左到右"),
            ["L.EachScanLatency"] = ("Average latency per scan", "每次检测的平均延迟"),
            ["L.TimeOfDay"] = ("Time of day", "时段观察"),
            ["L.LocalTime"] = ("Local time", "本地时间"),
            ["L.ScoreTrend"] = ("Score trend", "评分趋势"),
            ["L.RecentRecords"] = ("Recent records", "最近记录"),
            ["L.Last8"] = ("Last 8", "最近 8 次"),
            ["L.ABCompare"] = ("A/B comparison", "两次检测对比"),
            ["L.PickAB"] = ("Pick A / B to see the full difference", "选 A / B 看完整差异"),
            ["L.Compare"] = ("Compare", "对比"),
            ["L.CompareEmpty"] = ("Complete at least two scans to compare two records.", "至少完成两次检测后，可选两条记录做 A/B 对比。"),
            ["L.Metric"] = ("Metric", "指标"),
            ["L.Change"] = ("Change", "变化"),
            ["L.Testing"] = ("Scanning", "检测"),
            ["L.AutoRefreshTitle"] = ("Periodic auto refresh", "定时自动刷新"),
            ["L.AutoRefreshNote"] = ("Adds one sample every 60 s; pauses when the window is minimized", "每 60 秒补一次采样，窗口最小化时暂停"),
            ["L.DualProbeTitle"] = ("ICMP + TCP dual probe", "ICMP + TCP 双探测"),
            ["L.DualProbeNote"] = ("Falls back to TCP timing when ICMP is filtered, avoiding false loss", "ICMP 被拦截时自动改用 TCP 时间，避免误判丢包"),
            ["L.KeepHistoryTitle"] = ("Keep history", "保留历史"),
            ["L.KeepHistoryNote"] = ("Keep 30 days locally for comparison and trends", "本地保存最近 30 天，用于对比与趋势"),
            ["L.WifiNeighbors"] = ("Wi-Fi neighbors", "Wi-Fi 邻区"),
            ["L.ScanNeighbors"] = ("Scan neighbors", "扫描邻区"),
            ["L.NeighborReadOnlyNote"] = ("Read-only scan; does not touch the router. Windows WLAN API does not expose channels, so the page never invents channel advice.", "只读扫描，不修改路由器；Windows WLAN API 不提供信道，页面不编造信道建议。"),
            ["L.Shortcuts"] = ("Shortcuts", "快捷键"),
            ["L.SwitchPage"] = ("Switch page", "切换页面"),
            ["L.ToggleTheme"] = ("Toggle theme", "切换主题"),
            ["L.StopScan"] = ("Stop scan", "停止检测"),
            ["L.DataPrivacy"] = ("Data & privacy", "数据与隐私"),
            ["L.ClearHistory"] = ("Clear history", "清空历史"),
            ["L.ExportDiagnostics"] = ("Export diagnostics", "导出诊断包"),
            ["L.Runtime"] = ("Runtime", "运行环境"),
            ["L.Footprint"] = ("Footprint", "占用"),
            ["L.DnsOptimize"] = ("DNS optimization", "DNS 优化"),
            ["L.CopyCommand"] = ("Copy command", "复制命令"),
            ["L.PreviewNoExec"] = ("Preview (no changes)", "预演（不执行）"),
            ["L.ApplyAdmin"] = ("Apply (admin)", "应用（需管理员）"),
            ["L.Restore"] = ("Restore", "还原"),
            ["L.Project"] = ("Project", "项目"),
            ["L.License"] = ("License", "许可"),
            ["L.TechStack"] = ("Stack", "技术栈"),
            ["L.Feedback"] = ("Feedback", "反馈"),
            ["L.MetricLatency"] = ("LATENCY", "延迟 / LATENCY"),
            ["L.MetricJitter"] = ("JITTER", "抖动 / JITTER"),
            ["L.MetricLoss"] = ("LOSS", "丢包 / LOSS"),
            ["L.MetricDownload"] = ("DOWNLOAD", "下载 / DOWNLOAD"),
            ["L.WaitingMeasurement"] = ("Waiting for measurement", "等待测量"),
            ["L.NotTested"] = ("Not tested", "未测速"),
            ["L.AdjacentMean"] = ("Mean adjacent difference", "相邻包差均值"),
            ["L.Returned"] = ("returned", "已返回"),
            ["L.Uplink"] = ("Upload", "上行"),
            ["L.NoUpload"] = ("Upload not measured", "未测上行"),
            ["L.Excellent"] = ("Excellent", "优秀"),
            ["L.Good"] = ("Good", "良好"),
            ["L.Fair"] = ("Fair", "一般"),
            ["L.Poor"] = ("Poor", "较差"),
            ["L.VerdictExcellent"] = ("Network is excellent", "网络状态优秀"),
            ["L.VerdictGood"] = ("Network is good", "网络状态良好"),
            ["L.VerdictFair"] = ("Network is fair", "网络状态一般"),
            ["L.VerdictPoor"] = ("Network is poor", "网络状态较差"),
            ["L.WaitingForScan"] = ("Waiting for scan", "等待检测"),
            ["L.SampleRun"] = ("Sample data", "示例数据"),
            ["L.LocalMeasurement"] = ("Local measurement", "本机实测"),
            ["L.NotScanned"] = ("Not scanned yet", "尚未检测"),
            ["L.LastNDays"] = ("Last {0} days", "近 {0} 天"),
            ["L.RunsCount"] = ("{0} scans", "{0} 次检测"),
            ["L.BucketRuns"] = ("{0} runs", "{0} 次"),
            ["L.LateNight"] = ("Late night", "凌晨"),
            ["L.Morning"] = ("Morning", "上午"),
            ["L.Afternoon"] = ("Afternoon", "下午"),
            ["L.Evening"] = ("Evening", "晚间"),
            ["L.InsightWorst"] = ("{0} ({1}) averages {2} ms, above {3} ms overall; congestion is more likely in this window.", "{0}（{1}）平均 {2} ms，高于整体 {3} ms；网络拥堵更可能出现在这个时段。"),
            ["L.InsightFlat"] = ("No clear time-of-day difference yet; more samples make evening congestion and occasional loss easier to see.", "当前时段差异不明显；样本越多，晚间拥堵和偶发丢包越容易看出来。"),
            ["L.CompareSummary"] = ("{0}  →  {1}; ↑ better · ↓ worse", "{0}  →  {1}；↑ 变好 · ↓ 变差"),
            ["L.Score"] = ("Score", "评分"),
            ["L.NotMeasured"] = ("Not measured", "未测"),
            ["L.Flat"] = ("No change", "持平"),
            ["L.Today"] = ("Today", "今天"),
            ["L.YesterdayEvening"] = ("Yesterday evening", "昨天晚上"),
            ["L.Nov9"] = ("Nov 9", "11 月 9 日"),
            ["L.ThisScan"] = ("This scan", "本次检测"),
            ["L.VsLast"] = ("vs last", "较上次"),
            ["L.Example"] = ("Example", "示例"),
            ["L.ShareTitle"] = ("This network,\nwhat does it score?", "这一网，\n能打几分？"),
            ["L.FivePredictions"] = ("5 predictions · computed from this network", "5 项预测 · 按当前网络计算"),
            ["L.AverageRtt"] = ("average round trip", "平均往返"),
            ["L.AdjacentSample"] = ("adjacent-sample difference", "相邻样本差"),
            ["L.IcmpProbe"] = ("ICMP probes", "ICMP 探测"),
            ["L.NoDriverShort"] = ("No driver · no resident process", "无驱动 · 无常驻进程"),
            ["L.ShareWindowNote"] = ("Generate a PNG for a group chat or social post", "生成一张适合发群或社交平台的 PNG"),
            ["L.UploadFormat"] = ("Upload {0}", "上行 {0}"),
            ["L.WebBrowsing"] = ("Web browsing", "网页浏览"),
            ["L.Video4K"] = ("4K video", "4K 视频"),
            ["L.OnlineGaming"] = ("Online gaming", "在线游戏"),
            ["L.VideoMeeting"] = ("Video meetings", "视频会议"),
            ["L.LargeDownload"] = ("Large downloads", "大文件下载"),
            ["L.SamplesFormat"] = ("{0} samples", "{0} 次采样"),
            ["L.PeakFormat"] = ("Peak {0} ms", "峰值 {0} ms"),
            ["L.Wireless"] = ("wireless", "无线"),
            ["L.Wired"] = ("wired", "有线"),
            ["L.Signal"] = ("signal", "信号"),
            ["L.NotDetected"] = ("Not detected", "未检测"),
            ["L.NotAcquired"] = ("Not acquired", "未获取"),
            ["L.EgressFormat"] = ("Egress {0}", "出口 {0}"),
            ["L.SummaryFormat"] = ("Latency {0} ms, jitter {1} ms, loss {2}% on {3}.", "延迟 {0} ms，抖动 {1} ms，丢包 {2}%，{3}。"),
            ["L.WifiSignalFormat"] = ("Wi-Fi signal {0}%", "Wi-Fi 信号 {0}%"),
            ["L.SitesManagerNote"] = ("11 defaults · custom sites stay local", "默认 11 个 · 自定义站点只保存在本机"),
            ["L.Domestic7"] = ("Domestic 7", "国内 7"),
            ["L.International4"] = ("International 4", "国际 4"),
            ["L.Fixed"] = ("Fixed", "固定"),
            ["L.Edit"] = ("Edit", "编辑"),
            ["L.NoCustomSites"] = ("No custom sites yet. Add one on the right; defaults stay fixed.", "还没有自定义站点。右侧添加后，会出现在「自定义」分组；默认站点保持固定。"),
            ["L.Name"] = ("Name", "名称"),
            ["L.NameExample"] = ("e.g. Company status page", "例如：公司官网"),
            ["L.Address"] = ("Address", "地址"),
            ["L.Group"] = ("Group", "分组"),
            ["L.CustomFixed"] = ("Custom (fixed)", "自定义（固定）"),
            ["L.HttpsOnly"] = ("HTTPS only (443); a DNS / TCP / TLS / first-byte test runs before saving.", "只支持 HTTPS（443）；保存前会跑一次 DNS / TCP / TLS / 首字节测试。"),
            ["L.TestConnectivity"] = ("Test", "测试连通"),
            ["L.SaveSite"] = ("Save site", "保存站点"),
            ["L.SiteRules"] = ("Up to 8; duplicate domains are rejected; remove from the list.", "最多 8 个；不允许重复域名；可在列表里删除。"),
            ["L.SitesManagerFooter"] = ("Defaults are fixed; custom sites stay on this machine.", "默认站点固定；自定义站点只保存在本机。"),
            ["L.Done"] = ("Done", "完成"),
            ["L.Reading"] = ("Reading…", "读取中…"),
            ["L.NoRecords"] = ("No records", "暂无记录"),
            ["L.RecordsFormat"] = ("{0} records · 30 days", "{0} 条记录 · 保留 30 天"),
            ["L.NeighborHint"] = ("Scan once to see nearby hotspots", "扫描一次查看附近热点"),
            ["L.NeighborAdviceDefault"] = ("Neighbor scan is read-only; Windows WLAN API does not expose channels, so AirNet never invents channel advice.", "邻区扫描只读；Windows WLAN API 不提供信道，AirNet 不会编造信道建议。"),
            ["L.WiredNoScan"] = ("Wired connection; neighbor scan is not needed", "当前为有线连接，无需邻区扫描"),
            ["L.WiredAdvice"] = ("You are on Ethernet. Connect or enable a wireless adapter to scan nearby Wi-Fi.", "当前使用有线网络。接入或启用无线网卡后，才能扫描周围 Wi-Fi 热点。"),
            ["L.ScanningWlan"] = ("Scanning WLAN neighbors…", "正在扫描 WLAN 邻区…"),
            ["L.NoNeighborSupport"] = ("Neighbor scan is not supported on this platform", "当前平台不支持邻区扫描"),
            ["L.FoundNeighbors"] = ("{0} neighbors found", "发现 {0} 个邻区"),
            ["L.NoVisibleNeighbors"] = ("No visible neighbors", "未发现可见邻区"),
            ["L.ScanCancelled"] = ("Scan cancelled", "扫描已取消"),
            ["L.ScanFailed"] = ("Scan failed: {0}", "扫描失败：{0}"),
            ["L.NeighborUnsupportedAdvice"] = ("Neighbor scan is only available through Windows WLAN API; other platforms say so instead of inventing hotspots.", "邻区扫描仅在 Windows 的 WLAN API 下可用；其他平台会明确降级，不伪造热点列表。"),
            ["L.NoNeighborAdvice"] = ("No visible neighbors. On Wi-Fi, check the WLAN service, airplane mode and whether 2.4/5/6 GHz hotspots are nearby.", "没有发现可见邻区。若本机是无线连接，检查 WLAN 服务、飞行模式和附近是否有 2.4/5/6 GHz 热点。"),
            ["L.StrongestNeighbor"] = ("Strongest neighbor: {0} ({1}% · {2}).", "最强邻区：{0}（{1}% · {2}）。"),
            ["L.SameSsid"] = (" {0} hotspots share this SSID; prefer the stronger AP.", " 同一 SSID 有 {0} 个热点，优先靠近信号更强的 AP。"),
            ["L.WeakSignalAdvice"] = (" Signal is weak; move closer or use 5/6 GHz.", " 信号偏低，靠近路由器或改用 5GHz/6GHz 会更稳。"),
            ["L.GoodSignalAdvice"] = (" Signal is usable; the scan is read-only and never changes the router.", " 信号可用；邻区只读，不会修改路由器配置。"),
            ["L.PackageFormat"] = ("Store package {0} · source {1}", "商店包 {0} · 源码 {1}"),
            ["L.FootprintText"] = ("R2R single file · no driver · no resident process", "R2R 单文件 · 无驱动 · 无后台常驻"),
            ["L.ExperimentalMac"] = ("macOS is experimental: unsigned, not notarized, not verified on real hardware.", "macOS 为实验性支持：未签名、未公证，也未在真机验证。"),
            ["L.ExperimentalLinuxArm"] = ("Linux arm64 is experimental: CI builds pass, real hardware not verified.", "Linux arm64 为实验性支持：CI 构建通过，未在真机验证。"),
            ["L.PrivacyText"] = ("Measurements run locally; the egress lookup contacts 3322 / Cloudflare and uploads no local fields. History stays on this machine.", "测量在本机完成；出口 IP 查询会访问 3322 / Cloudflare，不上传本地字段。历史记录仅保存在本地。"),
            ["L.CurrentDns"] = ("Current {0}", "当前 {0}"),
            ["L.CurrentDnsLabel"] = ("Current DNS", "当前 DNS"),
            ["L.FastestSuffix"] = (" · fastest", " · 最快"),
            ["L.ClearHistoryDone"] = ("Local history cleared", "已清空本机历史"),
            ["L.DiagnosticsTitle"] = ("Export AirNet diagnostics", "导出 AirNet 诊断包"),
            ["L.DiagnosticsFilter"] = ("Diagnostics zip", "诊断包"),
            ["L.DiagnosticsWritten"] = ("Wrote {0} ({1} data files)", "已写出 {0}，含 {1} 个数据文件"),
            ["L.DiagnosticsNoData"] = ("Wrote {0} (no local data yet)", "已写出 {0}（暂无本地数据）"),
            ["L.DiagnosticsFailed"] = ("Write failed: {0}", "写出失败：{0}"),
            ["L.DnsAlreadyFast"] = ("DNS is already fast; no change needed", "当前解析已经够快，无需修改"),
            ["L.WaitingAdmin"] = ("Waiting for administrator confirmation…", "正在等待管理员确认…"),
            ["L.DnsApplied"] = ("Switched to {0}; use Restore to undo", "已切换到 {0}，可用「还原」恢复"),
            ["L.DnsPreview"] = ("Preview (not executed): {0}", "预演（未执行）：{0}"),
            ["L.NoBackup"] = ("No backup to restore", "没有可还原的记录"),
            ["L.DnsRestored"] = ("Restored the original DNS settings", "已还原为原有 DNS 设置"),
            ["L.DnsOperation"] = ("{0} {1} · {2} · {3}", "{0} {1} · {2} · {3}"),
            ["L.DnsBackup"] = ("{0} changed {1}; original DNS {2}", "{0} 修改过 {1}，原 DNS {2}"),
            ["L.LastScanFormat"] = ("Last scan {0}", "上次检测 {0}"),
            ["L.FooterLatency"] = ("Latency {0} ms", "延迟 {0} ms"),
            ["L.FooterLoss"] = ("Loss {0}%", "丢包 {0}%"),
            ["L.CollectedAtFormat"] = ("Collected {0}", "采集于 {0}"),
            ["L.DemoHint"] = ("Sample data · click Start scan for a real measurement", "示例数据 · 点击「开始检测」运行真实测量"),
            ["L.LocalHint"] = ("Waiting for scan · click Start scan to measure", "等待检测 · 点击「开始检测」开始测量"),
        };

    private ResourceDictionary? _dictionary;

    public string Language { get; private set; } = ChineseSimplified;

    public bool IsChinese => string.Equals(Language, ChineseSimplified, StringComparison.OrdinalIgnoreCase);

    public bool IsEnglish => !IsChinese;

    public string LanguageShortLabel => IsChinese ? "中" : "EN";

    public CultureInfo Culture => IsChinese
        ? CultureInfo.GetCultureInfo("zh-CN")
        : CultureInfo.GetCultureInfo("en-US");

    public event EventHandler? LanguageChanged;

    public void Apply(string? language)
    {
        var normalized = string.Equals(language, ChineseSimplified, StringComparison.OrdinalIgnoreCase)
                         || (language?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ?? false)
            ? ChineseSimplified
            : English;

        var changed = !string.Equals(Language, normalized, StringComparison.Ordinal) || _dictionary is null;
        Language = normalized;

        if (Application.Current is { } application)
        {
            var dictionary = new ResourceDictionary();
            foreach (var pair in Catalog)
            {
                dictionary[pair.Key] = IsChinese ? pair.Value.Zh : pair.Value.En;
            }

            if (_dictionary is not null)
            {
                application.Resources.MergedDictionaries.Remove(_dictionary);
            }

            application.Resources.MergedDictionaries.Add(dictionary);
            _dictionary = dictionary;
        }

        if (changed)
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string T(string key) =>
        Catalog.TryGetValue(key, out var value)
            ? IsChinese ? value.Zh : value.En
            : key;

    public string Format(string key, params object[] args) =>
        string.Format(Culture, T(key), args);

    public void Dispose()
    {
        if (_dictionary is not null && Application.Current is { } application)
        {
            application.Resources.MergedDictionaries.Remove(_dictionary);
        }

        _dictionary = null;
    }
}
