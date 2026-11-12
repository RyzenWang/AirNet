# AirNet 图标方向

**已选：01 脉冲 / Pulse。** 标题栏、首页纸叠和窗口 `.ico` 都已换成脉冲标记；其余四个方向保留作备选。
下面五个方向都不再复用 AirPDF 的纸飞机，全部是 512×512、透明背景、青绿底 + 纸白线；
`contact-sheet.png` 里附了 32px 实际尺寸预览。

| 文件 | 方向 | 适合的理由 |
| --- | --- | --- |
| `01-pulse.png` | 脉冲 / Pulse | 像延迟曲线的心跳，和"测量"直接相关 |
| `02-mesh.png` | 节点 / Mesh | 三个节点，适合表达"局域网 / 运营商 / 对端" |
| `03-signal.png` | 信号 / Signal | Wi-Fi 弧线，最直观；但同类产品也常用 |
| `04-route.png` | 路径 / Route | 对应逐跳、traceroute 和"哪一段坏了" |
| `05-mark.png` | 字母 / A | 品牌首字母 + 网络节点，识别度最高 |

已经落地：

1. `Styles/Icons.axaml` 新增 24×24 的 `IconPulse`，替换标题栏和首页纸叠里的 `IconPlane`；
2. `Assets/AirNet.ico` 由 512×512 生成，含 16/24/32/48/64/128/256 多尺寸；
3. `MainWindow.Icon` 与 `AirNet.App.csproj` 的 `ApplicationIcon` 都指向它。

> macOS 的 `.icns` 尚未生成；打 mac 包时按同一张 512 稿导出即可。
