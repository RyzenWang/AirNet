# Builds the local verification package: a no-install, portable zip.
#
#   powershell -File packaging/pack-portable.ps1
#   powershell -File packaging/pack-portable.ps1 -Runtime win-arm64
#
# Output: artifacts/packages/AirNet-<version>-win-x64-portable.zip
# The zip root contains AirNet.exe, so "extract and double-click" is the whole install.
#
# Installer builds (Velopack Setup.exe) are intentionally not part of this script; the
# release workflow handles those.
#
# IMPORTANT: this file must stay UTF-8 **with BOM**. It contains Chinese literals (the usage
# note written into the package) and Windows PowerShell 5.1 reads a BOM-less .ps1 as ANSI,
# which turns those literals into mojibake and fails with a misleading parser error about
# unterminated strings. If an editor strips the BOM, re-add it:
#   $c=[IO.File]::ReadAllText($p,[Text.Encoding]::UTF8)
#   [IO.File]::WriteAllText($p,$c,(New-Object Text.UTF8Encoding $true))

param(
    [string]$Runtime = "win-x64",
    [switch]$NoReadyToRun,
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$csproj = Get-Content (Join-Path $root "src/AirNet.App/AirNet.App.csproj") -Raw
if ($csproj -notmatch '<Version>([^<]+)</Version>') {
    throw "cannot read <Version> from AirNet.App.csproj"
}
$version = $Matches[1]

$publishDir = Join-Path $root "artifacts/publish/$Runtime-portable"
$stageDir = Join-Path $root "artifacts/stage/AirNet-$version"
if (-not $Output) {
    $Output = Join-Path $root "artifacts/packages"
}

Write-Host "packing AirNet $version ($Runtime, portable)" -ForegroundColor Cyan
Remove-Item -Recurse -Force $publishDir, $stageDir -ErrorAction SilentlyContinue

$publishArgs = @{ Runtime = $Runtime; Output = $publishDir }
if ($NoReadyToRun) { $publishArgs["NoReadyToRun"] = $true }
& (Join-Path $PSScriptRoot "publish.ps1") @publishArgs

New-Item -ItemType Directory -Force $stageDir | Out-Null
Copy-Item "$publishDir/*" $stageDir -Recurse

$readmeLines = @(
    "AirNet $version · 免安装便携版",
    "====================================",
    "",
    "用法",
    "  1. 解压到任意目录（不要在压缩包里直接运行）",
    "  2. 双击 AirNet.exe",
    "",
    "不需要安装 .NET，也不需要管理员权限。",
    "卸载 = 删掉这个文件夹。",
    "",
    "第一次打开",
    "  · 界面显示的是「示例数据」，点右上角「开始检测」才会跑真实测量。",
    "  · 未签名，Windows / 杀软可能拦截一次：更多信息 → 仍要运行。",
    "",
    "会用到网络的功能",
    "  · 开始检测：ICMP + TCP 握手 + DNS/TLS 首包，约 2-3 秒。",
    "  · 网站延迟：11 个默认站点 + 自定义站点，每站 3 次取中位数，识别 HTTP 拦截。",
    "  · 链路路径 → 开始追踪：系统 traceroute，约 10-20 秒，最多 16 跳。",
    "  · 带宽测速：单流 12MB，按需触发。",
    "  · 导出报告：生成单文件 HTML，浏览器可直接打开或打印成 PDF。",
    "",
    "命令行",
    "  AirNet.exe --sites result.txt     跑一轮网站延迟（无界面）",
    "  AirNet.exe --report report.html   跑一轮主检测并导出报告",
    "  AirNet.exe --selftest probe.txt   跑一轮主检测（含逐跳/MTU），把网卡与延迟写成文本",
    "",
    "核对探测结果（不用开界面）",
    "  probe.txt 里会列出来：interface / kind / ssid / signal / rssi / avg / p95 / jitter /",
    "  loss / mtu / culprit。Wi-Fi 下重点看 kind（是否为 Wi-Fi 5/6/7）、ssid、signal、rssi。",
    "",
    "会改系统的地方（仅一处）",
    "  · 设置 → DNS 优化：需要 UAC。执行前显示完整命令，应用后记录还原点，",
    "    随时可「还原」。解析不慢时它不会建议你改。",
    "",
    "数据位置",
    "  %LOCALAPPDATA%\AirNet\  （history.jsonl 历史、sites.jsonl 站点历史、settings.json 偏好、sites.json 自定义站点、dns-backup.json 还原点）",
    "  删掉该目录即清除全部本地数据。",
    "",
    "已知限制",
    "  · Wi-Fi 采样已在 Windows 真机验证（2026-11-12），但 dBm 是估算值（质量 ÷ 2 − 100），",
    "    真实 RSSI 与邻区/信道建议尚未实现；macOS 与 Linux 只显示「无线」，没有数值。",
    "  · 抖动取自 TCP 握手，比 ICMP 高 3-5ms 属正常。",
    "  · 逐跳依赖系统 traceroute，部分企业网络会拦截。",
    "  · 网站延迟只测 HTTPS 首字节；Google/YouTube/X 被拦截时会标不可达或 HTTP 拦截。"
) -join "`r`n"

$readmeLines | Out-File -FilePath (Join-Path $stageDir "使用说明.txt") -Encoding utf8

New-Item -ItemType Directory -Force $Output | Out-Null
$zip = Join-Path $Output "AirNet-$version-$Runtime-portable.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path "$stageDir/*" -DestinationPath $zip -CompressionLevel Optimal

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
$sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 2)

Write-Host ""
Write-Host "package : $zip"
Write-Host "size    : $sizeMb MB"
Write-Host "sha256  : $hash" -ForegroundColor Green
Write-Host "contents: $((Get-ChildItem $stageDir | Select-Object -ExpandProperty Name) -join ', ')"
