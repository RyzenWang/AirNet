# 网站延迟测试：功能、交互与视觉方案 v0.2

> 目标：回答"我常去的网站，现在打开得快不快；慢是慢在解析、连接、握手，还是对方首包"。
> 这是一次**按需**测试，不进入 3 秒主扫描，也不并入健康分。
>
> **状态：已实现（2026-11）**。六页导航、11 个默认站、最多 8 个自定义站、`sites.json`、
> HTTP 状态识别、`sites.jsonl` 历史、`--sites` CLI、报告表格、123 项单元测试都已接入；
> 实现截图见 [04-sites-light.png](shots/04-sites-light.png)。
>
> 视觉稿：[主页面](design/site-latency-mockup.png) · [HTML 源稿](design/site-latency-mockup.html)；
> 管理弹层实现见 `Views/SiteManagerView.axaml`。
>
> 稿内数值是设计示例，不是实测结果；平均 148 ms 把 4 个跨境站点也算了进去。

---

## 1. 定位与非目标

**定位**：在"整条链路"之外，补一层"具体站点"的体验观测。

| 用户问题 | 本功能给出的答案 |
| --- | --- |
| 百度/抖音/B 站现在打开快吗？ | 每个站点的总耗时 + 四段分解 |
| 是 DNS 慢，还是 TLS 慢？ | DNS / TCP / TLS / 等待首字节四段 |
| 只有 Google/GitHub 慢，是我网络坏了吗？ | 国内与国际分组判断，不直接归因本机 |
| 我自己常用的站点能不能测？ | 支持新增自定义站点，持久化到本机 |
| 上次是不是也这样？ | 与上次的差值；趋势放 M3 |

**非目标**

- 不做页面加载性能（LCP/FCP/资源瀑布），只测首字节。
- 不做网页内容抓取、截图、全文下载。
- 不做站点可用性监控（7×24 探活）。
- 不把站点延迟合进 0–100 健康分：CDN、跨境、站点自身限流都会影响结果，
  合进去会让"网络健康分"失去解释性。

---

## 2. 站点清单

### 2.1 默认站点（11 个：国内 7 + 国际 4）

| 站点 | Host | 分组 | 选择理由 |
| --- | --- | --- | --- |
| 百度 | `www.baidu.com` | 国内 | 搜索入口，覆盖面最广 |
| 腾讯 | `www.qq.com` | 国内 | 门户/社交，CDN 节点多 |
| 抖音 | `www.douyin.com` | 国内 | 短视频，首屏/TLS 感知强 |
| 小红书 | `www.xiaohongshu.com` | 国内 | 图片社区，DNS/TTFB 敏感 |
| 哔哩哔哩 | `www.bilibili.com` | 国内 | 视频社区，TLS/首包占比高 |
| 知乎 | `www.zhihu.com` | 国内 | 图文社区，DNS 影响明显 |
| 微博 | `www.weibo.com` | 国内 | 社交，弱网时最容易卡 |
| GitHub | `github.com` | 国际 | 开发常用，跨境对照 |
| Google | `www.google.com` | 国际 | 最典型的跨境基线 |
| YouTube | `www.youtube.com` | 国际 | 视频/CDN 跨境对照 |
| 推特 / X | `x.com` | 国际 | 社交跨境对照 |

**维护原则**：只选首页稳定、无需登录、允许匿名 GET 的站点；不选登录后才有内容的页面。
默认站点固定，不可编辑/删除；M3 再考虑隐藏默认站点的开关。

### 2.2 自定义站点

- 新增入口：页面右上角「管理站点」，右栏「站点清单」里也有同一个入口。
- 自定义站点归入 **「自定义」** 分组，不混进国内/国际计数；`全部` 会包含它们。
- 上限 **8 个**；不允许与默认站点或其他自定义站点重复（按 host 去重）。
- 只支持 **HTTPS 443**；地址可以填 `https://example.com` 或 `example.com`，
  统一归一化为 host，忽略 path/query；拒绝 `http://`、空格、文件路径和无法解析的 host。
- 保存前可点「测试连通」，跑一次 DNS/TCP/TLS/TTFB；失败会显示原因，
  但**允许保存为「未验证」**，避免站点临时故障时无法录入。
- 自定义站点可编辑、删除；删除需要二次确认（小弹层，不用系统对话框）。

这些规则解决的问题：

| 规则 | 原因 |
| --- | --- |
| 只允许 HTTPS 443 | 测的是网页首字节，不是任意 TCP 服务 |
| 归一化为 host | 保证同一站点不因 path/参数重复入库 |
| 上限 8 | 33 + 24 个请求会让单次测试过长 |
| 允许未验证保存 | 站点临时 502/被墙时，用户仍可先把条目建好 |
| 二次确认删除 | 自定义站点是用户输入，误删成本高 |

---

## 3. 测试流程

```
点击「开始测试」
  → 默认 11 + 自定义 N 个站点进入队列
  → 每站 3 次独立 HTTPS 请求（并发 4）
  → 每次记录 DNS / TCP / TLS / 等待首字节
  → 取总耗时中位数的那一次作为该站结果（四段相加仍等于总耗时）
  → 一行一行落位；全部完成显示汇总
```

- **采样**：每站 3 次，取中位数；第一次 DNS 可能是冷的，后两次通常更快，中位数能压掉单次抖动。
- **并发**：4 个站点同时测；单站超时 4s，整体上限 20s。
- **只读首字节**：`GET /` + `Connection: close`，读到第一个字节即断开；不下载 body。
- **可取消**：点「停止」后不再发起新请求，已完成保留，未完成标「已取消」。
- **不自动跑**：不进入主扫描、不参与 60s 自动刷新；避免每 60 秒打几十个请求。
- **失败保留**：DNS/TCP/TLS/首包任一段失败，保留已完成段并标出失败类型，不用 0 冒充。
- **默认过滤**：`全部`；切换 `国内 / 国际 / 自定义` 只过滤显示，不重测。

---

## 4. 结果与判断

每个站点输出：

| 字段 | 说明 |
| --- | --- |
| `totalMs` | 四段之和，排序主键 |
| `dnsMs` | 首次域名解析 |
| `tcpMs` | 新建 TCP 连接 |
| `tlsMs` | TLS 握手 |
| `ttfbMs` | 发出请求到第一个字节 |
| `success` / `failureKind` | 成功 / DNS / TCP / TLS / 首包超时 / 被拦截 / 取消 |
| `deltaMs` | 与上一次同站结果比较 |
| `httpStatus`（可选） | 读到状态行时记录；403 仍算"可达但被站点拦截" |

**分档只用于展示，不参与健康分**：

- `≤ 80 ms` 良好（青绿）
- `80–200 ms` 偏慢（琥珀）
- `> 200 ms` 跨境/异常（陶土）

判断文案只在证据足够时下结论：

- 国内 7 站都正常，只有国际 4 站慢 → "跨境/CDN 因素，不建议归因本机"。
- 所有站点 TLS 都高 → "握手阶段偏慢，可能与 TLS 协商/中间盒有关"。
- 所有站点 DNS 都高 → "解析偏慢，考虑换 DNS"。
- 多数站点 TCP 都高 → "到站点的连接建立慢，先看本机出口/运营商"。
- 单个自定义站点超时、其他都正常 → "该站点不可达/被拦截，不代表整体网络差"。

---

## 5. 自定义站点：交互细节

### 5.1 打开「管理站点」

两个入口：

1. 页面右上角 `管理站点`（ghost 按钮，和「重新测试」并列）。
2. 右栏「站点清单」卡片底部 `管理站点`。

打开后是应用内浮层（不是新 Window）：背景压暗，焦点锁在弹层内，`Esc`/右上角 `×` 关闭。

### 5.2 弹层结构（900×660）

```
┌ 管理站点                                          默认 11 · 自定义 0 · 只测 HTTPS 首字节   × ┐
├───────────────────────────────────────────┬──────────────────────────────────┤
│ 国内 7                                     │ 新增自定义站点                     │
│ [百]百度 www.baidu.com          固定       │ 名称  [ 例如：公司官网 ]           │
│ [腾]腾讯 www.qq.com             固定       │ 地址  [ https://status.example... ]│
│ [抖]抖音 www.douyin.com         固定       │ 分组  自定义（固定）               │
│ [小]小红书 www.xiaohongshu.com  固定       │                                    │
│ [哔]哔哩哔哩 www.bilibili.com   固定       │ 只支持 HTTPS（443）；保存前会跑一次 │
│ [知]知乎 www.zhihu.com          固定       │ DNS / TCP / TLS / 首字节测试。      │
│ [微]微博 www.weibo.com          固定       │ [测试连通] [保存站点]              │
│ 国际 4                                     │                                    │
│ [GH]GitHub github.com           固定       │ 最多 8 个；不允许重复域名；         │
│ [G]Google www.google.com        固定       │ 可在列表里删除。                   │
│ [Y]YouTube www.youtube.com      固定       │                                    │
│ [X]推特 / X x.com               固定       │                                    │
│ 自定义 0 / 8                               │                                    │
│ 还没有自定义站点。右侧添加后，会出现在       │                                    │
│ 「自定义」分组；默认站点保持固定。           │                                    │
├───────────────────────────────────────────┴──────────────────────────────────┤
│ 默认站点固定；自定义站点只保存在本机。                                        完成 │
└──────────────────────────────────────────────────────────────────────────────┘
```

管理弹层：`Views/SiteManagerView.axaml`（九宫格默认站点 + 右侧表单）。

### 5.3 新增流程

1. 输入名称 + 地址；名称空/过长、host 非法、重复、超过 8 个 → 行内错误，不弹系统对话框。
2. 可选 `测试连通`：显示一行四段结果；失败显示"DNS 失败 / TLS 失败 / 超时"等原因。
3. 点 `保存站点`：
   - 结构合法即保存；未测试或测试失败标「未验证」，不阻止保存。
   - 保存后弹层保持打开，自定义列表出现新行；关闭后主表自动刷新，`自定义` 计数 +1。
4. 编辑：点自定义行的 `编辑`，表单回填；保存后替换原记录。
5. 删除：行内 `删除` → 二次确认 → 从 `sites.json` 移除；主表同步。

### 5.4 持久化

- 路径：`%LOCALAPPDATA%\AirNet\sites.json`（与 `settings.json`、`history.jsonl` 同目录）；
  `sites.jsonl` 保存每轮站点结果。
- 源生成 JSON，裁剪安全；结构：

```json
{
  "sites": [
    {
      "id": "a8f6…",
      "name": "公司官网",
      "host": "status.example.com",
      "addedAt": "2026-11-12T11:20:14+08:00"
    }
  ]
}
```

- 读取失败/损坏 → 回退为空列表，不阻塞默认 11 站；保存失败 → 行内提示，不崩溃。
- `AIRNET_DATA_DIR` 优先，截图/CI 用隔离目录。

---

## 6. 历史、报告与设置

- **历史**：每次站点测试追加一行到 `sites.jsonl`（30 天 / 500 条），重启后仍能读到上一轮，
  "较上次"跨会话保留；趋势图放 M3。不混入现有 `history.jsonl`。
- **HTTP 状态**：解析响应首行的状态码；403/429/451 标"HTTP 拦截"（琥珀），5xx 标"HTTP 5xx"
  （红色），不把它们误判为网络故障；行内仍保留四段耗时。
- **报告**：`ReportBuilder` 增加可选 `SiteLatencyReport`，在 HTML 报告里加一张"站点延迟"表；
  不传则报告保持现状。UI 用当前一轮，`--report` 自动读取 `sites.jsonl` 最近一轮；
  自定义站点名称做 HTML encode，HTTP 问题在分段列追加状态说明。
- **设置**：首版不增加开关；站点清单、采样次数（3）、超时（4s）作为常量。
  M3 再加"是否包含国际站点/默认站点隐藏"。
- **隐私**：只发匿名 GET，不带 Cookie/Referer/登录态；不保存页面内容；结果只落本地。
  自定义站点由用户主动录入，文案提示"只填你信任的站点"。

---

## 7. 视觉设计

### 7.1 主页面

视觉稿：[site-latency-mockup.png](design/site-latency-mockup.png)。

```
品牌条 / 状态条（复用 ShellView）
侧栏：总览 · 实时检测 · 带宽测速 · 网站延迟（选中）· 链路路径 · 设置
内容区：
  ┌ 页面头：网站延迟 / 11 个站点（默认 11 + 自定义 0）     [全部11|国内7|国际4|自定义0] [管理站点] [重新测试]
  ├ 汇总条：最快 28ms ｜ 最慢 342ms ｜ 平均 148ms ｜ 完成 11/11
  └ ┌ 站点延迟（主卡）                              ┐ ┌ 本次判断 ┐
    │ 站点   总耗时   分段条  较上次  状态            │ │ 口径     │
    │ 百度   28ms    ▓▓▓▓    ↓4     ●               │ │ 站点清单 │
    │ ... 11 行（每行 40px）                        │ └──────────┘
    └ 默认 11 + 自定义 0 · 不并入健康分              ┘
状态栏：本机实测 · 11/11 · 平均 148ms
```

### 7.2 关键组件

| 组件 | 规格 |
| --- | --- |
| 汇总条 | 4 列：最快 / 最慢 / 平均 / 完成；值 24px Semibold，脚注 9.5px |
| 站点行 | 40px；28px 字徽 + 名称/host；总耗时等宽；分段条 6px；差值 9.5px；状态点 7px |
| 分段条 | 四段同色系透明度：DNS 1.0 / TCP 0.78 / TLS 0.56 / 等待 0.32；宽度按耗时占比，最小 1px |
| 状态点 | 青绿/琥珀/陶土三档；**同时有文字分档**，不只靠颜色 |
| 管理站点弹层 | 900×660；左侧默认/自定义列表，右侧新增表单；背景压暗，焦点锁定 |
| 空态 | 表格内"还没有站点数据" + "开始测试"；自定义列表"还没有自定义站点" |
| 加载态 | 骨架行 + `ScanSweep` + "正在测试 3/11 · 2.4s"；结果逐行落位 |
| 失败态 | 行内"DNS 失败/超时/被拦截"，不写 0；汇总单独计失败数 |
| 分组 | `全部 / 国内 / 国际 / 自定义`；只过滤显示，不重测；自定义不混入国内/国际计数 |

### 7.3 动效

- 测试中：复用 `ScanSweep` 扫描带 + 脉冲点 + 200ms 计时。
- 结果落位：每行 180ms 淡入 + 分段条从 0 长到目标宽度，行间 40ms 错峰。
- 数字：总耗时 300ms 滚动；差值箭头 160ms 淡入。
- 弹层：背景 160ms 压暗，卡片 180ms `CubicEaseOut` 上浮 8px；关闭反向。
- `Features.Motion.Enabled=false`（截图导出/CI）时全部关闭。

### 7.4 视觉约束

- 只使用现有 token：`AppBackgroundBrush` / `SurfaceBrush` / `AccentBrush` / `WarnBrush` / `BadBrush`。
- 中文最小 10px；11 行 × 40px，保证 860px 窗口内不滚动。
- 不新增图表库；分段条用现有自绘控件的堆叠变体。

---

## 8. 技术实现

### 8.1 Core

```text
src/AirNet.Core/
  Sites/
    SiteTarget.cs          Id / Name / Host / Group / IsCustom
    SiteCatalog.cs         11 个默认站点 + 合并自定义
    CustomSiteStore.cs     sites.json 读写（源生成 JSON）
    SiteHistoryStore.cs    sites.jsonl 追加/读取/裁剪
  Probes/
    SiteLatencyProbe.cs    并发 4、每站 3 次、中位数、失败分类
  Diagnostics/
    SiteLatencyReport.cs   Results / StartedAt / DurationMs
```

- **复用现成四段计时**：`ApplicationTimingProbe.MeasureAsync(host, 443, timeoutMs, ct)`
  已经按 DNS→TCP→TLS→首字节顺序计时，且带 `FailureNote`。首版不重写 socket 逻辑。
- **并发 4**：`SemaphoreSlim(4)`；每站 3 次顺序采样，站点之间并发。
- **中位数**：按 `TotalMs` 排序取中间样本，直接使用该样本的四段值，保证"四段相加 = 总耗时"。
- **取消**：每个请求带 `CancellationToken`；取消后把未完成站点标 `Cancelled`。
- **失败分类**：把 `LayerTiming.FailureNote` 映射为 `Dns/Tcp/Tls/Ttfb/Timeout`；不抛异常到 UI。
- **不自带评分**：`SiteLatencyProbe` 只产出原始数据；分档/文案在 ViewModel 或 `SiteDiagnosis` 中做。
- **CLI**：`--sites <out.txt>` 已实现，输出每个站点的状态、四段耗时、HTTP 状态和失败原因；
  同时把本轮写入 `sites.jsonl`，供随后 `--report` 使用。

### 8.2 App

```
ViewModels/SiteLatencyViewModel.cs     页面 + 管理弹层状态
Views/SiteLatencyView.axaml(.cs)       页面 + 弹层 Overlay
Views/SiteManagerView.axaml(.cs)       管理弹层（可拆）
```

- `SiteLatencyViewModel`：
  - `IsTesting`、`TestStatusText`、`ProgressText`、`Results`、`Summary`、`FailureCount`。
  - `RunTestCommand` / `StopTestCommand`；`CancellationTokenSource`。
  - `IsManagerOpen`、`CustomSites`、`Draft`（Name/Host/Validation/TestResult）。
  - `OpenManager`、`CloseManager`、`TestDraft`、`SaveDraft`、`EditCustom`、`DeleteCustom`。
  - 用 `IProgress<SiteResult>` 逐行落位；`ScanSweep IsActive="{Binding IsTesting}"`。
  - 复用 `SpeedViewModel` 的 200ms timer 写法。
- `ShellViewModel`：
  - 增加 `Sites` 页面 VM、`Navigate("Sites")`、`PageIndex` 映射、`IsSitesSelected`。
  - 导航快捷键 Ctrl+1–6（六页）。
- 主页面按 §7 布局；行模板绑定 `SiteRow`（展示投影），不直接把 Core record 绑到 XAML。
- 弹层用应用内 Overlay，不做新 Window；`Esc` 关闭，焦点圈定在弹层内。
- 分段条新建 `StackedBar` 自绘控件，参数 `Segments` + 四档透明度；或叠加 `MeterBar`。
- 共享：`SnapshotView` 旁新增 `SiteLatencyView` 投影；XAML 不写 converter；`IconGlobe` 复用。

### 8.3 数据流

```
SiteLatencyView [开始测试]
  → SiteLatencyViewModel.RunTestAsync
    → SiteCatalog (11 + custom)
    → SiteLatencyProbe.RunAsync(catalog, progress, ct)
      → ApplicationTimingProbe ×3/站点，并发 4
    → IProgress<SiteResult> → 行落位
    → 完成：汇总 + LastReport
  → 报告导出可选读取 LastReport
```

### 8.4 测试

| 层级 | 用例 |
| --- | --- |
| 单元 | 中位数（奇/偶/失败样本）、失败分类、取消、目录唯一性、11 个默认站点 |
| 单元 | `CustomSiteStore`：正常化、重复、上限 8、损坏 JSON、`AIRNET_DATA_DIR` |
| 集成 | `--sites` 真跑 11 站；断网/DNS 失败时结构正确；总耗时上限 |
| 视觉 | `--export` 第 6 页；主页面/管理弹层/加载/失败各一张 |
| 手工 | 国内正常、Google/YouTube/X 慢、DNS 故障、只开 IPv6、代理/VPN、自定义站点增删改 |

### 8.5 性能预算

- 11 站 × 3 次 = 33 个请求，并发 4：正常网络 **6–12s**；最坏 20s 上限。
- 单站超时 4s；取消后 ≤1 个请求周期退出。
- 不新增常驻内存；结果 11–19 条，趋势页后才引入文件。

---

## 9. 风险与取舍

| 风险 | 处理 |
| --- | --- |
| 站点返回 403/验证页 | 仍记录 TTFB，但标"HTTP 拦截"；不判定为网络故障 |
| Google/YouTube/X 被拦截 | 行内标"不可达/被拦截"，不把国际组慢归因本机 |
| CDN/Anycast 波动 | 3 次取中位数；只和同站上次比，不跨站比 |
| DNS 缓存让第一次/后面不一致 | 记录"冷/热"备注；中位数优先 |
| 自定义站点填内网/错误地址 | 只允许 HTTPS/443；明确提示"只填信任站点"；失败不崩溃 |
| 自定义站点过多 / 重复 | 上限 8；host 去重；行内错误 |
| 站点改版/下线 | 默认清单集中维护；失败行保留，不阻塞其他站点 |
| HTTP/1.1 与 HTTP/2 差异 | 首版接受；只比较同一探针历史，不承诺绝对值 |
| 页面首字节 ≠ 页面可用 | 文案写"首字节延迟"，不写"网页加载完成" |

---

## 10. 里程碑与估时

| 阶段 | 内容 | 估时 |
| --- | --- | --- |
| Phase 1 Core | `SiteCatalog` + `SiteLatencyProbe` + 中位数/失败/取消 + 单元测试 | 1.5 天 |
| Phase 2 主页面 | 第六页 + 汇总/表格/分组 + 空/加载/失败态 + 动效 | 2 天 |
| Phase 3 自定义站点 | `CustomSiteStore` + 管理弹层 + 校验/编辑/删除 + 持久化 | 1.5 天 |
| Phase 4 联调 | Shell 导航/快捷键、报告表格、`--sites` 烟测 | 1 天 |
| Phase 5 校准 | 11 站真跑、阈值/文案、截图、文档 | 1 天 |

**总计约 7 个工作日**；先做 Phase 1+2 可评审主页面，Phase 3 再补自定义站点。

---

## 11. 已确认的决策（2026-11）

1. ✅ 自定义站点固定归入「自定义」分组，不混进国内/国际计数。
2. ✅ 自定义站点上限 **8 个**，只支持 **HTTPS 443**。
3. ✅ 测试失败时**允许保存为未验证**。
4. ✅ 默认 11 站固定不可删；M3 再支持隐藏。
5. ✅ 分档阈值继续用 80/200ms，仅展示，不评分。
6. ✅ 坚持"站点延迟不并入健康分"。
