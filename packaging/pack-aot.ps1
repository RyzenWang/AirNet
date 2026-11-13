# Builds the official Windows Native AOT portable package.
#
#   powershell -ExecutionPolicy Bypass -File packaging/pack-aot.ps1
#
# Output: artifacts/packages/AirNet-<version>-win-x64-aot.zip
#
# Native AOT cannot be combined with PublishSingleFile. The zip therefore contains
# AirNet.exe plus its native Skia/ANGLE/HarfBuzz DLLs. PDBs are stripped because the
# native PDB is much larger than the executable and is not needed by end users.
#
# Cross-platform AOT is not validated yet: Linux needs clang/zlib and macOS needs the
# Xcode toolchain. Those platforms stay on the existing R2R build for now.

param(
    [string]$Runtime = "win-x64",
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csproj = Get-Content (Join-Path $root "src/AirNet.App/AirNet.App.csproj") -Raw
if ($csproj -notmatch '<Version>([^<]+)</Version>') {
    throw "cannot read <Version> from AirNet.App.csproj"
}
$version = $Matches[1]

$publishDir = Join-Path $root "artifacts/publish/$Runtime-aot"
$stageDir = Join-Path $root "artifacts/stage/AirNet-$version-aot"
if (-not $Output) {
    $Output = Join-Path $root "artifacts/packages"
}

Write-Host "packing AirNet $version ($Runtime, Native AOT)" -ForegroundColor Cyan
Remove-Item -Recurse -Force $publishDir, $stageDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $publishDir | Out-Null

dotnet publish (Join-Path $root "src/AirNet.App/AirNet.App.csproj") `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishAot=true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=true `
    -p:PublishReadyToRun=false `
    -o $publishDir `
    --nologo

# Native debug symbols are huge and have no value in a portable user package.
# Linux emits AirNet.dbg; Windows emits a PDB.
Get-ChildItem $publishDir -File | Where-Object { $_.Extension -in '.pdb', '.dbg' } | Remove-Item -Force
$files = Get-ChildItem $publishDir -File
if ($files.Count -eq 0) {
    throw "AOT publish produced no files"
}

New-Item -ItemType Directory -Force $stageDir | Out-Null
Copy-Item "$publishDir/*" $stageDir -Recurse

$readmeLines = @(
    "AirNet $version · Native AOT 便携版",
    "====================================",
    "",
    "用法",
    "  1. 解压到任意目录（不要在压缩包里直接运行）",
    "  2. 双击 AirNet.exe",
    "",
    "这是 Windows 正式 Native AOT 包，不是单文件：",
    "  AirNet.exe 旁边必须保留 libSkiaSharp.dll、av_libglesv2.dll、",
    "  libHarfBuzzSharp.dll；移动时请移动整个文件夹。",
    "",
    "不需要安装 .NET，也不需要管理员权限。",
    "卸载 = 删掉这个文件夹。",
    "",
    "注意",
    "  · 未签名，Windows / 杀软可能拦截一次：更多信息 → 仍要运行。",
    "  · AOT 版启动更快、内存更低，但没有 JIT，崩溃栈与普通版不同。",
    "  · 邻区扫描仅 Windows 可用；有线电脑会跳过。",
    "  · 游戏/会议模式是独立深测，不进入 3 秒主扫描。",
    "",
    "命令行",
    "  AirNet.exe --sites result.txt     跑一轮网站延迟（无界面）",
    "  AirNet.exe --report report.html   跑一轮主检测并导出报告",
    "  AirNet.exe --selftest probe.txt   跑一轮主检测（含逐跳/MTU/双栈）",
    "  AirNet.exe --speedtest speed.txt  测上下行带宽",
    "  AirNet.exe --probe --json p.json  输出 JSON（schema v1），退出码 0/1/2/3",
    "",
    "数据位置",
    "  %LOCALAPPDATA%\AirNet\  （历史、站点历史、设置、DNS 还原点）"
) -join "`r`n"

[IO.File]::WriteAllText(
    (Join-Path $stageDir "使用说明.txt"),
    $readmeLines,
    (New-Object Text.UTF8Encoding($true)))

New-Item -ItemType Directory -Force $Output | Out-Null
$zip = Join-Path $Output "AirNet-$version-$Runtime-aot.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path "$stageDir/*" -DestinationPath $zip -CompressionLevel Optimal

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
$sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 2)
Write-Host ""
Write-Host "package : $zip"
Write-Host "size    : $sizeMb MB"
Write-Host "sha256  : $hash" -ForegroundColor Green
Write-Host "contents: $((Get-ChildItem $stageDir | Select-Object -ExpandProperty Name) -join ', ')"
