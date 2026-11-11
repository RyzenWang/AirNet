# AirNet

**轻量、快速的网络检测与优化工具** —— 点一下，三秒告诉你网络好不好、问题在路由器还是运营商。

基于 .NET 10 + Avalonia 11.3，与 [AirPDF](F:\AirPdf) 同一套"纸张"设计语言，同一套单文件发布与更新管线。

![总览（浅色）](docs/shots/01-overview-light.png)

![实时检测（浅色）](docs/shots/02-monitor-light.png)

![链路路径（浅色）](docs/shots/04-path-light.png)

---

## 当前状态

**M2 进行中**：五页界面、评分与诊断、真实探测（含亚毫秒抖动）、三段归因、逐跳路径、MTU、历史落盘、
裁剪单文件发布都已跑通。报告导出与一键 DNS 仍是占位。

| 部分 | 状态 |
| --- | --- |
| 视觉系统（五页 × 深浅色） | ✅ M0/M2 |
| 真实探测：ICMP 丢包 / TCP 计时 / DNS·TCP·TLS·TTFB | ✅ M1 |
| 亚毫秒抖动（`Stopwatch` 计时 TCP 握手） | ✅ 本机实测 2.6ms（原先恒为 0） |
| Wi-Fi 信号 / 速率 / PHY（wlanapi） | ✅ 代码完成，未在真实无线网卡上验证 |
| 评分、场景估计、规则诊断 | ✅ M1 |
| 历史（JSONL，30 天，裁剪安全） | ✅ M1 |
| 带宽测速（单流） | ✅ |
| **三段归因**（局域网 / 运营商 / 对端） | ✅ M2 粗定位 + 逐跳定位 |
| **逐跳路径**（系统 traceroute，本地化无关解析） | ✅ M2 实测 14 跳 / 17s |
| **MTU**（DF 二分） | ✅ M2 实测 1480（PPPoE） |
| 裁剪 + R2R 单文件发布 | ✅ 40.6MB / 0.9–1.1s / 154MB |
| 报告导出、一键 DNS、双栈 | ⬜ 见 [技术方案](docs/TECH_PLAN.md) |

程序启动时展示**确定性样例数据**（右上角标"示例数据"），点"开始检测"即切换为真实测量并写入历史。

## 快速开始

需要 .NET 10 SDK。

```powershell
git clone <repo> ; cd AirNet

dotnet run --project src/AirNet.App                              # 启动应用
dotnet run --project src/AirNet.App -- --selftest out.txt        # 跑一轮真实探测 + 历史往返，输出报告
dotnet run --project src/AirNet.App -- --export docs/shots       # 重新生成全部截图

powershell -File packaging/publish.ps1                           # 裁剪单文件发布 + 实测启动/内存
```

`--selftest` 的输出示例（本机，有线 1000M）：

```
interface=以太网      local=192.168.1.2   gateway=192.168.1.1
probeMode=Tcp         avg=38.6  p95=41.9  jitter=2.6  loss=0  sent=20  received=20
dnsLookupMs=15.8  tcpMs=5.1  tlsMs=53.8  ttfbMs=8.5   timingOk=True
score=88  grade=良好  hops=2  findings=2  historyRoundTrip=True
traceOk=True hops=14 reached=14     mtu=1480 payload=1452
culprit=无 precise=True headline=整条路径没有明显瓶颈
```

## 目录

```
src/AirNet.Core/         探测与诊断内核（无 UI 依赖）
  Probes/                InterfaceInspector · LatencyProbe · ThroughputProbe
                         ApplicationTimingProbe · TraceProbe · MtuProbe · WlanRadio · MeasurementRunner
  Diagnostics/           NetworkSnapshot · LatencyAnalyzer · DiagnosisEngine
                         AttributionEngine（三段归因）· PathAnalysis（逐跳/MTU 模型）
  History/               JSONL 历史（裁剪安全，源生成序列化）
  Demo/                  确定性样例数据
src/AirNet.App/          Avalonia 应用
  Styles/                Colors(token) · Icons · Controls(字号/按钮/卡片/开关)
  Controls/              ScoreRing · Sparkline · MeterBar · SignalBars（全部自绘）
  Views/                 ShellView + Overview / Monitor / Speed / Path / Settings
  ViewModels/            Shell（导航与扫描生命周期）· 五个页面 VM · SnapshotView（展示投影）
packaging/publish.ps1    裁剪 + R2R 发布，并打印体积/启动/内存
docs/
  DESIGN.md              视觉规范：色板、字号、组件、图表、动效、无障碍
  TECH_PLAN.md           技术方案：架构、探针、评分与归因、性能预算、里程碑
  FEATURES.md            功能清单（P0/P1/P2 + 验收口径）
  shots/                 浅色/深色共 10 张截图
```

## 设计要点

- **同一张纸，换了一支笔**：沿用 AirPDF 的暖白纸面色板（`#F1EEE7` / `#FBFAF7` / `#DED7CB`）与字号阶梯；
  主色由陶土改为青绿（健康），陶土降级为**告警色** —— 链路变差时才会出现。
- **数字有口径**：每个指标都带单位、P95、采样数或满量程，不给出无法解释的分数。
- **结构可读**：44px 品牌条 + 46px 状态条 + 216px 侧栏 + 30px 状态栏，四页共用，不做导航栈重建。

细节见 [docs/DESIGN.md](docs/DESIGN.md)。

## 已知限制

- **Wi-Fi 采样未在硬件上验证**：本机是有线连接，`wlanapi` 分支只走通了"无无线关联 → 返回 null"的降级路径；插上 Wi-Fi 后才算真正验收。
- 内存 154MB、启动 0.9–1.1s：这是 Avalonia + Skia + Inter 的固定成本，与 AirPDF 同量级；"轻"体现在无驱动、无后台常驻、40MB 解压体积，不是内存数字。
- 截图导出必须用 `--scale 1`：Avalonia 的 `RenderTargetBitmap` 在 2× DPI 下会复用 1× 字形缓存，文字发糊（已记录，未绕开）。
- TCP 握手 RTT 比 ICMP 高 3–5ms（多一层本地协议栈），评分阈值已按此校准；TCP 也观测不到丢包，所以丢包仍由 ICMP 给出。
- 报告导出、一键优化、快捷键、动效均为占位。

## 许可

MIT。
