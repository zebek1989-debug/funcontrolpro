param(
    [string]$Version = "0.1.0-local",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "artifacts/release",
    [switch]$SkipMsi
)

$ErrorActionPreference = "Stop"

function Resolve-WixVersion {
    param([string]$InputVersion)

    if ($InputVersion -match "^\d+\.\d+\.\d+(\.\d+)?$") {
        return $InputVersion
    }

    if ($InputVersion -match "^\d+\.\d+\.\d+") {
        return $Matches[0]
    }

    return "1.0.0"
}

$projectPath = "src/FanControlPro.Presentation/FanControlPro.Presentation.csproj"
$wixProjectPath = "installer/wix/FanControlPro.Installer.wixproj"
$wixManifestScript = "scripts/release/Generate-WixFileManifest.ps1"

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputRootFullPath = [System.IO.Path]::GetFullPath($OutputRoot)
$runRoot = Join-Path $outputRootFullPath "$Version-$timestamp"
$publishDir = Join-Path $runRoot "publish/$Runtime"
$portableZipPath = Join-Path $runRoot "FanControlPro-$Version-$Runtime-portable.zip"
$checksumsPath = Join-Path $runRoot "SHA256SUMS.txt"
$installerDir = Join-Path $runRoot "installer"

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $installerDir -Force | Out-Null

Write-Host "Publishing application..."
dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -o $publishDir | Out-Host

Write-Host "Creating portable zip..."
if (Test-Path -LiteralPath $portableZipPath) {
    Remove-Item -LiteralPath $portableZipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $portableZipPath -Force

$msiPath = $null
$skipInstaller = $SkipMsi.IsPresent -or (-not $IsWindows)

if ($skipInstaller) {
    Write-Warning "Skipping MSI build (SkipMsi set or non-Windows environment)."
}
else {
    if (-not (Test-Path -LiteralPath $wixProjectPath)) {
        throw "WiX project not found: $wixProjectPath"
    }

    if (-not (Test-Path -LiteralPath $wixManifestScript)) {
        throw "WiX manifest script not found: $wixManifestScript"
    }

    Write-Host "Generating WiX file manifest from publish output..."
    & $wixManifestScript `
        -PublishDir $publishDir `
        -OutputFile "installer/wix/PublishedFiles.wxs"

    $wixVersion = Resolve-WixVersion -InputVersion $Version
    Write-Host "Building MSI installer (WiX version: $wixVersion)..."

    dotnet build $wixProjectPath `
        -c $Configuration `
        -p:PublishDir="$publishDir\" `
        -p:ProductVersion=$wixVersion `
        -p:OutputPath="$installerDir\" | Out-Host

    $builtMsi = Get-ChildItem -Path $installerDir -Filter "*.msi" -File -Recurse | Select-Object -First 1
    if ($null -eq $builtMsi) {
        throw "MSI build completed but no .msi artifact was found in $installerDir"
    }

    $msiPath = $builtMsi.FullName
    Write-Host "MSI ready: $msiPath"
}

$entries = @()

$zipHash = Get-FileHash -Algorithm SHA256 -LiteralPath $portableZipPath
$entries += "{0}  {1}" -f $zipHash.Hash, (Split-Path $portableZipPath -Leaf)

if ($msiPath) {
    $msiHash = Get-FileHash -Algorithm SHA256 -LiteralPath $msiPath
    $entries += "{0}  {1}" -f $msiHash.Hash, (Split-Path $msiPath -Leaf)
}

$entries | Set-Content -Path $checksumsPath -Encoding UTF8

Write-Host ""
Write-Host "Release artifacts:"
Write-Host "  Root: $runRoot"
Write-Host "  Portable zip: $portableZipPath"
if ($msiPath) {
    Write-Host "  MSI: $msiPath"
}
Write-Host "  Checksums: $checksumsPath"
