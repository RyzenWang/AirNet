# Publishes AirNet as a trimmed, ReadyToRun, single-file Windows build and reports
# the numbers the README claims. Run from the repo root:
#
#   pwsh packaging/publish.ps1                 # win-x64, R2R
#   pwsh packaging/publish.ps1 -Runtime linux-x64
#   pwsh packaging/publish.ps1 -NoReadyToRun   # smaller, but cold start roughly doubles

param(
    [string]$Runtime = "win-x64",
    [switch]$NoReadyToRun,
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $Output) {
    $Output = Join-Path $root "artifacts/publish/$Runtime"
}

$r2r = if ($NoReadyToRun) { "false" } else { "true" }

# An incremental publish can leave native libraries behind between RID switches.
# Start clean and keep them external so every archive has the same layout.
Remove-Item -Recurse -Force $Output -ErrorAction SilentlyContinue
Write-Host "publishing $Runtime (ReadyToRun=$r2r) -> $Output" -ForegroundColor Cyan
dotnet publish (Join-Path $root "src/AirNet.App/AirNet.App.csproj") `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=true `
    -p:TrimMode=partial `
    -p:PublishReadyToRun=$r2r `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=false `
    -o $Output `
    --nologo

$files = Get-ChildItem $Output -File
# Symbols are stripped from the shipping folder; keep them in artifacts if a crash needs a stack.
Get-ChildItem $Output -Filter *.pdb -File | Remove-Item -Force
$files = Get-ChildItem $Output -File
$totalMb = [math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host ""
$files | Sort-Object Length -Descending |
    Select-Object Name, @{Name = "MB"; Expression = { [math]::Round($_.Length / 1MB, 1) } } |
    Format-Table -AutoSize
Write-Host "total on disk: $totalMb MB" -ForegroundColor Green

$exe = Join-Path $Output "AirNet.exe"
$onWindows = [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT
if ($onWindows -and (Test-Path $exe)) {
    # Cold start to first painted window, measured the same way the README quotes it.
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $exe -PassThru
    $shown = 0
    for ($i = 0; $i -lt 500; $i++) {
        Start-Sleep -Milliseconds 20
        $running = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
        if ($running -and $running.MainWindowHandle -ne 0) {
            $shown = $watch.ElapsedMilliseconds
            break
        }
    }

    Start-Sleep -Seconds 2
    $running = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "startup to window: $shown ms"
        Write-Host "working set: $([math]::Round($running.WorkingSet64 / 1MB, 1)) MB"
        $running | Stop-Process -Force
    }
}
