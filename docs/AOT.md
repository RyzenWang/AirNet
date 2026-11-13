# Native AOT

正式发布包全部使用 Native AOT，由 GitHub Actions 的原生 runner 分别构建：

| 资产 | Runner | 工具链 |
| --- | --- | --- |
| Windows x64 | `windows-latest` | MSVC / VS Build Tools |
| Linux x64 | `ubuntu-latest` | clang + zlib |
| Linux arm64 | `ubuntu-24.04-arm` | clang + zlib |
| macOS x64 | `macos-15-intel` | Xcode |
| macOS arm64 | `macos-15` | Xcode |

资产名统一带 `-aot` 后缀，例如 `AirNet-<version>-win-x64-aot.zip`、
`AirNet-<version>-linux-x64-aot.tar.gz`。旧 R2R 单文件包只保留为本地回退/对照。

Windows 的 AOT 构建和运行已验证；macOS/Linux 的 AOT 构建需要等 CI 原生 runner 结果。

## 构建

Windows：

```powershell
powershell -ExecutionPolicy Bypass -File packaging/pack-aot.ps1
```

Linux / macOS（在目标 OS 上）：

```bash
pwsh packaging/pack-cross.ps1 -Runtimes linux-x64 -Aot
pwsh packaging/pack-cross.ps1 -Runtimes osx-arm64 -Aot
```

AOT 不能用 `PublishSingleFile`，Windows 包内是：

- `AirNet.exe`
- `libSkiaSharp.dll`
- `av_libglesv2.dll`
- `libHarfBuzzSharp.dll`
- `使用说明.txt`

PDB 在打包时移除。移动时必须移动整个文件夹，不能只拿 `AirNet.exe`。
macOS 仍然是 `AirNet.app`，Linux 仍然是 `AirNet/` + `run.sh`。

## 2026-11-13 AOT 构建结果

Windows 本机运行验证：

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

CI 原生 runner 全平台 AOT 构建（workflow dispatch，未发 Release）：

| 资产 | 大小 | 构建 |
| --- | --- | --- |
| `win-x64-aot.zip` | 16.83 MB | ✅ |
| `linux-x64-aot.zip` / `.tar.gz` | 16.57 / 15.96 MB | ✅ |
| `linux-arm64-aot.zip` / `.tar.gz` | 15.58 / 14.88 MB | ✅ |
| `macos-x64-aot.zip` | 28.50 MB | ✅ |
| `macos-arm64-aot.zip` | 26.73 MB | ✅ |

Linux/macOS 目前只验证了 AOT **构建与归档**；GUI/CLI 运行还需要在真实桌面环境验收。

## 限制

- Windows x64 已本机验证；Linux/macOS 已通过 CI 原生 runner 的 AOT 构建，运行仍待真机验收。
- AOT 没有 JIT，崩溃栈、动态加载和部分反射行为与普通版不同。
- Native PDB 很大（约 83 MB），发布包已剥离；需要符号时单独归档。
- 本次只切换了构建/打包路径，没有创建新 tag 或 Release；发版前仍需确认版本号和发布说明。
