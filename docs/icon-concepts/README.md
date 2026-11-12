# AirNet 图标方向

**已选：01 脉冲 / Pulse。** 标题栏、首页纸叠、窗口 `.ico` 和 mac 的 `.icns` 都是这个标记；其余四个方向保留作备选。

## 底色由生成脚本决定

图标底色**不是手绘的**，由 `make-icon.py` 按品牌色重新生成：

```powershell
# 需要 Pillow；改品牌色时跑一次，然后提交生成的 .ico / .icns
python docs/icon-concepts/make-icon.py
python docs/icon-concepts/make-icon.py --brand E3563B   # 指定底色
```

| 产物 | 用途 |
| --- | --- |
| `src/AirNet.App/Assets/AirNet.ico` | 窗口图标 + exe 的 `ApplicationIcon`（16/24/32/48/64/128/256） |
| `packaging/assets/AirNet.icns` | macOS 包（`CFBundleIconFile`） |

为什么要有脚本：v0.2.3 把品牌色从青绿换成暖橙 `#E3563B` 时，只改了 XAML 里的 `AccentBrush`，
`.ico` 仍是旧的 `#2E6F63`，于是任务栏和资源管理器里挂着绿色图标。形状与颜色现在分开管理，
下次换色不会再漏。

脚本只重上色、不重画：从 `01-pulse.png` 提取字形覆盖率（按亮度反解），再用品牌色重新合成，
所以抗锯齿边缘不会出现描边。结尾那个琥珀色圆点会统一改成纸白，因为暖橙底上暖黄点对比度不够。

## 五个方向

都曾是 512×512、透明背景、青绿底 + 纸白线的探索稿；`contact-sheet.png` 里附了 32px 实际尺寸预览。
其中 `01-pulse.png` 已由脚本更新为当前品牌色，其余四张保留为青绿时代的备选。

| 文件 | 方向 | 适合的理由 |
| --- | --- | --- |
| `01-pulse.png` | 脉冲 / Pulse | 像延迟曲线的心跳，和"测量"直接相关 |
| `02-mesh.png` | 节点 / Mesh | 三个节点，适合表达"局域网 / 运营商 / 对端" |
| `03-signal.png` | 信号 / Signal | Wi-Fi 弧线，最直观；但同类产品也常用 |
| `04-route.png` | 路径 / Route | 对应逐跳、traceroute 和"哪一段坏了" |
| `05-mark.png` | 字母 / A | 品牌首字母 + 网络节点，识别度最高 |

## 已经落地

1. `Styles/Icons.axaml` 里的 24×24 `IconPulse`，标题栏和首页纸叠都用它，颜色跟随 `AccentBrush`；
2. `.ico` / `.icns` 由脚本生成，与 `AccentBrush` 同色；
3. `MainWindow.Icon` 与 `AirNet.App.csproj` 的 `ApplicationIcon` 都指向 `.ico`。
