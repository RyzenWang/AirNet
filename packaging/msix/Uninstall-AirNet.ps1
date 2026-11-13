# Removes the sideloaded AirNet package and the test certificate trusted by the
# install script.
#
#   powershell -File packaging/msix/Uninstall-AirNet.ps1

param(
    [string] $Certificate = (Join-Path $PSScriptRoot '..\..\artifacts\msix\AirNet-TestSigning.cer')
)

$ErrorActionPreference = 'Stop'

$app = Get-AppxPackage -Name AirNet
if ($app) {
    Remove-AppxPackage -Package $app.PackageFullName
    Write-Host "Removed $($app.PackageFullName)" -ForegroundColor Green
}
else {
    Write-Host 'AirNet is not installed.' -ForegroundColor Yellow
}

if (Test-Path $Certificate) {
    $thumbprint = (New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $Certificate).Thumbprint
    foreach ($store in 'Cert:\LocalMachine\Root', 'Cert:\LocalMachine\TrustedPeople') {
        $found = Get-ChildItem $store | Where-Object { $_.Thumbprint -eq $thumbprint }
        if ($found) {
            Remove-Item $found.PSPath
            Write-Host "Removed certificate from $store" -ForegroundColor Green
        }
    }
}
