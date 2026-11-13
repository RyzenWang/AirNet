# Native AOT

Windows x64 正式发布包使用 Native AOT；macOS / Linux 仍使用
`PublishSingleFile + PublishTrimmed + ReadyToRun`，等原生 runner 上的 AOT 验证完成后再切换。

Windows 正式资产名：`AirNet-<version>-win-x64-aot.zip`。它不再与旧的
`win-x64-portable.zip` 混用；旧 R2R 包只保留为本地回退和对照。

## 构建

在 Windows 上需要 .NET 10 SDK 和 MSVC linker / Visual Studio Build Tools：

```powershell
powershell -ExecutionPolicy Bypass -File packaging/pack-aot.ps1
```

输出：`artifacts/packages/AirNet-<version>-win-x64-aot.zip`

AOT 不能用 `PublishSingleFile`，包内是：

- `AirNet.exe`
- `libSkiaSharp.dll`
- `av_libglesv2.dll`
- `libHarfBuzzSharp.dll`
- `使用说明.txt`

PDB 在打包时移除。移动时必须移动整个文件夹，不能只拿 `AirNet.exe`。

## 2026-11-13 本机验证（Windows x64）

| 指标 | R2R 单文件 | Native AOT |
| --- | --- | --- |
| 压缩包 | 26.74 MB | **16.24 MB** |
| exe | 25.5 MB | 25.7 MB |
| 启动到窗口 | 约 1.3–1.9 s | **411 ms** |
| 工作集 | 约 170–176 MB | **118.9 MB** |
| `--probe --json` | ✅ | ✅ 退出码 0 |
| `--selftest` | ✅ | ✅ 历史/双栈/逐跳正常 |
| `--export` | ✅ 12 张 | ✅ 12 张 |
| GUI | ✅ | ✅ |

## 限制

- 只验证了 **Windows x64**；Linux 需要 clang/zlib，macOS 需要 Xcode 工具链，尚未实测。
  这些平台的发布资产暂时继续使用 R2R。
- AOT 没有 JIT，崩溃栈、动态加载和部分反射行为与普通版不同。
- Native PDB 很大（约 83 MB），发布包已剥离；需要符号时单独归档。
- 本次只切换了构建/打包路径，没有创建新 tag 或 Release；发版前仍需确认版本号和发布说明。
