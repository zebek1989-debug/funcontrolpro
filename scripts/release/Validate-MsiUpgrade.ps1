param(
    [Parameter(Mandatory = $true)]
    [string]$OldMsiPath,
    [Parameter(Mandatory = $true)]
    [string]$NewMsiPath,
    [string]$ExpectedDisplayName = "FanControl Pro",
    [string]$ExecutableName = "FanControlPro.Presentation.exe",
    [int]$LaunchWaitSeconds = 8
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "Validate-MsiUpgrade.ps1 can only run on Windows."
}

if (-not (Test-Path -LiteralPath $OldMsiPath)) {
    throw "Old MSI file not found: $OldMsiPath"
}

if (-not (Test-Path -LiteralPath $NewMsiPath)) {
    throw "New MSI file not found: $NewMsiPath"
}

$oldMsiFullPath = (Resolve-Path -LiteralPath $OldMsiPath).Path
$newMsiFullPath = (Resolve-Path -LiteralPath $NewMsiPath).Path

$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$logRoot = Join-Path $env:TEMP "FanControlPro-MsiUpgrade-$runId"
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

$oldInstallLog = Join-Path $logRoot "msi-old-install.log"
$newInstallLog = Join-Path $logRoot "msi-new-install.log"
$uninstallLog = Join-Path $logRoot "msi-uninstall.log"
$summaryPath = Join-Path $logRoot "summary.txt"

$installedPackageForCleanup = $null

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

function Parse-VersionOrNull {
    param([string]$Value)

    try {
        return [Version]$Value
    }
    catch {
        return $null
    }
}

try {
    Write-Host "Installing old MSI: $oldMsiFullPath"
    Invoke-MsiExec -Action "/i" -Msi $oldMsiFullPath -LogPath $oldInstallLog
    $installedPackageForCleanup = $oldMsiFullPath

    $oldEntry = Get-InstallEntry -DisplayName $ExpectedDisplayName
    if ($null -eq $oldEntry) {
        throw "Old package install entry '$ExpectedDisplayName' not found."
    }

    $oldVersionRaw = "$($oldEntry.DisplayVersion)"
    $oldVersion = Parse-VersionOrNull -Value $oldVersionRaw

    $appDataRoot = Join-Path $env:APPDATA "FanControlPro"
    New-Item -ItemType Directory -Path $appDataRoot -Force | Out-Null
    $sentinelPath = Join-Path $appDataRoot "upgrade-sentinel.txt"
    "upgrade-sentinel-created:$([DateTimeOffset]::UtcNow.ToString('o'))" | Set-Content -Path $sentinelPath -Encoding UTF8

    Write-Host "Installing new MSI (upgrade): $newMsiFullPath"
    Invoke-MsiExec -Action "/i" -Msi $newMsiFullPath -LogPath $newInstallLog
    $installedPackageForCleanup = $newMsiFullPath

    $newEntry = Get-InstallEntry -DisplayName $ExpectedDisplayName
    if ($null -eq $newEntry) {
        throw "New package install entry '$ExpectedDisplayName' not found after upgrade."
    }

    $newVersionRaw = "$($newEntry.DisplayVersion)"
    $newVersion = Parse-VersionOrNull -Value $newVersionRaw

    if ($oldVersion -and $newVersion) {
        if ($newVersion -le $oldVersion) {
            throw "Upgrade version check failed. Old=$oldVersionRaw New=$newVersionRaw"
        }
    }
    else {
        Write-Warning "Could not parse versions reliably (Old='$oldVersionRaw', New='$newVersionRaw')."
    }

    $installDirectory = Resolve-InstallDirectory -InstallEntry $newEntry -ExeName $ExecutableName
    if ([string]::IsNullOrWhiteSpace($installDirectory)) {
        throw "Unable to resolve install directory after upgrade."
    }

    $exePath = Join-Path $installDirectory $ExecutableName
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Executable not found after upgrade: $exePath"
    }

    Write-Host "Launching upgraded app: $exePath"
    $process = Start-Process -FilePath $exePath -ArgumentList "--start-minimized" -PassThru
    Start-Sleep -Seconds $LaunchWaitSeconds

    if ($process.HasExited) {
        throw "Upgraded application exited early (exitCode=$($process.ExitCode))."
    }

    Stop-Process -Id $process.Id -Force
    Start-Sleep -Seconds 1

    if (-not (Test-Path -LiteralPath $sentinelPath)) {
        throw "Upgrade removed user data sentinel unexpectedly: $sentinelPath"
    }

    $summary = @(
        "MSI upgrade validation passed.",
        "Old MSI: $oldMsiFullPath",
        "New MSI: $newMsiFullPath",
        "Old version: $oldVersionRaw",
        "New version: $newVersionRaw",
        "Install dir: $installDirectory",
        "Sentinel retained: $sentinelPath",
        "Old install log: $oldInstallLog",
        "New install log: $newInstallLog"
    )

    $summary | Set-Content -Path $summaryPath -Encoding UTF8
    Write-Host ($summary -join [Environment]::NewLine)
}
finally {
    if ($installedPackageForCleanup) {
        try {
            Write-Host "Cleanup uninstall: $installedPackageForCleanup"
            Invoke-MsiExec -Action "/x" -Msi $installedPackageForCleanup -LogPath $uninstallLog
        }
        catch {
            Write-Warning "Cleanup uninstall failed: $($_.Exception.Message)"
        }
    }
}
