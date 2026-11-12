# AirNet 视觉规范 v0.1

> 与 `F:\AirPdf`（AirPDF）同一套"纸张"语言的产品族版本。本文记录**继承了什么、改了什么、为什么**，
> 以及落地到 Avalonia 的 token 与组件清单。所有截图来自本仓库里可运行的原型（`src/AirNet.App`）。

---

## 1. 设计目标

AirNet 的定位是"轻量、快速检测和优化网络"。视觉要服务三个判断，而不是装饰：

| 原则 | 视觉落地 |
| --- | --- |
| **一眼可读**：3 秒内知道网络好不好 | 首页一句结论 + 一个健康分环；仪表盘首屏只有 6 张卡片 |
| **轻**：像一个随手打开的工具，不像安全软件 | 暖白纸感底、细描边、无渐变无光效、无常驻图标；单窗口、无向导 |
| **可信**：每个数字都能解释 | 数字 + 单位 + 口径（P95、采样数、满量程）；结论后面永远跟证据和建议 |

## 2. 与 AirPDF 的继承与差异

**继承**（保持产品族一致性）：

- 暖白"纸张"色板：`#F1EEE7` 底、`#FBFAF7` 卡面、`#DED7CB` 描边；
- 字号阶梯与字重：h1 34 / h2 21 / h3 15 / eyebrow 10 / caption 11.5；
- 圆角体系：卡片 14、控件 8–10、胶囊 999；
- 布局骨架：44px 品牌条 + 46px 工具条 + 侧栏 + 内容 + 30px 状态条；
- 手绘线条图标（Lucide 24×24，1.8 描边）、衬线体标题插画、"纸叠"构图；
- 主题：浅色 / 深色 / 跟随系统三态，同一套 token key。

**改变**（让 AirNet 自己成立）：

| 维度 | AirPDF | AirNet | 理由 |
| --- | --- | --- | --- |
| 主色 | 暖橙 `#E3563B` | **暖橙 `#E3563B`（浅色）** | 浅色与 AirPDF 同一支橙笔；操作、品牌、导航选中和扫描动效用它 |
| 语义色 | 无（只有强调/危险） | good / warn / bad 三档 | 网络诊断必须有三档状态；good 继续用青绿，避免橙色同时代表“健康”和“操作” |
| 第二色 | 青 `#2E6F63`（次） | 青 `#2E6F63`（good / 图表） | 橙色负责可点击层，青绿负责健康与数据层 |
| 深色 | 暖橙 `#FF6B4E` | **保持现有青绿 `#69B3A4`** | 深色不改，切入后仍是当前 AirNet 的视觉记忆 |
| 主体 | 单文档阅读画布 | 仪表盘卡片网格 + 侧栏导航 | 工具类产品需要固定六页，阅读器是文档驱动 |
| 数字声音 | 无 | 专用数值字号/字距，等宽用于地址与端口 | 测量值需要"仪表感" |
| 主题默认 | 跟随系统 | 跟随系统（原型默认浅色便于评审） | — |

> 结论：**同一张纸，同一支橙笔；健康仍用青绿。** 浅色与 AirPDF 的暖橙品牌一致，
> 但 good / warn / bad 仍靠语义色和文字表达；深色完全保持现状。

## 3. 色板（Token）

### 3.1 基础层

| Token | Light | Dark | 用途 |
| --- | --- | --- | --- |
| `AppBackgroundBrush` | `#F1EEE7` | `#13171C` | 应用底 |
| `TitleBarBackgroundBrush` | `#F4F0E8` | `#14191F` | 品牌条 |
| `ToolbarBackgroundBrush` | `#F9F7F2` | `#181D23` | 状态工具条 |
| `SidebarBackgroundBrush` | `#EAE5DC` | `#171C22` | 侧栏 |
| `SurfaceBrush` | `#FBFAF7` | `#1C222A` | 卡片 |
| `SurfaceAltBrush` | `#F6F2EB` | `#202831` | 卡片内嵌块 |
| `SurfaceHoverBrush` | `#EDE7DD` | `#2A333D` | 悬停 |
| `SurfacePressedBrush` | `#E3DCCF` | `#333E4A` | 按下 / 滑轨 |
| `BorderBrush` | `#DED7CB` | `#2C343E` | 1px 描边 |
| `BorderStrongBrush` | `#C7BEB0` | `#43505D` | 强调描边、开关轨 |
| `TextPrimaryBrush` | `#1D232C` | `#F3EFE6` | 主文本 |
| `TextSecondaryBrush` | `#69645C` | `#ABA49A` | 说明文本 |
| `TextTertiaryBrush` | `#9A9286` | `#7B756D` | 辅助/时间戳 |
| `StatusBackgroundBrush` | `#F9F7F2` | `#181D23` | 状态条 |

### 3.2 强调与语义

| Token | Light | Dark | 说明 |
| --- | --- | --- | --- |
| `AccentBrush` | `#E3563B` | `#69B3A4` | 浅色品牌/主操作/导航/扫描；深色保持青绿 |
| `AccentHoverBrush` | `#C9482F` | `#86C6B8` | 主按钮悬停 |
| `AccentSoftBrush` | `#FBE4DD` | `#1E312E` | 橙色浅底（选中态、品牌底） |
| `AccentTextBrush` | `#A93620` | `#8FD0C2` | 橙色文字、链接 |
| `AccentOnBrush` | `#FFFFFF` | `#10201C` | **主色底上的文字**（深色主题主色偏亮，必须用深墨） |
| `GoodBrush` | `#2E6F63` | `#69B3A4` | 健康、健康环、数据图表（浅色不再等于 accent） |
| `GoodSoftBrush` | `#E2EFEB` | `#1E312E` | 健康浅底 |
| `WarnBrush` | `#A9701A` | `#E0A85C` | 警告（暖琥珀，仍在纸张色域内） |
| `WarnSoftBrush` | `#F6EAD4` | `#33280F` | 警告浅底 |
| `BadBrush` | `#B23A2B` | `#FF8E79` | 严重（陶土系，与 AirPDF 主色同源） |
| `BadSoftBrush` | `#F8E2DD` | `#39221F` | 严重浅底 |
| `BrandBrush` | `#E3563B` | `#FF6B4E` | 品牌/插画点缀，与浅色 accent 同值 |
| `TrackBrush` | `#E7E0D4` | `#2B343E` | 环/条/滑轨 |
| `GridLineBrush` | `#E4DCD0` | `#242C35` | 图表网格 |
| `GuideBrush` | `#B9AE9E` | `#4A5764` | 图表参考线（均值/P95） |
| `ChartFillBrush` | `#42→00 2E6F63` | `#4D→00 69B3A4` | 面积图渐变（跟随 Good，不跟随 accent） |

对比度（浅色）：主文本 12.9:1、次文本 5.6:1。橙色主按钮上的白字约 3.7:1，
属于大字号/粗体按钮的视觉折中；正文和关键状态不依赖白字。深色沿用原青绿方案。

## 4. 字体与字号阶梯

```
AppFontFamily     : Inter, Segoe UI, Microsoft YaHei UI, Noto Sans CJK SC, PingFang SC, sans-serif
DisplayFontFamily : Georgia, Times New Roman, Songti SC, SimSun, serif     ← 仅插画标题
MonoFontFamily    : Cascadia Mono, Consolas, SF Mono, Menlo, monospace     ← IP / 端口 / 时间
```

| 角色 | 字号 / 字重 / 字距 | 用例 |
| --- | --- | --- |
| `.h1` | 34 / SemiBold / -0.8，行高 44 | 首页主张（两行） |
| `.h2` | 21 / SemiBold / -0.2 | 卡片内结论 |
| `.h3` | 15 / SemiBold | 区块标题 |
| `.eyebrow` | 10 / Bold / +1.3 / 全大写 | 栏目元信息（NAVIGATE、LATENCY / 60S） |
| `.caption` | 11.5 / Regular | 说明、副标题 |
| `.micro` | 10.5 / Regular / 三级色 | 时间戳、口径注释 |
| `.value` | 23 / SemiBold / -0.7 | 指标值 |
| `.valueBig` | 38 / SemiBold / -1.6 | 测速大数字 |
| `.unit` | 10.5 / SemiBold / 二级色 | 单位（ms、Mbps、%） |
| `.mono` | 11 / Regular 等宽 | IP、DNS、耗时 |

**硬规则：中文不小于 10px。** 8–9px 的中文在 1× 渲染下笔画会糊成一团（原型第一版踩过，`延迟/丢包/抖动` 全部不可读），因此中文最低 10，只有拉丁字母与数字允许 8–9.5。

## 5. 间距、圆角、描边、阴影

- 间距基数 **2 的倍数**；卡片内边距 16–22；卡片间距 12（列表内 8–10）。
- 圆角：卡片 14、内嵌块 10、按钮 8–10、胶囊 999、开关 11。
- 描边统一 **1px** `BorderBrush`；**不用重阴影做层级**，只用 1px 线与底色差。
- 阴影只在两处：① 首页"纸叠"插画（`0 14/22/50` 三级）；② 浮层（`0 12 30 #1A000000`）。
- 分割线：1px `BorderBrush`，高度 14（行内）/ 1px 通栏（块间）。

## 6. 布局骨架

```
┌────────────────────────────────────────────── 44 品牌条：Logo+字标 │ 链路胶囊 │ 示例数据 │ 开始检测 │ 主题 │ 窗口按钮
├────────────────────────────────────────────── 46 状态条：结论 │ 上次检测 │ 采样数 │ 峰值 ………… 自动刷新开关 │ 导出报告
├──────────┬───────────────────────────────────
│ 216 侧栏 │ 内容区（各页自管，最大宽 1000–1160）
│ WORKSPACE│
│  总览    │
│  实时检测│
│  带宽测速│
│  网站延迟│   ← M2 新增：11 站 + 自定义站点 + 分段条
│  链路路径│   ← M2 新增：归因 + 逐跳 + MTU
│  设置    │
│ 本机链路 │
│ 轻量模式 │
├──────────┴───────────────────────────────────
└────────────────────────────────────────────── 30 状态条：运行状态 │ 延迟 │ 丢包 │ 采集时间 │ 版本
```

内容区最大宽 1160，窗口最小 1040×700；页面内容在视口内居中，避免大屏拉散。

## 7. 组件

| 组件 | 类名 / 控件 | 规格 |
| --- | --- | --- |
| 主按钮 | `Button.primary` | 高 34–40、圆角 10、主色底 + `AccentOnBrush` 字 |
| 软按钮 | `Button.soft` | 主色浅底 + 主色字，用于"开始检测"这类次级主操作 |
| 描边按钮 | `Button.ghost` | 卡面色 + 1px 描边 |
| 图标按钮 | `Button.icon` / `.small` | 34 / 28 方，圆角 8 |
| 胶囊 | `Button.chip` / `Border.pill` | 高 28–26，圆角 999，1px 描边 |
| 迷你动作 | `Button.mini` | 高 24，`SurfaceAlt` 底，用于诊断建议的"优化 DNS" |
| 侧栏项 | `Button.nav` + `.active` | 高 34，选中为主色浅底 + 左图标转主色 |
| 卡片 | `Border.card` | 卡面色 + 1px 描边 + 圆角 14 |
| 内嵌块 | `Border.tile` | `SurfaceAlt` + 描边 + 圆角 10 |
| 分段控件 | `Button.seg` + `.active` | 高 30，选中白底 + 描边 + 微阴影 |
| 开关 | `ToggleButton.switch` | 38×21，选中主色，白钮 15 |
| 状态点 | `Ellipse.sevDot` / `.tileDot` | 5–7px，good/warn/bad/info 四态 |
| 窗口按钮 | `Button.windowControl` | 46×32，关闭键悬停 `#C42B1C` |

## 8. 图表

四个自绘控件（无第三方图表库，全部 `Avalonia.Control` + `DrawingContext`）：

| 控件 | 参数 | 用法 | 规范 |
| --- | --- | --- | --- |
| `ScoreRing` | `Value/Maximum/Thickness/StartAngle(-90)/SweepAngle(360)/TrackBrush/ProgressBrush` | 健康分、上下行测速 | **360° 圆环**，缺口在 12 点方向；轨道平头（圆头会在接近满值处留下一颗"孤点"），进度圆头 2px 收边 |
| `Sparkline` | `Series/Stroke/Fill/GridLines/GuideValue/ShowEndDot` | 延迟时间线、卡片缩略 | 面积用 `ChartFillBrush`；均值参考线 1px 虚线 `3,4`；末点 3.2px 圆点；自适应 min/max 并留 12% 余量 |
| `MeterBar` | `Value(0-1)/TrackBrush/FillBrush` | 场景分数、逐跳耗时 | 高 3–5，圆角半高，短值时保证最小可见宽度 |
| `SignalBars` | `Percent/FillBrush/EmptyBrush` | 无线信号 | 4 根 3px 柱（4/7/10/13 高），按 25% 阶梯点亮 |

**首包分解条**（M1 新增，位于"链路"卡片底部）：把 DNS / TCP / TLS / 等待首字节四段按耗时占比画成一条
284px 的堆叠条，同一色系四档透明度（1.0 / 0.72 / 0.46 / 0.24）递减，下方图例给出毫秒数，右侧是总首包时间。
用同一主色的透明度阶梯而不是四种颜色，是因为四段是同一条路径上的阶段，不是四类不同的事物。

规则：**图表只用 Good + 参考线灰**；橙色只用于操作/品牌，出现告警色时整页最多一处，避免"仪表盘花屏"。

## 9. 首页插画：纸叠

三张"报告纸"叠放，是 AirPDF 首页纸叠的直系变体：

- 底层：空白纸 + 左缘青色索引条（`-12°`）；
- 中层：空白纸 + 右缘琥珀索引条（`7°`）；
- 顶层：**唯一有内容的卡**（`-2.5°`），承载 "AIRNET / DIAGNOSTIC" 小标、衬线标题 `Know your signal.`、健康分环、延迟曲线、三项指标、页脚字标。

规则：**只有最上面一张写内容**。后排纸一旦露出被裁切的文字，会读成渲染错误（第一版就是这样，已改为纯纸 + 索引条）。

## 10. 动效（M2 已实现，可选方案见 [motion.html](design/motion.html)）

统一开关 `Features.Motion`：截图导出与 CI 会关掉它，保证截图确定；将来"减少动态"偏好也挂在同一处。

> 实装的是 **A+B 混合**（下表）。对比页 [motion.html](design/motion.html) 仍保留三版原始方案；
> 动态预览：[motion.html](design/motion.html)。静态截图不再入库，避免示例中的网络地址进入图片。

| 场景 | 实现 | 参数 |
| --- | --- | --- |
| **检测中 · A 扫描带** | `ScanSweep` 覆盖层：一条 150px 渐变光带自上而下扫过页面，不参与布局 | 1.6s/循环，16ms 帧 |
| **检测中 · B 实时读数** | 探针逐样本回传（15 个 ICMP + 10 个 TCP），四块指标卡实时刷新，环变成旋进弧、分数显示「—」 | 环 275°/s |
| **检测中 · 曲线** | 按 `Capacity` 槽位生长（ICMP 15 / TCP 10 槽），阶段切换时清空重画 | 不重复扫入 |
| **检测中 · 工具条** | 当前探针 + 计时 + 底部 3px 进度条（向目标缓动） | 120ms 步进，系数 0.22 |
| **测速 / 追踪 · 检查动画** | 页面级 `ScanSweep` + 脉冲点 + 实时计时；测速显示「正在测速 · x.x s」，追踪显示「正在追踪 · x.x s」 | 200ms 步进；截图模式关闭 |
| 检测完成 | 环 600ms 过冲到 1.035 再回稳；数字 420ms 滚动；指标卡 480ms 高亮一次；曲线扫入 | `CubicEaseOut` |
| 页面切换 | 新页面淡入 | 160ms `CubicEaseOut` |
| 条填充 | `MeterBar.Value` 带 `DoubleTransition` | 520ms `CubicEaseOut` |

**为什么 ICMP 阶段抖动显示「—」**：ICMP 只有 1ms 分辨率，抖动恒为 0，直接显示像坏掉的表；TCP 阶段开始才有真实抖动。

**为什么曲线在阶段切换时清空**：ICMP 与 TCP 是两种不同的测量，把 35ms 的 ICMP 与 39ms 的 TCP 接成一条线，会画出不存在的走势。

**为什么监控页不再整页降透明**：A+B 之后检测中的监控页是「活」的，降透明只留给其它四页——它们确实还是旧数据。

**为什么数字必须来自真实样本**：跳动可以是假的，但假跳动会在"结果落地"的瞬间露出破绽（数字会跳回去）。所有 live 值都直接来自 `IProgress<LiveSample>`。

**为什么测速/追踪也用扫描带**：这两个动作分别要 5–15s 和 8–20s，只改按钮文字会像卡死；
扫描带 + 脉冲点 + 计时是同一套"正在工作"的语言，且 `ScanSweep` 是覆盖层，不会推动布局。

**未做**：按压微缩放（收益太低）、C 方案的全屏模糊（Skia 大面积 blur 与"轻量"冲突）。

## 11. 无障碍

- 中文最小 10px、正文 11.5–14；对比度见 §3.2。
- 所有图标按钮都有 `ToolTip` 与可读名称；窗口按钮可 Tab 到达。
- 三档状态**不只靠颜色**：结论用文字（优秀/良好/一般/较差）+ 点 + 分数，色盲用户仍可判断。
- 主题跟随系统；浅色主色为暖橙、深色保持青绿，主按钮文字按各自 `AccentOnBrush` 取白/深墨。

## 12. 代码位置

```
src/AirNet.App/
  Styles/Colors.axaml      色板 token（Light/Dark 双字典 + 字体 + 圆角）
  Styles/Icons.axaml       线性图标（Lucide 风格；含品牌脉冲标记）
  Styles/Controls.axaml    字号阶梯、按钮、卡片、开关、状态点、图表默认值
  Controls/ScoreRing.cs    360° 环
  Controls/Sparkline.cs    延迟曲线
  Controls/MeterBar.cs     条
  Controls/SignalBars.cs   信号柱
  Controls/ScanSweep.cs   检测/测速/追踪的扫描带（覆盖层，不参与布局）
  Features/Motion.cs      动效总开关与时长
  Services/SettingsStore.cs  settings.json（主题 / 自动刷新 / 双探测 / 保留历史）
  Views/ShellView.axaml    骨架：品牌条 / 状态条 / 侧栏 / 状态栏
  Views/OverviewView.axaml 首页 + 纸叠插画
  Views/MonitorView.axaml  仪表盘
  Views/SpeedView.axaml    测速
  Views/SiteLatencyView.axaml  网站延迟（主页面）
  Views/SiteManagerView.axaml  管理站点弹层
  Views/PathView.axaml     链路路径
  Views/SettingsView.axaml 设置
```

## 13. 截图

| 页面 | 浅色 | 深色 |
| --- | --- | --- |
| 总览 | ![总览浅色](shots/01-overview-light.png) | ![总览深色](shots/01-overview-dark.png) |
| 实时检测 | ![仪表盘浅色](shots/02-monitor-light.png) | ![仪表盘深色](shots/02-monitor-dark.png) |
| 带宽测速 | ![测速浅色](shots/03-speed-light.png) | ![测速深色](shots/03-speed-dark.png) |
| 网站延迟 | ![站点浅色](shots/04-sites-light.png) | ![站点深色](shots/04-sites-dark.png) |
| 链路路径 | ![路径浅色](shots/05-path-light.png) | ![路径深色](shots/05-path-dark.png) |
| 设置 | ![设置浅色](shots/06-settings-light.png) | ![设置深色](shots/06-settings-dark.png) |

截图由 `AirNet.exe --export <目录> --scale 1` 生成：程序在屏幕外真实渲染窗口后逐页抓取，与最终运行时像素一致。
