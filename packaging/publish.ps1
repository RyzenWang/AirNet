# Publishes AirNet as a trimmed, ReadyToRun, single-file build by default, or as
# Native AOT with -Aot. Run from the repo root:
#
#   pwsh packaging/publish.ps1                 # win-x64, R2R
#   pwsh packaging/publish.ps1 -Runtime linux-x64
#   pwsh packaging/publish.ps1 -NoReadyToRun   # smaller, but cold start roughly doubles
#   pwsh packaging/publish.ps1 -Aot -Runtime linux-x64
#
# Native AOT requires the native toolchain for the target OS and cannot be
# combined with PublishSingleFile. Cross-OS AOT builds must run on the target OS.

param(
    [string]$Runtime = "win-x64",
    [switch]$NoReadyToRun,
    [switch]$Aot,
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
$mode = if ($Aot) { "Native AOT" } else { "R2R=$r2r" }
Write-Host "publishing $Runtime ($mode) -> $Output" -ForegroundColor Cyan

$publishArgs = @(
    "publish",
    (Join-Path $root "src/AirNet.App/AirNet.App.csproj"),
    "-c", "Release",
    "-r", $Runtime,
    "--self-contained", "true",
    "-p:PublishTrimmed=true",
    "-o", $Output,
    "--nologo"
)
if ($Aot) {
    $publishArgs += @(
        "-p:PublishAot=true",
        "-p:PublishSingleFile=false",
        "-p:PublishReadyToRun=false"
    )
}
else {
    $publishArgs += @(
        "-p:PublishSingleFile=true",
        "-p:TrimMode=partial",
        "-p:PublishReadyToRun=$r2r",
        "-p:EnableCompressionInSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=false"
    )
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$files = Get-ChildItem $Output -File
# Symbols are stripped from the shipping folder; keep them in artifacts if a crash needs a stack.
# Linux Native AOT emits AirNet.dbg next to the executable (not a PDB).
Get-ChildItem $Output -File | Where-Object { $_.Extension -in '.pdb', '.dbg' } | Remove-Item -Force
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
