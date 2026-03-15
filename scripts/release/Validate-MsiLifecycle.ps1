param(
    [Parameter(Mandatory = $true)]
    [string]$MsiPath,
    [string]$ExpectedDisplayName = "FanControl Pro",
    [string]$ExecutableName = "FanControlPro.Presentation.exe",
    [int]$LaunchWaitSeconds = 8
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "Validate-MsiLifecycle.ps1 can only run on Windows."
}

if (-not (Test-Path -LiteralPath $MsiPath)) {
    throw "MSI file not found: $MsiPath"
}

$msiFullPath = (Resolve-Path -LiteralPath $MsiPath).Path
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$logRoot = Join-Path $env:TEMP "FanControlPro-MsiValidation-$runId"
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

$installLog = Join-Path $logRoot "msi-install.log"
$uninstallLog = Join-Path $logRoot "msi-uninstall.log"
$summaryPath = Join-Path $logRoot "summary.txt"

function Invoke-MsiExec {
    param(
        [string]$Action,
        [string]$Msi,
        [string]$LogPath
    )

    $arguments = "$Action `"$Msi`" /qn /norestart /l*v `"$LogPath`""
    $process = Start-Process -FilePath "msiexec.exe" -ArgumentList $arguments -Wait -PassThru

    $acceptable = @(0, 1641, 3010)
    if ($acceptable -notcontains $process.ExitCode) {
        throw "msiexec failed (action=$Action, exitCode=$($process.ExitCode), log=$LogPath)"
    }
}

function Get-InstallEntry {
    param([string]$DisplayName)

    $paths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )

    $entries = foreach ($path in $paths) {
        Get-ItemProperty -Path $path -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -eq $DisplayName }
    }

    if (-not $entries) {
        return $null
    }

    return $entries | Sort-Object DisplayVersion -Descending | Select-Object -First 1
}

function Resolve-InstallDirectory {
    param(
        [object]$InstallEntry,
        [string]$ExeName
    )

    if ($InstallEntry -and -not [string]::IsNullOrWhiteSpace($InstallEntry.InstallLocation)) {
        $candidate = $InstallEntry.InstallLocation
        $exePath = Join-Path $candidate $ExeName
        if (Test-Path -LiteralPath $exePath) {
            return $candidate
        }
    }

    $roots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    foreach ($root in $roots) {
        $matches = Get-ChildItem -Path $root -Filter $ExeName -Recurse -File -ErrorAction SilentlyContinue
        if ($matches) {
            return $matches[0].DirectoryName
        }
    }

    return $null
}

Write-Host "Installing MSI: $msiFullPath"
Invoke-MsiExec -Action "/i" -Msi $msiFullPath -LogPath $installLog

$installEntry = Get-InstallEntry -DisplayName $ExpectedDisplayName
if ($null -eq $installEntry) {
    throw "Installed product '$ExpectedDisplayName' not found in uninstall registry."
}

$installDirectory = Resolve-InstallDirectory -InstallEntry $installEntry -ExeName $ExecutableName
if ([string]::IsNullOrWhiteSpace($installDirectory)) {
    throw "Unable to resolve install directory for '$ExpectedDisplayName'."
}

$exePath = Join-Path $installDirectory $ExecutableName
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Executable not found after install: $exePath"
}

Write-Host "Launching app: $exePath"
$process = Start-Process -FilePath $exePath -ArgumentList "--start-minimized" -PassThru
Start-Sleep -Seconds $LaunchWaitSeconds

if ($process.HasExited) {
    throw "Application exited early after install (exitCode=$($process.ExitCode))."
}

Stop-Process -Id $process.Id -Force
Start-Sleep -Seconds 1

$appDataRoot = Join-Path $env:APPDATA "FanControlPro"
New-Item -ItemType Directory -Path $appDataRoot -Force | Out-Null
$sentinelPath = Join-Path $appDataRoot "installer-sentinel.txt"
"sentinel-created:$([DateTimeOffset]::UtcNow.ToString('o'))" | Set-Content -Path $sentinelPath -Encoding UTF8

Write-Host "Uninstalling MSI: $msiFullPath"
Invoke-MsiExec -Action "/x" -Msi $msiFullPath -LogPath $uninstallLog

if (Test-Path -LiteralPath $exePath) {
    throw "Executable still present after uninstall: $exePath"
}

if (-not (Test-Path -LiteralPath $sentinelPath)) {
    throw "User data sentinel missing after uninstall: $sentinelPath"
}

$summary = @(
    "MSI lifecycle validation passed.",
    "MSI: $msiFullPath",
    "Install dir: $installDirectory",
    "Install log: $installLog",
    "Uninstall log: $uninstallLog",
    "Sentinel retained: $sentinelPath"
)

$summary | Set-Content -Path $summaryPath -Encoding UTF8
Write-Host ($summary -join [Environment]::NewLine)
