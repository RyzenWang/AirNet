# Builds every release asset: Windows x64 Native AOT plus macOS/Linux x64 and arm64.
# No setup/installer package is produced by this repository.
#
#   pwsh packaging/pack-all.ps1
#
# Windows is the official AOT asset. macOS/Linux remain single-file R2R until
# Native AOT is validated on native runners and their toolchains.
# Output: artifacts/packages/AirNet-<version>-*.zip

param(
    [switch]$SkipWindows
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if (-not $SkipWindows) {
    & (Join-Path $PSScriptRoot "pack-aot.ps1") -Runtime "win-x64"
}

& (Join-Path $PSScriptRoot "pack-cross.ps1")

Write-Host ""
Write-Host "portable assets:" -ForegroundColor Cyan
Get-ChildItem (Join-Path $root "artifacts/packages") -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^AirNet-\d+\.\d+\.\d+-(win|macos|linux)-.*\.(zip|tar\.gz)$' } |
    Sort-Object Name |
    Select-Object Name, @{Name = "MB"; Expression = { [math]::Round($_.Length / 1MB, 2) } } |
    Format-Table -AutoSize
