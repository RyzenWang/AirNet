# Installs the locally built AirNet MSIX for review.
#
# Windows only accepts a sideloaded package when its signing certificate is trusted
# machine-wide, and that needs administrator rights. The certificate here is
# self-signed, so after this it is trusted on this machine only.
#
#   powershell -File packaging/msix/Install-AirNet.ps1
#
# Everything it changes is undone by Uninstall-AirNet.ps1 in this folder.

param(
    [string] $Package,
    [string] $Certificate
)

$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this from an elevated PowerShell (right-click > Run as administrator).'
}

if (-not $Package) {
    $candidate = Get-ChildItem (Join-Path $PSScriptRoot '..\..\artifacts\msix') -Filter 'AirNet-*-x64.msix' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $candidate) { throw 'No package found. Run packaging/msix/Build-Msix.ps1 first.' }
    $Package = $candidate.FullName
}

if (-not $Certificate) {
    $Certificate = (Get-ChildItem (Join-Path $PSScriptRoot '..\..\artifacts\msix') -Filter 'AirNet-TestSigning.cer' -ErrorAction SilentlyContinue |
        Select-Object -First 1).FullName
    if (-not $Certificate) { throw 'No signing certificate found. Build with -SelfSign first.' }
}

foreach ($path in @($Package, $Certificate)) {
    if (-not (Test-Path $path)) { throw "Missing $path" }
}

Write-Host '==> Trusting the test certificate' -ForegroundColor Cyan
$cert = Import-Certificate -FilePath $Certificate -CertStoreLocation 'Cert:\LocalMachine\Root'
Import-Certificate -FilePath $Certificate -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
Write-Host "    $($cert.Thumbprint)"

Write-Host '==> Installing the package' -ForegroundColor Cyan
Add-AppxPackage -Path $Package

$app = Get-AppxPackage -Name AirNet
if (-not $app) { throw 'The package installed but Windows does not list it.' }

Write-Host ''
Write-Host "Installed $($app.PackageFullName)" -ForegroundColor Green
Write-Host ''
Write-Host 'What to check, in order:' -ForegroundColor Yellow
Write-Host '  1. Start menu tile: the pulse mark should sit inset, not fill the tile.'
Write-Host '  2. Launch it and run a scan: score, latency, dual stack, DNS comparison.'
Write-Host '  3. Settings: the DNS card should say the Store build cannot change DNS,'
Write-Host '     and still show the command to copy.'
Write-Host '  4. History survives a restart (it lives under the package data folder,'
Write-Host '     not %LOCALAPPDATA%\AirNet - that is normal for MSIX).'
Write-Host ''
Write-Host 'Uninstall with Uninstall-AirNet.ps1 in this folder.' -ForegroundColor Yellow
