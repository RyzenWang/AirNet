# Installs the locally built AirNet MSIX for review.
#
# Windows only accepts a sideloaded package when the signing certificate's root is
# trusted machine-wide, and that needs administrator rights. This script therefore
# must run elevated; it refuses to pretend otherwise.
#
#   powershell -File packaging/msix/Install-AirNet.ps1
#   powershell -File packaging/msix/Install-AirNet.ps1 -VerifyOnly
#
# The certificate import goes through certutil rather than Import-Certificate:
# importing into LocalMachine\Root through the cmdlet can pop a confirmation that a
# non-interactive session silently skips, which is exactly how the first attempt
# failed with 0x800B0109 while everything else looked correct.
#
# Everything this changes is undone by Uninstall-AirNet.ps1 in this folder.

param(
    [string] $Package,
    [string] $Certificate,
    [switch] $VerifyOnly
)

$ErrorActionPreference = 'Stop'

function Write-Step([string] $Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

$artifacts = Join-Path $PSScriptRoot '..\..\artifacts\msix'

if (-not $Package) {
    $candidate = Get-ChildItem $artifacts -Filter 'AirNet-*-x64.msix' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $candidate) { throw 'No package found. Run packaging/msix/Build-Msix.ps1 -SelfSign first.' }
    $Package = $candidate.FullName
}

if (-not $Certificate) {
    $candidate = Join-Path $artifacts 'AirNet-TestSigning.cer'
    if (-not (Test-Path $candidate)) { throw 'No signing certificate found. Build with -SelfSign first.' }
    $Certificate = $candidate
}

foreach ($path in @($Package, $Certificate)) {
    if (-not (Test-Path $path)) { throw "Missing $path" }
}

# The package and the .cer must belong together; a mismatch produces 0x800B0109 and
# looks exactly like an untrusted root.
$signature = Get-AuthenticodeSignature $Package
if (-not $signature.SignerCertificate) {
    throw 'The package is unsigned. Sideloading needs a signed package (build with -SelfSign).'
}

$cerThumbprint = (New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $Certificate).Thumbprint
if ($signature.SignerCertificate.Thumbprint -ne $cerThumbprint) {
    throw "The package is signed by a different certificate than the .cer. Rebuild with -SelfSign."
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

function Test-Trusted([string] $Store, [string] $Thumbprint) {
    $hit = Get-ChildItem "Cert:\LocalMachine\$Store" -ErrorAction SilentlyContinue |
        Where-Object { $_.Thumbprint -eq $Thumbprint }
    return [bool]$hit
}

$trustedRoot = Test-Trusted 'Root' $cerThumbprint
$trustedPeople = Test-Trusted 'TrustedPeople' $cerThumbprint

Write-Host "package     $([System.IO.Path]::GetFileName($Package))"
Write-Host "certificate $cerThumbprint"
Write-Host "  LocalMachine\Root          $(if ($trustedRoot) { 'trusted' } else { 'MISSING' })"
Write-Host "  LocalMachine\TrustedPeople $(if ($trustedPeople) { 'trusted' } else { 'MISSING' })"
Write-Host "elevated    $isAdmin"
Write-Host ''

if ($VerifyOnly) {
    if ($trustedRoot -and $trustedPeople) {
        Write-Host 'Trust is in place; the package can be installed.' -ForegroundColor Green
    }
    else {
        Write-Host 'Trust is incomplete. Run this script from an elevated PowerShell.' -ForegroundColor Yellow
    }
    return
}

if (-not $isAdmin) {
    throw @"
This must run from an elevated PowerShell: Windows only trusts a sideloaded package
whose certificate lives in the machine store.

Right-click PowerShell > Run as administrator, then run:

  powershell -File "$PSScriptRoot\Install-AirNet.ps1"
"@
}

if (-not $trustedRoot) {
    Write-Step 'Trusting the certificate root (machine store)'
    certutil -addstore -f Root $Certificate | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "certutil failed to add the root (exit $LASTEXITCODE)." }
}

if (-not $trustedPeople) {
    Write-Step 'Trusting the certificate for sideloading (machine store)'
    certutil -addstore -f TrustedPeople $Certificate | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "certutil failed to add to TrustedPeople (exit $LASTEXITCODE)." }
}

# Trust is what actually failed last time, so it is re-checked rather than assumed.
if (-not (Test-Trusted 'Root' $cerThumbprint) -or -not (Test-Trusted 'TrustedPeople' $cerThumbprint)) {
    throw 'The certificate import reported success but the machine stores do not contain it.'
}

Write-Step 'Installing the package'
Add-AppxPackage -Path $Package

$app = Get-AppxPackage -Name AirNet
if (-not $app) { throw 'The package installed but Windows does not list it.' }

Write-Host ''
Write-Host "Installed $($app.PackageFullName)" -ForegroundColor Green
Write-Host ''
Write-Host 'What to check, in order:' -ForegroundColor Yellow
Write-Host '  1. Start menu tile: the pulse mark should sit inset, not fill the tile.'
Write-Host '  2. Launch it and run a scan: score, latency, dual stack, DNS comparison.'
Write-Host '  3. Settings > DNS: the Store build cannot change DNS itself; it must say so'
Write-Host '     and still show the command to copy.'
Write-Host '  4. History survives a restart (it lives in the package data folder, not'
Write-Host '     %LOCALAPPDATA%\AirNet - that is normal for MSIX).'
Write-Host ''
Write-Host 'Uninstall with Uninstall-AirNet.ps1 in this folder.' -ForegroundColor Yellow
