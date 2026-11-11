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
    "  · 链路路径 → 开始追踪：系统 traceroute，约 10-20 秒，最多 16 跳。",
    "  · 带宽测速：单流 12MB，按需触发。",
    "  · 导出报告：生成单文件 HTML，浏览器可直接打开或打印成 PDF。",
    "",
    "会改系统的地方（仅一处）",
    "  · 设置 → DNS 优化：需要 UAC。执行前显示完整命令，应用后记录还原点，",
    "    随时可「还原」。解析不慢时它不会建议你改。",
    "",
    "数据位置",
    "  %LOCALAPPDATA%\AirNet\  （history.jsonl 历史、dns-backup.json 还原点）",
    "  删掉该目录即清除全部本地数据。",
    "",
    "已知限制",
    "  · Wi-Fi 信号采样未在真实无线网卡上验证过（开发机是有线）。",
    "  · 抖动取自 TCP 握手，比 ICMP 高 3-5ms 属正常。",
    "  · 逐跳依赖系统 traceroute，部分企业网络会拦截。"
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
