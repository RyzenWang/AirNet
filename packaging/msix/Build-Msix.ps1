# Builds a sideloadable MSIX package for AirNet from a folder publish.
#
# Adapted from the AirPDF pipeline, which already ships through the Microsoft Store.
# The steps are the same and each one exists for a reason documented below.
#
#   powershell -File packaging/msix/Build-Msix.ps1 -Version 0.2.9.0
#   powershell -File packaging/msix/Build-Msix.ps1 -Version 0.2.9.0 -NoAot
#
# Notes:
#   * MSIX cannot use PublishSingleFile, so the app is published as a folder.
#   * The package version NEVER overrides the app version: the Store channel numbers
#     packages on its own line, and the About card shows both so a report is traceable.
#   * AOT is the default: half the size and well under a second to the first window.
#   * The package is unsigned unless -CertificatePath is given. A Store submission is
#     re-signed by Microsoft; a sideload needs a certificate the machine trusts.

param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $IdentityName = 'AirNet',
    [string] $Publisher = 'CN=AirNet Test Signing (not for release)',
    [string] $PublisherDisplayName = 'AirNet',

    [string] $Configuration = 'Release',
    [string] $OutputDirectory = 'artifacts/msix',

    [string] $CertificatePath,
    [string] $CertificatePassword,

    [switch] $SkipPublish,
    [switch] $SelfSign,
    [switch] $ForStore,
    [switch] $NoAot,

    [ValidateSet('x64')]
    [string] $Architecture = 'x64'
)

$ErrorActionPreference = 'Stop'

function Write-Step([string] $Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path (Join-Path $root 'AirNet.slnx'))) {
    throw "Could not find the repository root from $PSScriptRoot."
}

$outputDirectory = Join-Path $root $OutputDirectory
$publishDirectory = Join-Path $outputDirectory "app-$Architecture"
$layoutDirectory = Join-Path $outputDirectory "layout-$Architecture"

if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "-Version must have four numeric parts, for example 0.2.9.0 (got '$Version')."
}

$versionParts = $Version.Split('.') | ForEach-Object { [int] $_ }
if ($versionParts | Where-Object { $_ -gt 65535 }) {
    throw "-Version parts must each be 65535 or less (got '$Version')."
}

if (-not $Version.EndsWith('.0')) {
    Write-Warning 'The fourth version part is reserved by the Store and must be 0 for submissions.'
}

# Manifest identity rules, checked here so a packaging failure is readable.
if ($IdentityName -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]*$' -or $IdentityName.Length -lt 3 -or $IdentityName.Length -gt 50) {
    throw "-IdentityName must be 3-50 characters of letters, digits, periods or dashes (got '$IdentityName')."
}

if (-not $Publisher.StartsWith('CN=') -or $Publisher -match '[\r\n]') {
    throw "-Publisher must be a single-line distinguished name starting with 'CN=' (got '$Publisher')."
}

function Resolve-BuildTools {
    param([string] $ToolName)

    $command = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    foreach ($kit in @('C:\Program Files (x86)\Windows Kits\10\bin', 'C:\Program Files\Windows Kits\10\bin')) {
        if (-not (Test-Path $kit)) { continue }
        $found = Get-ChildItem $kit -Recurse -Filter "$ToolName.exe" -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    throw "Could not find $ToolName.exe. Install the Windows SDK or add it to PATH."
}

$makeAppx = Resolve-BuildTools -ToolName 'makeappx'
Write-Host "makeappx: $makeAppx"

if (-not $SkipPublish) {
    Write-Step ("Publishing the application as a folder layout ({0})" -f $(if ($NoAot) { 'JIT' } else { 'AOT' }))
    if (Test-Path $publishDirectory) { Remove-Item $publishDirectory -Recurse -Force }

    $aotArguments = @()
    if (-not $NoAot) {
        $aotArguments = @('-p:PublishAot=true', '-p:StripSymbols=true')
    }

    & dotnet publish (Join-Path $root 'src/AirNet.App/AirNet.App.csproj') `
        -c $Configuration `
        -r "win-$Architecture" `
        --self-contained true `
        -p:PublishSingleFile=false `
        @aotArguments `
        -o $publishDirectory

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
}

if (-not (Test-Path (Join-Path $publishDirectory 'AirNet.exe'))) {
    throw "No AirNet.exe in $publishDirectory."
}

Write-Step 'Building the package layout'
if (Test-Path $layoutDirectory) { Remove-Item $layoutDirectory -Recurse -Force }
New-Item -ItemType Directory -Path $layoutDirectory -Force | Out-Null

Get-ChildItem $publishDirectory -Force | Copy-Item -Destination $layoutDirectory -Recurse -Force

# MSIX refuses debug symbols and anything that is not part of the product.
$pdbs = Get-ChildItem $layoutDirectory -Recurse -Filter *.pdb -ErrorAction SilentlyContinue
if ($pdbs) { $pdbs | Remove-Item -Force }

Write-Step 'Generating logo assets'
$assetsDirectory = Join-Path $layoutDirectory 'Assets'
New-Item -ItemType Directory -Path $assetsDirectory -Force | Out-Null

Add-Type -AssemblyName System.Drawing

# The source is the 512px PNG master, not the .ico: System.Drawing decodes PNG-compressed
# ICO frames as noise, which is how the first package shipped a rainbow tile.
$sourceIcon = Join-Path $root 'docs/icon-concepts/01-pulse.png'
if (-not (Test-Path $sourceIcon)) { throw "Missing icon: $sourceIcon" }

function New-PackageImage {
    param(
        [string] $SourcePath,
        [string] $Target,
        [int] $Width,
        [int] $Height,
        [double] $ContentScale = 1.0,
        [bool] $Paper = $false
    )

    $image = [System.Drawing.Image]::FromFile($SourcePath)
    try {
            $bitmap = New-Object System.Drawing.Bitmap($Width, $Height)
            try {
                $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
                try {
                    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

                    # MSIX logos are canvases: Start draws the square ones at full size, so the
                    # mark stays inset instead of filling the tile edge to edge.
                    if ($Paper) {
                        $graphics.Clear([System.Drawing.Color]::FromArgb(255, 241, 238, 231))
                    }
                    else {
                        $graphics.Clear([System.Drawing.Color]::Transparent)
                    }

                    $scale = [Math]::Min(($Width * $ContentScale) / $image.Width, ($Height * $ContentScale) / $image.Height)
                    $drawWidth = [int]($image.Width * $scale)
                    $drawHeight = [int]($image.Height * $scale)
                    $graphics.DrawImage($image, [int](($Width - $drawWidth) / 2), [int](($Height - $drawHeight) / 2), $drawWidth, $drawHeight)
                }
                finally { $graphics.Dispose() }

                $bitmap.Save($Target, [System.Drawing.Imaging.ImageFormat]::Png)
            }
            finally { $bitmap.Dispose() }
        }
    finally { $image.Dispose() }
}

$images = @(
    @{ Name = 'Square44x44Logo.png'; Width = 44; Height = 44; Scale = 0.96; Paper = $false },
    @{ Name = 'Square150x150Logo.png'; Width = 150; Height = 150; Scale = 1.0; Paper = $false },
    @{ Name = 'StoreLogo.png'; Width = 50; Height = 50; Scale = 0.92; Paper = $false },
    @{ Name = 'Wide310x150Logo.png'; Width = 310; Height = 150; Scale = 0.60; Paper = $false },
    @{ Name = 'SplashScreen.png'; Width = 620; Height = 300; Scale = 0.42; Paper = $false }
)

foreach ($image in $images) {
    New-PackageImage -SourcePath $sourceIcon -Target (Join-Path $assetsDirectory $image.Name) `
        -Width $image.Width -Height $image.Height -ContentScale $image.Scale -Paper $image.Paper
    Write-Host "  $($image.Name) $($image.Width)x$($image.Height)"
}

# Windows asks for a scale-specific file before it upscales the 100% one. Providing the
# common DPI steps keeps the tile crisp on 125-400% displays instead of resampling.
$scaleVariants = @(125, 150, 200, 400)
foreach ($percent in $scaleVariants) {
    $factor = $percent / 100.0

    foreach ($base in $images) {
        $width = [int][Math]::Round($base.Width * $factor)
        $height = [int][Math]::Round($base.Height * $factor)
        $name = [System.IO.Path]::GetFileNameWithoutExtension($base.Name) + ".scale-$percent.png"
        New-PackageImage -SourcePath $sourceIcon -Target (Join-Path $assetsDirectory $name) `
            -Width $width -Height $height -ContentScale $base.Scale -Paper $base.Paper
    }
}

Write-Host "  scale variants: $($scaleVariants -join ', ')%"
# Windows picks target-size variants for Start, the taskbar and the app list. Without
# them the single 44px asset gets scaled, which makes the icon look oversized.
foreach ($size in 16, 20, 24, 32, 40, 48, 64, 256) {
    $name = "Square44x44Logo.targetsize-$size.png"
    New-PackageImage -SourcePath $sourceIcon -Target (Join-Path $assetsDirectory $name) `
        -Width $size -Height $size -ContentScale 0.92 -Paper $false
    Write-Host "  $name ${size}x${size}"
}

Write-Step 'Expanding the manifest'
$manifest = Get-Content (Join-Path $PSScriptRoot 'AppxManifest.xml') -Raw
$manifest = $manifest.Replace('{{IDENTITY_NAME}}', $IdentityName)
$manifest = $manifest.Replace('{{PUBLISHER_DISPLAY_NAME}}', $PublisherDisplayName)
$manifest = $manifest.Replace('{{PUBLISHER}}', $Publisher)
$manifest = $manifest.Replace('{{VERSION}}', $Version)
$manifest = $manifest.Replace('{{ARCH}}', $Architecture)
Set-Content -Path (Join-Path $layoutDirectory 'AppxManifest.xml') -Value $manifest -Encoding utf8

if ($ForStore -and $CertificatePath) {
    throw '-ForStore publishes unsigned: the Store re-signs the package. Omit -CertificatePath/-SelfSign.'
}

if ($SelfSign -and -not $CertificatePath) {
    Write-Step 'Creating a self-signed certificate for this build'
    $certificatePath = Join-Path $outputDirectory 'AirNet-TestSigning.pfx'
    $publicPath = Join-Path $outputDirectory 'AirNet-TestSigning.cer'
    $password = ConvertTo-SecureString -String 'airnet' -Force -AsPlainText

    if (Test-Path $certificatePath) {
        Write-Host "  reusing $certificatePath"
    }
    else {
        $certificate = New-SelfSignedCertificate -Type Custom `
            -Subject $Publisher `
            -KeyUsage DigitalSignature `
            -FriendlyName 'AirNet MSIX test signing' `
            -CertStoreLocation 'Cert:\CurrentUser\My' `
            -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}') `
            -NotAfter (Get-Date).AddYears(2)

        Export-PfxCertificate -Cert $certificate -FilePath $certificatePath -Password $password | Out-Null
        Export-Certificate -Cert $certificate -FilePath $publicPath | Out-Null
        Write-Host "  $($certificate.Thumbprint)"
    }

    $CertificatePath = $certificatePath
    $CertificatePassword = 'airnet'
}
Write-Step 'Packing'
$packageName = "AirNet-$Version-$Architecture.msix"
$packagePath = Join-Path $outputDirectory $packageName
if (Test-Path $packagePath) { Remove-Item $packagePath -Force }

& $makeAppx pack /d $layoutDirectory /p $packagePath /o
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed with exit code $LASTEXITCODE." }

if ($CertificatePath) {
    Write-Step 'Signing with the supplied certificate'
    $signTool = Resolve-BuildTools -ToolName 'signtool'
    $signArgs = @('sign', '/fd', 'SHA256', '/f', $CertificatePath, '/a')
    if ($CertificatePassword) { $signArgs += @('/p', $CertificatePassword) }
    $signArgs += $packagePath

    & $signTool @signArgs
    if ($LASTEXITCODE -ne 0) { throw "signtool failed with exit code $LASTEXITCODE." }
}

if ($ForStore) {
    # Partner Center accepts .msixupload: a zip holding the package. Written through the zip
    # API so the extension is exactly what the portal expects.
    Write-Step 'Building the .msixupload for Partner Center'
    $uploadName = "AirNet-$Version-$Architecture.msixupload"
    $uploadPath = Join-Path $outputDirectory $uploadName
    if (Test-Path $uploadPath) { Remove-Item $uploadPath -Force }

    $uploadDirectory = Join-Path $outputDirectory 'upload'
    if (Test-Path $uploadDirectory) { Remove-Item $uploadDirectory -Recurse -Force }
    New-Item -ItemType Directory -Path $uploadDirectory -Force | Out-Null
    Copy-Item $packagePath (Join-Path $uploadDirectory $packageName)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $uploadDirectory, $uploadPath,
        [System.IO.Compression.CompressionLevel]::Optimal, $false)

    Write-Host "  $uploadName" -ForegroundColor Green
}

Write-Step 'Verifying the package'
$verifyDirectory = Join-Path $outputDirectory 'verify'
if (Test-Path $verifyDirectory) { Remove-Item $verifyDirectory -Recurse -Force }

& $makeAppx unpack /p $packagePath /d $verifyDirectory /o
if ($LASTEXITCODE -ne 0) { throw 'The package could not be unpacked; it is likely corrupt.' }

$generated = [xml](Get-Content (Join-Path $verifyDirectory 'AppxManifest.xml') -Raw)
$ns = New-Object System.Xml.XmlNamespaceManager($generated.NameTable)
$ns.AddNamespace('f', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$ns.AddNamespace('rescap', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities')

$identity = $generated.SelectSingleNode('/f:Package/f:Identity', $ns)
$executable = $generated.SelectSingleNode('//f:Application/@Executable', $ns).Value
$fullTrust = $generated.SelectSingleNode('//rescap:Capability[@Name="runFullTrust"]', $ns)

$checks = @(
    @{ Name = 'identity version'; Ok = ($identity.Version -eq $Version); Detail = $identity.Version },
    @{ Name = 'identity name'; Ok = ($identity.Name -eq $IdentityName); Detail = $identity.Name },
    @{ Name = 'identity publisher'; Ok = ($identity.Publisher -eq $Publisher); Detail = $identity.Publisher },
    @{ Name = 'architecture'; Ok = ($identity.ProcessorArchitecture -eq $Architecture); Detail = $identity.ProcessorArchitecture },
    @{ Name = 'entry executable present'; Ok = (Test-Path (Join-Path $verifyDirectory $executable)); Detail = $executable },
    @{ Name = 'runFullTrust capability'; Ok = ($null -ne $fullTrust); Detail = 'declared' }
)

foreach ($image in @('Square44x44Logo.png', 'Square150x150Logo.png', 'StoreLogo.png', 'Wide310x150Logo.png', 'SplashScreen.png')) {
    $checks += @{ Name = "asset $image"; Ok = (Test-Path (Join-Path $verifyDirectory "Assets/$image")); Detail = $image }
}

$targets = @(Get-ChildItem (Join-Path $verifyDirectory 'Assets') -Filter 'Square44x44Logo.targetsize-*.png' -ErrorAction SilentlyContinue)
$checks += @{ Name = 'app list target-size assets'; Ok = ($targets.Count -ge 8); Detail = "$($targets.Count) assets" }

$failed = 0
foreach ($check in $checks) {
    if ($check.Ok) {
        Write-Host ("  PASS  {0} ({1})" -f $check.Name, $check.Detail) -ForegroundColor Green
    }
    else {
        Write-Host ("  FAIL  {0} ({1})" -f $check.Name, $check.Detail) -ForegroundColor Red
        $failed++
    }
}

$packageSize = [math]::Round((Get-Item $packagePath).Length / 1MB, 1)
$payloadSize = [math]::Round((Get-ChildItem $layoutDirectory -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)

Write-Host ''
Write-Host "Package: $packagePath" -ForegroundColor Green
Write-Host "  version      $Version"
Write-Host "  identity     $IdentityName"
Write-Host "  publisher    $Publisher"
Write-Host "  architecture $Architecture"
Write-Host "  packaged     $packageSize MB from $payloadSize MB of payload"
Write-Host "  signed       $(if ($CertificatePath) { 'yes' } else { 'no' })"

if ($failed -gt 0) {
    throw "$failed package check(s) failed."
}

Write-Host ''
if ($CertificatePath) {
    Write-Host 'Sideload with: powershell -File packaging/msix/Install-AirNet.ps1' -ForegroundColor Yellow
}
else {
    Write-Host 'Unsigned: valid for a Store upload, not for sideloading.' -ForegroundColor Yellow
}
