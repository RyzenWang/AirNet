# AirNet 图标方向（待选）

当前标题栏仍用 AirPDF 的纸飞机 `IconPlane`，不符合"网络诊断"的语义。下面五个方向都不再复用纸飞机，
全部是 512×512、透明背景、青绿底 + 纸白线；`contact-sheet.png` 里附了 32px 实际尺寸预览。

| 文件 | 方向 | 适合的理由 |
| --- | --- | --- |
| `01-pulse.png` | 脉冲 / Pulse | 像延迟曲线的心跳，和"测量"直接相关 |
| `02-mesh.png` | 节点 / Mesh | 三个节点，适合表达"局域网 / 运营商 / 对端" |
| `03-signal.png` | 信号 / Signal | Wi-Fi 弧线，最直观；但同类产品也常用 |
| `04-route.png` | 路径 / Route | 对应逐跳、traceroute 和"哪一段坏了" |
| `05-mark.png` | 字母 / A | 品牌首字母 + 网络节点，识别度最高 |

选定后需要做两件事：

1. 把该图形的 24×24 路径写进 `src/AirNet.App/Styles/Icons.axaml`，替换标题栏的 `IconPlane`；
2. 生成 Windows `.ico` / macOS `.icns` 并接到 `MainWindow.Icon` 与发布脚本。

> 这些是概念稿，不是最终矢量稿；选定方向后会按 16/24/32/48/256 像素重新对齐笔画。
