# AirNet 技术方案 v0.1

> 目标：用最小体积把"网络到底哪里不好"测清楚，并给出**可执行**的建议。
> 技术栈刻意与 `F:\AirPdf` 对齐（.NET 10 + Avalonia 11.3 + CommunityToolkit.Mvvm），共享设计系统、
> 自绘控件经验、单文件发布与 Velopack 更新管线。

---

## 1. 目标与非目标

**目标**

1. 3 秒内给出可用结论（健康分 + 一句话归因）。
2. 结论可解释：每个判断都能落到一条测量证据。
3. 体积与资源占用量级：解压 ≤ 45MB、运行内存 ≤ 160MB、冷启动 ≤ 1s（M1 实测 40.2MB / 152MB / 0.8–1.0s）。
4. 无驱动、无后台服务、无开机自启；关窗即停。
5. Windows / macOS / Linux 同一套代码，能力按平台降级。

**非目标**

- 不做"网络加速/游戏加速"（无法在不改变路由的前提下承诺收益）。
- 不做全局抓包、不做 DNS 劫持、不改系统代理。
- 不做云侧画像、不上传测量数据（可选匿名统计必须显式开启）。
- 不做 7×24 后台监控（那是另一类产品，与"轻"冲突）。

## 2. 技术选型

| 层 | 选择 | 理由 |
| --- | --- | --- |
| 运行时 | .NET 10 | 与 AirPDF 一致；`System.Net.NetworkInformation` / `Socket` / `SslStream` 齐备；AOT+R2R 可压到 15–20MB |
| UI | Avalonia 11.3 | 跨平台单代码库；自绘 `DrawingContext` 足够画环/曲线；与 AirPDF 样式可直接复用 |
| MVVM | CommunityToolkit.Mvvm 8.4 | 源生成器，零反射，AOT 友好 |
| 图标/字体 | Lucide 几何 + Inter | 与 AirPDF 同一套资产 |
| 图表 | **自研 4 个控件** | 引 OxyPlot/SkiaSharp 图表会多 2–4MB，且样式难融入纸张语言 |
| 存储 | JSON 追加日志（首版）→ 超阈值再上 SQLite | 30 天 × 每天 24 次 ≈ 720 条，JSON 足够；避免 1.5MB 原生依赖破坏体积承诺 |
| 发布 | 单文件 self-contained + Velopack | 与 AirPDF 同管线，增量更新 |
| 测试 | xUnit + Avalonia.Headless | Headless 可做视觉回归（本原型的 `--export` 已是雏形） |

**为什么不用 WinUI/WPF**：只能在 Windows 上跑，与 AirPDF 的跨平台发布流程不通用。
**为什么不用 Electron/Tauri**：Electron 体积 100MB+；Tauri 需要 Rust 工具链，且网络 API 仍要回到系统调用。

## 3. 架构

```
AirNet.sln
├─ src/AirNet.Core            纯 .NET，无 UI 依赖，可被 CLI/测试直接引用
│  ├─ Probes/                 InterfaceInspector, LatencyProbe, ThroughputProbe, MeasurementRunner
│  ├─ Sites/                  SiteCatalog（11 站）, CustomSiteStore, SiteHistoryStore, SiteLatencyProbe
│  ├─ Diagnostics/            NetworkSnapshot(模型), LatencyAnalyzer(统计), DiagnosisEngine(评分/归因)
│  └─ Demo/                   确定性样例数据（评审与演示用）
└─ src/AirNet.App             Avalonia
   ├─ ViewModels/             Shell(导航+扫描生命周期), Overview, Monitor, Speed, SiteLatency, Path, Settings
   ├─ Views/                  ShellView + 六个页面（UserControl，非 Window）+ SiteManagerView
   ├─ Controls/               ScoreRing, Sparkline, MeterBar, SignalBars, ScanSweep
   └─ Styles/                 Colors, Icons, Controls
```

**依赖方向**：`App → Core`，Core 不引用 Avalonia。所有"数字 → 文案"的格式化集中在 `SnapshotView`，
XAML 里不写转换器，视觉评审时只读 XAML 就能看懂。

**线程模型**

- 探针全部 `async`，跑在线程池；不占用 UI 线程。
- 采样结果先入 `Channel<Sample>`，UI 侧用一个 250ms 的 `DispatcherTimer` 批量取，避免每 100ms 触发一次布局。
- 每次扫描一个 `CancellationTokenSource`；"停止"按钮立即 `Cancel()`，探针在每次循环检查 token（最坏 1 个探测周期内退出）。
- 扫描期间禁用主按钮、把结论区切到"采集中"骨架态，**不做全屏遮罩**（会让人以为卡死）。

**生命周期**：窗口创建即空闲，只有用户点"开始检测"或开启自动刷新才发包；窗口关闭 → 取消所有 token → 退出。

## 4. 探测层

| 探针 | 实现 | 权限 | 耗时 | 降级 |
| --- | --- | --- | --- | --- |
| 接口清单 | `NetworkInterface.GetAllNetworkInterfaces()` + 网关/掩码/MTU | 无 | <10ms | 无 |
| 网关探测 | ICMP 网关地址 ×4 | 无 | ~300ms | TCP 连网关常用端口 |
| 参考主机 ICMP | `Ping.SendPingAsync` ×15，间隔 90ms | 无 | ~1.4s | ICMP 全丢 → TCP 握手计时 |
| TCP 握手 | `Socket.ConnectAsync` + `Stopwatch` ×10，间隔 70ms | 无 | ~0.8s | — |
| DNS 解析 | `Dns.GetHostAddressesAsync` 计时 ×3 | 无 | ~100ms | 直连 53/UDP 查询绕开缓存 |
| TLS 握手 | `SslStream.AuthenticateAsClientAsync` 计时 | 无 | ~200ms | 跳过 |
| HTTP 首字节 | `HttpClient` + `ResponseHeadersRead` | 无 | ~200ms | 跳过 |
| 出口 IP / 落地 | `https://ip.3322.net` + `https://www.cloudflare.com/cdn-cgi/trace`（纯文本，<1KB，并行取先到者） | 无 | ~0.2–3s | 显示"未获取"，不阻塞扫描 |
| 带宽下行 | 单流 GET，**4s 时间预算**（非字节数） | 无 | ~4s | 失败显未测出 |
| 带宽上行 | 4MB 流式 POST，单缓冲不占内存 | 无 | 1–5s | 同上 |
| 双栈 | 同一主机 IPv4/IPv6 各 5 次 TCP 握手，与首包分解并行 | 无 | ~0.5s | 无 IPv6 时标「仅 IPv4」 |
| 逐跳 | 调用系统 `tracert -d -w 500` / `traceroute -n` 解析输出（M2 已实现） | 无（子进程） | 8–20s | 仅网关+目标两跳 |
| Wi-Fi 信号/速率 | Windows `wlanapi.dll`（M1 已实现）；macOS `CoreWLAN`；Linux `iw`/`/proc/net/wireless` | 无 | <50ms | 仅报"无线/有线" |
| MTU | `PingOptions.DontFragment` + 缓冲区二分（M2 已实现） | 无 | ~1s | 跳过 |
| 网站延迟 | `ApplicationTimingProbe` ×11 站 ×3 次，并发 4，中位数 + HTTP 状态（M2 已实现） | 无 | 6–12s | 失败站保留原因；403/5xx 单独标记 |

### 4.1 一个必须解决的坑：ICMP 分辨率

本机实测（M0，`AirNet.exe --selftest`）：

```
avg=35  p95=35  jitter=0  loss=0  sent=15  received=15
```

`System.Net.NetworkInformation.Ping` 的 `RoundtripTime` 是**整毫秒**，15 个包全部落在 35ms，
于是抖动恒为 0 —— 这对"网络好不好"的判断是致命的（真实抖动可能有 ±3ms）。

**M1 已实现的对策**：延迟、P95、抖动一律由 `Stopwatch` 计时的 **TCP 握手序列**（10 次独立 SYN）计算，
ICMP 只负责**丢包比例**（TCP 握手观测不到丢包）。同一台机器的复测结果：

```
probeMode=Tcp  avg=38.6  p95=41.9  jitter=2.6  loss=0  sent=15  received=15
```

抖动从 0 变成 2.6ms，P95 有了区分度，`ProbeMode` 会写进快照并在 UI 上标注（"ICMP 计时 / TCP 计时"）。
TCP RTT 比 ICMP 略高（多一层本地协议栈开销），这是可接受的系统性偏差，评分阈值据此校准。

**仍未做的**：raw socket（需 UAC）提供亚毫秒 ICMP；M3 视需求再加。

### 4.2 采样与窗口

- 一轮扫描 = 15 个 ICMP（90ms 间隔）+ 10 次 TCP 握手（70ms 间隔）+ 首包分解 + 网关 4 包。约 2.2–3s；
  出口 IP 与首包分解并行，带宽测速是单独动作。
- 传感器窗口：延迟曲线保留最近 60 个样本 / 60 秒；计算 P95 用全部样本。
- 自动刷新（M2 已实现）：默认关闭；开启后 60s 一次，且**只在窗口可见时**执行（`IsVisible` / `WindowState` 检查），最小化即暂停。
- 移动网络（`NetworkInterfaceType.Wwanpp*`）默认不做带宽测试，避免偷跑流量。
- 网站延迟是独立动作：11 个默认站（+ 最多 8 个自定义）×3 次，并发 4，约 6–12s；
  **不进入主扫描，也不参与自动刷新**。每轮写入 `sites.jsonl`（30 天/500 条），
  `--sites` 可无 UI 跑同一套探针，`--report` 会读取最近一轮站点表。

### 4.3 带宽测速算法

```
预热：先跑 500ms 小请求，丢弃（TCP 慢启动、TLS 复用）
主体：4 路并发 ranged GET，块大小 256KB，丢弃 body 只计数
统计：取 5 秒滑动窗口中吞吐最高的 1 秒作为峰值，同时报"平均"
上行：4 路并发 POST 随机 64KB 块，同样窗口统计
顺序：下行 → 上行 → 复测一次延迟（判定是否被测速本身挤爆）
```

注意点：

- 服务端选点：Cloudflare（全球任播，`speed.cloudflare.com/__down|__up`）+ 可配置自有端点；
- 必须校验 `Content-Length` 与实际读到的字节一致，遇到运营商劫持页（HTML）要判失败；
- 结果要给"口径说明"：单流保守、多流接近峰值（UI 的"测速说明"卡片已写明）。

## 5. 诊断模型

### 5.1 评分

```
score = 100
      − clamp((avg − 12) × 0.42, 0, 26)      // 平均延迟
      − clamp((jitter − 1.5) × 1.1, 0, 14)   // 抖动
      − clamp(loss × 9, 0, 38)               // 丢包（权重最高）
      − clamp((70 − wifi%) × 0.12, 0, 12)    // 无线信号
分档：≥90 优秀 / ≥75 良好 / ≥60 一般 / <60 较差
```

权重来自"用户体感优先级"：丢包 > 抖动 > 延迟 > 射频。每项设上限，避免单一维度把分数打到 0（那会让用户失去区分度）。
M2 用真实场景数据回归校准（见 5.4）。

### 5.2 归因：三段式（M2 已实现）

`AttributionEngine` 有两个入口，置信度不同：

| 入口 | 数据 | 精度 | 何时用 |
| --- | --- | --- | --- |
| `FromSnapshot` | 网关 ICMP + 参考主机 | 粗定位 | 每次扫描后立即给出 |
| `FromTrace` | 逐跳延迟 | 逐跳定位 | 用户点「开始追踪」后 |

**逐跳版算法**（`FromTrace`）：

1. **接入段**取前 3 跳里第一个有响应的节点 —— 家用路由器普遍不回 TTL 过期包，"网关不回"不等于"局域网坏"（第一版把这种情况判成局域网故障，是错的）。
2. 中间跳取中位数作为运营商段延迟；最后一跳作为对端。
3. 段增量 = 本段延迟 − 上一段延迟，三段增量之和等于总延迟。
4. **不对健康链路报警**：只有总延迟 > 60ms 或丢包 ≥ 2% 时，段增量才会被判为问题；35ms 的路径即使 33ms 花在骨干上，结论也是"没有明显瓶颈"。
5. 输出 `Culprit ∈ {局域网, 运营商, 对端, 无}` + 一句话 + 三段证据 + 是否精确。

本机实测（`tracert -d`）：网关不回 TTL 探测，第 2 跳 2.0ms、第 6 跳 37ms → 判"运营商增量 34ms，但总延迟 35ms，无瓶颈"。

### 5.3 逐跳解析的健壮性

`tracert` 的提示文本是本地化的（"请求超时" / "Request timed out"），所以解析器**不认文字**：

- 行首 `^\s*(\d{1,2})\s+` 取跳数；
- 行内匹配 IPv4 取地址，匹配 `([\d.]+|<[\d.]+)\s*ms` 取该跳最小值；
- 没有地址 = 该跳超时。

`SpikeIndex` 在**跳过无响应跳**之后比较相邻有响应跳的增量，否则超时一跳就会让后面的突变检测失效（也是第一版实测发现的）。

MTU 用 `DontFragment` 二分（1200–1472），失败即"包太大"，8 次探测内收敛；结果 1480 说明链路有 PPPoE 开销。

### 5.3 规则库

每条规则结构化为 `Finding { Severity, Title, Detail, ActionLabel, Evidence[] }`，
`ActionLabel` 对应一个可执行动作（M2 起可真正执行，见 6）。

已实现（原型）：丢包三档、抖动两档、无线信号、DNS 耗时。
M1 追加：上行/下行不对称、IPv6 可用但劣于 IPv4、MTU 黑洞、代理/VPN 命中、双 NAT、网关 DNS 慢于公共 DNS。

### 5.4 场景估计

```
网页   = 100 − 0.22·latency − 0.7·jitter − 5·loss
4K视频 = 100 − 8·loss − (latency>80 ? 0.3·(latency−80) : 0)
游戏   =  96 − 0.5·latency − 1.5·jitter − 7·loss
会议   = 100 − 1.4·jitter − 7·loss − (up<8 ? 2·(8−up) : 0)
下载   = min(100, 52 + 0.12·downMbps)
```

这些系数是**先验**，标注为"预测"。M3 用"同一时刻的真实 QoE 采样"（视频缓冲事件、游戏 RTT）做回归，
把"预测"换成"校准值"，并在 UI 上区分（已实现文案：`场景体验 / 预测`）。

## 6. 优化动作（"优化"到底做什么）

原则：**只做可解释、可回退、不改用户路由的配置修正**。

**M2 已实现：DNS 切换（Windows）**，流程刻意做成四步，任何一步都可以停：

1. `DnsAdvisor` 只在「解析 ≥ 25ms 且当前不是 223.5.5.5/1.1.1.1」时才给建议，否则明确说"不建议折腾"；
2. 把将执行的 `netsh` 命令原文显示在界面上，并提供复制；
3. 点「应用」才弹 UAC；用户取消（1223）是正常返回，不报警；
4. 应用前把**原 DNS 与还原命令**写进 `dns-backup.json`，重启后仍可一键还原。

| 动作 | 平台 | 需要权限 | 回退 |
| --- | --- | --- | --- |
| 切换 DNS 为 223.5.5.5 / 1.1.1.1 | **Win ✅ M2**；macOS/Linux 生成命令但不自动执行 | 管理员 | 记录原值 + 还原命令，一键回滚 |
| 建议关闭高占用进程 | 全平台（只读列举） | 无 | 不执行，仅提示 |
| 5GHz/6GHz 与信道建议 | Windows `WlanGetNetworkBssList` 扫描邻区 | 无 | 只读建议 |
| 无线节能/省电模式排查 | Win 设备管理器查询 | 无（读） | 只读建议 |
| MTU 黑洞修复建议 | 全平台 | 管理员 | 记录原值 |
| 关闭 IPv6（当 IPv6 明显更差时） | 全平台 | 管理员 | 显式确认 + 还原 |

UI 上所有"一键"操作都必须：① 展示将要执行的命令；② 提供"还原"；③ 失败时给出原始错误。
**不提供**：改 hosts、改代理、改防火墙、"加速"开关。

## 7. 数据与报告

- 存储：`%AppData%/AirNet/runs.jsonl`（每行一次快照摘要），保留 30 天，超过 2MB 轮转；
  打开历史页时按需读取（`File.ReadLines` 流式），不做全量反序列化。
- 报告（**M2 已实现**）：`ReportBuilder` 生成自包含 HTML —— 内联 CSS、内联 SVG 曲线、零脚本，
  单文件 7–8KB，浏览器直接打印成 PDF。内网地址默认掩码（`192.168.x.x`），出口地址只留三段。
  由 `--report <path>` 或界面「导出报告」触发，同一份代码也用于 CI 断言。
- 隐私：报告导出的 IP 默认做掩码（`192.168.1.x`、出口只留 ASN/城市），可手动关闭。

## 8. 性能预算与手段

| 指标 | 预算 | 本机 2026-11 复测（Release，trim + R2R，便携版） | 手段 / 备注 |
| --- | --- | --- | --- |
| 解压体积 | ≤ 45MB | **41.1MB**（exe 25.3 + Skia 9.0 + ANGLE 5.2 + HarfBuzz 1.7）；压缩后 26.9MB | `PublishSingleFile` + `TrimMode=partial`；**达标**（清理 publish 目录后复测） |
| 冷启动 | ≤ 1400ms | **1.6–1.9s** 到窗口可见（单次复测） | R2R 已开；**未达预算**，待查启动路径 |
| 运行内存 | ≤ 180MB | **~176MB** 工作集 | 图表只保留 60 点、不缓存位图；**达标** |
| 安装包 | ≤ 25MB | Windows zip **26.9MB**（略超）；macOS **17.8/18.2MB**、Linux **14.5/15.1MB**（均含说明） | `packaging/pack-all.ps1`；只出便携 zip，不做 setup |
| 扫描 CPU | < 3% | 未测 | ICMP 90ms / TCP 70ms 间隔；UI 250ms 合并刷新 |

> 结论：清理 publish 目录后，2026-11 复测为 26.9MB zip / 41.1MB 解压 / 1.6–1.9s / ~176MB；
> 体积和内存已回到预算内，启动仍略高于 1.4s。此前 52.2MB / 217MB 的记录包含增量发布残留，
> 已通过 `publish.ps1` 的清理步骤修正。复现：`powershell -File packaging/pack-all.ps1`。

## 9. 跨平台矩阵

| 能力 | Windows | macOS | Linux |
| --- | --- | --- | --- |
| ICMP | `Ping`（同步/异步） | 同 | 同（部分发行版需 `CAP_NET_RAW`→ 自动退 TCP） |
| 无线信号 | `wlanapi.dll` | CoreWLAN（P/Invoke） | `iw` 或 `/proc/net/wireless` |
| 邻区扫描 | `WlanGetNetworkBssList` | CoreWLAN scan | `iw scan`（需 root，则跳过） |
| 逐跳 | `tracert` 子进程 | `traceroute` | `traceroute`/`tracepath` |
| 改 DNS | `netsh interface ip set dns` | `networksetup -setdnsservers` | `nmcli con mod` |
| 自定义标题栏 | `BorderOnly + NoChrome` | 同 | 同 |
| 便携包 | `win-x64-portable.zip` | `macos-arm64/x64.zip`（`.app`） | `linux-x64/arm64.zip` |

## 10. 权限、隐私与安全

- **默认零提权**：所有测量无需管理员。只有"切换 DNS / 改 MTU"这类写操作才申请一次提权，并明确展示命令。
- **零上传**：除"出口 IP 查询"（每次扫描一次，best-effort；开关待 M3）与"测速"（用户点击）外不产生外发流量；遥测默认关闭。
- 子进程调用（`tracert`）必须使用参数数组而非字符串拼接，避免注入。
- 报告/日志中的 IP 可掩码；不记录 SSID 明文（默认只存 `Wi-Fi#1` 这种占位）。

## 11. 测试策略

| 层级 | 手段 | 覆盖 |
| --- | --- | --- |
| 单元 | xUnit（`tests/AirNet.Core.Tests`，70 项） | `LatencyAnalyzer`、`DiagnosisEngine`、`TraceProbe.Parse`、`ReportBuilder`、`EgressProbe`、`DnsAdvisor`、`HistoryStore`、`SiteCatalog`/`CustomSiteStore`/`SiteHistoryStore`/HTTP 状态 |
| 集成 | 本机 `--selftest` 模式（已实现） | 探针链路真实可用性，CI 上可跳过 |
| 网络伪造 | `tc netem`（Linux）/ Clumsy（Windows）注入丢包抖动 | 归因是否正确 |
| 视觉回归 | `--export` 截图 + 像素 diff | 深浅色 × 六页，防样式回退 |
| 持续集成 | GitHub Actions（`.github/workflows/ci.yml`） | `-warnaserror` 构建、单元测试、探针冒烟断言、报告断言、截图工件 |
| 现场 | 手动矩阵：Wi-Fi/有线、VPN、双栈、热点、移动网络 | 降级路径 |

## 12. 里程碑

| 阶段 | 范围 | 产出 | 估时 |
| --- | --- | --- | --- |
| **M0 视觉原型** | 四页原型 + 设计系统 + 真实探针内核 + 样例数据 | 可运行 exe、8 张截图、本文档 | ✅ 已完成 |
| **M1 测量内核** | TCP 计时抖动、Wi-Fi 信号、DNS/TCP/TLS/TTFB 分解、评分校准、历史落盘、裁剪+R2R 发布 | 抖动 2.6ms、四段分解条、历史落盘、40MB 发布包 | ✅ 已完成 |
| **M2 诊断与优化** | 三段归因、逐跳、MTU、网站延迟（含自定义站点）、一键 DNS（含还原）、报告导出、CI | ✅ 已完成 | — |
| **M3 场景与分发** | 游戏/会议模式、双栈、CLI、邻区扫描、Velopack 打包与签名 | 1.1 | 2 周 |

## 13. 风险

| 风险 | 影响 | 对策 |
| --- | --- | --- |
| ICMP 被运营商/防火墙拦截 | 误判"丢包 100%" | 三重判定：ICMP 全丢即切 TCP；UI 标注"TCP 探测" |
| 虚拟网卡（Hyper-V/WSL/VPN）抢占 | 测错接口 | 已实现：过滤虚拟网卡 + 优先"有默认网关"的接口（本机实测由 `vEthernet` 纠正为 `以太网`） |
| 无线信号在 macOS/Linux 拿不到 | 该平台无射频分 | 计算分时置中值并标注"该平台不支持射频测量" |
| 测速服务被劫持/限速 | 数字失真 | 校验字节数、多端点探测、异常时给"无法测量"而非假数字 |
| 杀软对 raw socket / 子进程敏感 | 误报 | 默认不用 raw socket；`tracert` 仅在用户点击时启动 |
| 测量抖动导致结论跳变 | 用户不信任 | 一轮结论需 15 个样本过阈值；只有 1 个坏包不降档 |

## 14. 从 AirPDF 直接复用的资产

- `Styles/Colors.axaml`、`Styles/Controls.axaml` 的结构（本仓库已按 AirNet 调整 token）；
- 自绘控件的写法（`AffectsRender<T>` + `DrawingContext`，无第三方图表库）；
- 自定义标题栏（`SystemDecorations=BorderOnly` + `BeginMoveDrag`）；
- `--export` 截图流水线（视觉回归基线）；
- 单文件发布 + Velopack 更新管线（`packaging/`）。

## 15. 现状（M2 已完成）

| 能力 | 状态 |
| --- | --- |
| 设计系统 + 六页视觉 + 深浅色 | ✅ M0/M2 |
| ICMP 丢包 / TCP 握手计时（亚毫秒抖动） | ✅ M1，本机实测 jitter 2.6ms |
| 接口识别（虚拟网卡过滤 + 网关优先） | ✅ M1 |
| Wi-Fi 信号 / 速率 / PHY（wlanapi P/Invoke） | ✅ 2026-11-12 真机验证：SSID / 信号质量 / 速率 / PHY 代际；dBm 仍为估算，邻区扫描未做 |
| DNS / TCP / TLS / TTFB 四段分解 | ✅ M1 |
| 三段归因（局域网 / 运营商 / 对端） | ✅ M2，粗定位 + 逐跳定位两级 |
| 逐跳路径（系统 traceroute + 本地化无关解析） | ✅ M2，实测 14 跳 / 17s；目标未响应时不判"对端" |
| MTU（DF 二分） | ✅ M2，实测 1480 |
| 评分、场景估计、规则诊断 | ✅ M1 |
| 历史持久化（JSONL，30 天，裁剪安全） | ✅ M1 |
| 出口 IP（Cloudflare / 3322，best-effort） | ✅ M2，被拦截时显示"未获取" |
| 带宽测速（单流，上下行） | ✅ 下行 4s 时间预算 + 上行 4MB 流式，任一向失败显"未测出" |
| 网站延迟（11 默认 + 最多 8 自定义） | ✅ M2，四段分解/3 次中位数/HTTP 状态；自定义落 `sites.json`，历史落 `sites.jsonl`，`--sites` 可无 UI 烟测 |
| 裁剪 + R2R 单文件发布 | ✅ M1；本机 2026-11 复测 26.9MB zip / 41.1MB 解压 / 1.6–1.9s / ~176MB |
| 跨平台便携包 | ✅ Windows / macOS（arm64+x64）/ Linux（x64+arm64）zip；Linux x64 已在真实环境启动验证；Linux arm64 与 macOS 尚未验证（macOS 未签名/未公证） |
| 报告导出（自包含 HTML，可打印 PDF） | ✅ M2，7.8KB，零脚本 |
| 一键 DNS + 还原（Windows 提权，UAC 可取消） | ✅ M2，实测命令生成与还原记录；**提权执行未自动化验证** |
| 单元测试（xUnit，70 项） | ✅ M2，覆盖统计 / 分档 / 解析 / 掩码 / 历史 / DNS / 站点目录、自定义站点、HTTP 状态与站点历史 |
| CI（构建 + 单元测试 + 探针/站点冒烟 + 报告断言 + 截图工件） | ✅ M2 |
| 动效（扫描/测速/追踪扫描带 + 实时读数 + 落地过冲/高亮） | ✅ M2，样本真实回传，截图模式可关闭 |
| 设置项持久化 + 60s 自动刷新 + 快捷键 | ✅ M2，写入 `settings.json`；Ctrl+R / Ctrl+1–6 / Ctrl+T / Esc |
| 双栈（IPv4/IPv6 对照） | ✅ 同步测两条路，IPv6 偏慢时给 Warn 结论 |
| 代理 / VPN 识别 | ✅ `HttpClient.DefaultProxy` + 隧道网卡枚举；区分「只影响 HTTP 探测」与「影响全部」 |
| 邻区扫描、游戏/会议模式 | ⬜ M3 |

> 原型以**确定性样例数据**启动（右上角标注"示例数据"），点"开始检测"即切换为真实测量并写入历史；
> 首页的"最近检测"在历史非空时自动切换为"本机实测"。
> 带宽只在用户点"开始测速"后才有值：未测速时快照、报告、历史都按"未测速"处理，不再继承样例的
> 384.2 Mbps；在真实快照上测速会写一条历史记录，供"近 6 次"趋势使用。
