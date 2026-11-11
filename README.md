# AirNet

**轻量、快速的网络检测与优化工具** —— 点一下，三秒告诉你网络好不好、问题在路由器还是运营商。

基于 .NET 10 + Avalonia 11.3，与 [AirPDF](F:\AirPdf) 同一套"纸张"设计语言，同一套单文件发布与更新管线。

![总览（浅色）](docs/shots/01-overview-light.png)

![实时检测（浅色）](docs/shots/02-monitor-light.png)

---

## 当前状态

这是 **M0 视觉原型**：界面、设计系统、评分模型与真实探测内核已经跑通，历史/报告/一键优化还是占位。

| 部分 | 状态 |
| --- | --- |
| 视觉系统（四页 × 深浅色） | ✅ 完成 |
| 真实探测（ICMP / TCP / DNS / 网卡识别） | ✅ 已实现，本机自测通过 |
| 评分、场景估计、规则诊断 | ✅ 已实现（系数待现场校准） |
| 带宽测速 | ✅ 单流已实现并接线 |
| Wi-Fi 信号、逐跳、历史、报告、一键优化 | ⬜ 方案见 [技术方案](docs/TECH_PLAN.md) |

程序启动时展示**确定性样例数据**（右上角标"示例数据"），点"开始检测"即切换为真实测量。

## 快速开始

需要 .NET 10 SDK。

```powershell
git clone <repo> ; cd AirNet

dotnet run --project src/AirNet.App            # 启动应用
dotnet run --project src/AirNet.App -- --selftest out.txt   # 跑一轮真实探测，输出报告（无窗口）
dotnet run --project src/AirNet.App -- --export docs/shots  # 重新生成全部截图
```

`--selftest` 的输出示例（本机）：

```
interface=以太网      local=192.168.1.2   gateway=192.168.1.1
avg=35  p95=35  jitter=0  loss=0  sent=20  received=20
dnsLookupMs=22.7   score=90   grade=优秀   hops=2
```

## 目录

```
src/AirNet.Core/         探测与诊断内核（无 UI 依赖）
  Probes/                InterfaceInspector · LatencyProbe · ThroughputProbe · MeasurementRunner
  Diagnostics/           NetworkSnapshot · LatencyAnalyzer · DiagnosisEngine
  Demo/                  确定性样例数据
src/AirNet.App/          Avalonia 应用
  Styles/                Colors(token) · Icons · Controls(字号/按钮/卡片/开关)
  Controls/              ScoreRing · Sparkline · MeterBar · SignalBars（全部自绘）
  Views/                 ShellView + Overview / Monitor / Speed / Settings
  ViewModels/            Shell（导航与扫描生命周期）· 四个页面 VM · SnapshotView（展示投影）
docs/
  DESIGN.md              视觉规范：色板、字号、组件、图表、动效、无障碍
  TECH_PLAN.md           技术方案：架构、探针、评分与归因、性能预算、里程碑
  FEATURES.md            功能清单（P0/P1/P2 + 验收口径）
  shots/                 浅色/深色共 8 张截图
```

## 设计要点

- **同一张纸，换了一支笔**：沿用 AirPDF 的暖白纸面色板（`#F1EEE7` / `#FBFAF7` / `#DED7CB`）与字号阶梯；
  主色由陶土改为青绿（健康），陶土降级为**告警色** —— 链路变差时才会出现。
- **数字有口径**：每个指标都带单位、P95、采样数或满量程，不给出无法解释的分数。
- **结构可读**：44px 品牌条 + 46px 状态条 + 216px 侧栏 + 30px 状态栏，四页共用，不做导航栈重建。

细节见 [docs/DESIGN.md](docs/DESIGN.md)。

## 已知限制

- **性能尚未达标**：当前是未裁剪的框架依赖构建，实测冷启动 1391ms、工作集 154MB。裁剪 + R2R 在 M1 完成，目标 15–20MB / ≤120MB / ≤600ms。
- 截图导出必须用 `--scale 1`：Avalonia 的 `RenderTargetBitmap` 在 2× DPI 下会复用 1× 字形缓存，文字发糊（已记录，未绕开）。
- 抖动当前由 `Ping.RoundtripTime`（整毫秒）计算，同一条链路上常恒为 0；M1 改用 `Stopwatch` 计时的 TCP 握手样本。
- 诊断建议的按钮、报告导出、快捷键、动效均为占位。

## 许可

MIT。
