# Builds portable macOS and Linux assets. With -Aot the build must run on the
# target OS because Native AOT needs that platform's native toolchain.
#
#   pwsh packaging/pack-cross.ps1
#   pwsh packaging/pack-cross.ps1 -Runtimes linux-x64 -Aot
#   pwsh packaging/pack-cross.ps1 -Runtimes osx-arm64 -Aot
#
# Output (root of each archive):
#   macOS: AirNet.app
#   Linux: AirNet/ (binary + native libraries + readme + .desktop)
#
# The executable bit is written into the zip's Unix external attributes. Use unzip
# (Info-ZIP) or macOS Archive Utility so the permissions are restored.

param(
    [string[]]$Runtimes = @("linux-x64", "linux-arm64", "osx-x64", "osx-arm64"),
    [switch]$Aot,
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = Split-Path -Parent $PSScriptRoot
$csproj = Get-Content (Join-Path $root "src/AirNet.App/AirNet.App.csproj") -Raw
if ($csproj -notmatch '<Version>([^<]+)</Version>') {
    throw "cannot read <Version> from AirNet.App.csproj"
}
$version = $Matches[1]
if (-not $Output) {
    $Output = Join-Path $root "artifacts/packages"
}
New-Item -ItemType Directory -Force $Output | Out-Null

function New-PortableZip {
    param(
        [string]$Stage,
        [string]$ZipPath,
        [string]$ExecutableRelativePath
    )

    Remove-Item $ZipPath -Force -ErrorAction SilentlyContinue
    $archive = [System.IO.Compression.ZipFile]::Open($ZipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem $Stage -Recurse -File) {
            $relative = $file.FullName.Substring($Stage.Length).TrimStart('\', '/').Replace('\', '/')
            $entry = $archive.CreateEntry($relative, [System.IO.Compression.CompressionLevel]::Optimal)
            # 0o100755 / 0o100644 in the high 16 bits. The low bits are DOS attributes.
            $isExecutable = $relative -eq $ExecutableRelativePath -or $relative.EndsWith('.sh')
            $isNativeLibrary = $relative.EndsWith('.so') -or $relative.Contains('.so.') -or $relative.EndsWith('.dylib')
            $mode = if ($isExecutable -or $isNativeLibrary) { 0x81ED } else { 0x81A4 }
            $entry.ExternalAttributes = $mode -shl 16
            $entry.LastWriteTime = $file.LastWriteTime
            $sourceStream = [System.IO.File]::OpenRead($file.FullName)
            try {
                $targetStream = $entry.Open()
                try {
                    $sourceStream.CopyTo($targetStream)
                }
                finally {
                    $targetStream.Dispose()
                }
            }
            finally {
                $sourceStream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

$buildMode = if ($Aot) { "Native AOT" } else { "R2R 单文件" }
$macReadme = @(
    "AirNet $version · macOS 免安装版",
    "构建：$buildMode",
    "====================================",
    "",
    "用法",
    "  1. 解压得到 AirNet.app",
    "  2. 拖到「应用程序」或任意目录，双击启动",
    "",
    "未签名提示",
    "  · 首次打开若提示「无法验证开发者」，右键 AirNet.app → 打开。",
    "  · 或在终端执行：xattr -dr com.apple.quarantine AirNet.app",
    "",
    "数据位置",
    "  ~/Library/Application Support/AirNet/",
    "  删除该目录即清除历史、设置和自定义站点。",
    "",
    "已知限制",
    "  · Wi-Fi 信号/速率是 Windows 专属实现；macOS 上会降级为接口信息。",
    "  · 逐跳依赖系统 traceroute；部分网络会过滤。",
    "  · 未做 Apple 签名与公证，发布包仅供本机验证。"
) -join "`n"

$linuxReadme = @(
    "AirNet $version · Linux 免安装版",
    "构建：$buildMode",
    "====================================",
    "",
    "用法",
    "  1. 解压后进入 AirNet 目录",
    "  2. 直接执行 bash run.sh（zip/tar 都适用，不依赖解压后的执行位）",
    "  3. 也可以 chmod +x AirNet && ./AirNet",
    "",
    "依赖",
    "  · X11 或 Wayland（Avalonia 通过 X11/XWayland 运行）",
    "  · libfontconfig1、libX11、libICE、libSM 等常见桌面库",
    "  · 不需要安装 .NET；运行时已自带",
    "",
    "数据位置",
    "  ~/.local/share/AirNet/",
    "  删除该目录即清除历史、设置和自定义站点。",
    "",
    "已知限制",
    "  · Wi-Fi 信号/速率是 Windows 专属实现；Linux 上会降级为接口信息。",
    "  · 逐跳依赖系统 traceroute；部分网络会过滤。",
    "  · Linux x64 已在真实环境启动验证；Linux arm64 尚待验证。",
    "  · 未做发行版打包，发布包仅供本机验证。"
) -join "`n"

foreach ($runtime in $Runtimes) {
    $isMacRuntime = $runtime -like 'osx-*'
    $isLinuxRuntime = $runtime -like 'linux-*'
    if (-not ($isMacRuntime -or $isLinuxRuntime)) {
        throw "unsupported runtime: $runtime (this script handles osx-* and linux-*)"
    }

    $publishDir = Join-Path $root "artifacts/publish/$runtime"
    $stageRoot = Join-Path $root "artifacts/stage-cross/$runtime"
    $suffix = if ($Aot) { "-aot" } else { "" }
    Remove-Item -Recurse -Force $stageRoot -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $stageRoot | Out-Null

    Write-Host "publishing $runtime $(if ($Aot) { '(Native AOT)' } else { '(R2R)' })" -ForegroundColor Cyan
    $publishArgs = @{
        Runtime = $runtime
        Output = $publishDir
        Aot = $Aot
    }
    if (-not $Aot) {
        $publishArgs["NoReadyToRun"] = $true
    }
    & (Join-Path $PSScriptRoot "publish.ps1") @publishArgs

    if ($isMacRuntime) {
        $app = Join-Path $stageRoot "AirNet.app"
        $macOsDir = Join-Path $app "Contents/MacOS"
        $resourcesDir = Join-Path $app "Contents/Resources"
        New-Item -ItemType Directory -Force $macOsDir, $resourcesDir | Out-Null
        Copy-Item "$publishDir/*" $macOsDir -Recurse
        $icon = Join-Path $PSScriptRoot "assets/AirNet.icns"
        if (-not (Test-Path $icon)) {
            throw "missing macOS icon: $icon"
        }
        Copy-Item $icon (Join-Path $resourcesDir "AirNet.icns")
        Set-Content -Path (Join-Path $app "Contents/PkgInfo") -Value "APPL????" -NoNewline -Encoding ascii

        $plist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>AirNet</string>
  <key>CFBundleDisplayName</key><string>AirNet</string>
  <key>CFBundleIdentifier</key><string>io.airnet.app</string>
  <key>CFBundleVersion</key><string>$version</string>
  <key>CFBundleShortVersionString</key><string>$version</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleExecutable</key><string>AirNet</string>
  <key>CFBundleIconFile</key><string>AirNet.icns</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>LSApplicationCategoryType</key><string>public.app-category.utilities</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
"@
        Set-Content -Path (Join-Path $app "Contents/Info.plist") -Value $plist -Encoding utf8
        Set-Content -Path (Join-Path $app "Contents/Resources/使用说明.txt") -Value $macReadme -Encoding utf8

        $asset = Join-Path $Output "AirNet-$version-macos-$($runtime.Substring(4))$suffix.zip"
        New-PortableZip -Stage $stageRoot -ZipPath $asset -ExecutableRelativePath "AirNet.app/Contents/MacOS/AirNet"
    }
    else {
        $appDir = Join-Path $stageRoot "AirNet"
        New-Item -ItemType Directory -Force $appDir | Out-Null
        Copy-Item "$publishDir/*" $appDir -Recurse
        Set-Content -Path (Join-Path $appDir "README.txt") -Value $linuxReadme -Encoding utf8
        $desktop = @"
[Desktop Entry]
Type=Application
Name=AirNet
Comment=Network diagnostics
Exec=AirNet
Terminal=false
Categories=Network;Utility;
"@
        Set-Content -Path (Join-Path $appDir "AirNet.desktop") -Value $desktop -Encoding utf8

        # This wrapper also repairs the executable bit when a Windows unzip tool
        # strips Unix permissions. Running `bash run.sh` works even without +x.
        $runScript = @'
#!/usr/bin/env bash
set -e
cd "$(dirname "$0")"
chmod +x AirNet 2>/dev/null || true
exec ./AirNet "$@"
'@
        [System.IO.File]::WriteAllText(
            (Join-Path $appDir "run.sh"),
            $runScript.Replace("`r`n", "`n"),
            (New-Object System.Text.UTF8Encoding($false)))

        $asset = Join-Path $Output "AirNet-$version-linux-$($runtime.Substring(6))$suffix.zip"
        New-PortableZip -Stage $stageRoot -ZipPath $asset -ExecutableRelativePath "AirNet/AirNet"
        $tarAsset = Join-Path $Output "AirNet-$version-linux-$($runtime.Substring(6))$suffix.tar.gz"
        Remove-Item $tarAsset -Force -ErrorAction SilentlyContinue
        & tar -czf $tarAsset -C $stageRoot "AirNet"
        if ($LASTEXITCODE -ne 0) {
            throw "tar failed for $tarAsset"
        }
    }

    $assets = @($asset)
    if ($isLinuxRuntime) { $assets += $tarAsset }
    foreach ($item in $assets) {
        $info = Get-Item $item
        Write-Host ("asset   : {0} ({1:N2} MB)" -f $item, ($info.Length / 1MB)) -ForegroundColor Green
        Write-Host ("sha256  : {0}" -f (Get-FileHash $item -Algorithm SHA256).Hash)
    }
}
