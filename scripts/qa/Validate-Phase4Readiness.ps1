[CmdletBinding()]
param(
    [string]$ProjectPath = "tests/FanControlPro.Tests/FanControlPro.Tests.csproj",
    [string]$ReportPath = "artifacts/qa/phase4-readiness-report.md"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-NativeOrThrow {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$StepName
    )

    & $FilePath @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "$StepName failed with exit code $exitCode."
    }
}

function Resolve-WslContext {
    param([string]$WindowsPath)

    if ($WindowsPath -match "^\\\\wsl(?:\.localhost)?\\([^\\]+)\\(.+)$") {
        $distro = $Matches[1]
        $linuxSuffix = $Matches[2] -replace "\\", "/"
        $linuxPath = "/$linuxSuffix"

        return [pscustomobject]@{
            Distro = $distro
            LinuxPath = $linuxPath
        }
    }

    return $null
}

function Invoke-DotNetOrThrow {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$StepName,
        [pscustomobject]$WslContext
    )

    if ($null -eq $WslContext) {
        Invoke-NativeOrThrow -FilePath "dotnet" -Arguments $Arguments -StepName $StepName
        return
    }

    & wsl.exe -d $WslContext.Distro --cd $WslContext.LinuxPath --exec dotnet @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "$StepName failed with exit code $exitCode."
    }
}

function Resolve-HardwareAccessDefault {
    param([string]$SettingsPath)

    if (-not (Test-Path -LiteralPath $SettingsPath)) {
        return "unknown (missing appsettings.json)"
    }

    $json = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
    if ($null -eq $json.EcWriteSafety) {
        return "unknown (missing EcWriteSafety)"
    }

    return [string]$json.EcWriteSafety.EnableHardwareAccess
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).ProviderPath
$wslContext = Resolve-WslContext -WindowsPath $repoRoot
Push-Location $repoRoot

try {
    Write-Host "Phase 4 readiness: build + targeted tests"
    if ($null -ne $wslContext) {
        Write-Host "Detected WSL UNC path. dotnet commands will run via wsl.exe in distro '$($wslContext.Distro)'."
    }

    Invoke-DotNetOrThrow `
        -Arguments @("build", "--verbosity", "minimal") `
        -StepName "dotnet build" `
        -WslContext $wslContext

    Invoke-DotNetOrThrow `
        -Arguments @(
            "test",
            $ProjectPath,
            "--verbosity",
            "minimal",
            "--no-build",
            "--no-restore",
            "--filter",
            "FullyQualifiedName~FanControlPro.Tests.Presentation."
        ) `
        -StepName "dotnet test (phase 4 presentation scope)"
        -WslContext $wslContext

    Invoke-DotNetOrThrow `
        -Arguments @(
            "test",
            $ProjectPath,
            "--verbosity",
            "minimal",
            "--no-build",
            "--no-restore",
            "--filter",
            "FullyQualifiedName~TaskSchedulerAutostartServiceTests"
        ) `
        -StepName "dotnet test (phase 4 autostart scope)"
        -WslContext $wslContext

    Invoke-DotNetOrThrow `
        -Arguments @(
            "test",
            $ProjectPath,
            "--verbosity",
            "minimal",
            "--no-build",
            "--no-restore",
            "--filter",
            "FullyQualifiedName~JsonApplicationSettingsServiceTests"
        ) `
        -StepName "dotnet test (phase 4 settings scope)"
        -WslContext $wslContext

    $settingsPath = Join-Path $repoRoot "src/FanControlPro.Presentation/appsettings.json"
    $hardwareAccessDefault = Resolve-HardwareAccessDefault -SettingsPath $settingsPath

    $reportDirectory = Split-Path -Path $ReportPath -Parent
    if (-not [string]::IsNullOrWhiteSpace($reportDirectory)) {
        New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    }

    $timestamp = [DateTimeOffset]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
    $report = @(
        "# Phase 4 Readiness Report",
        "",
        "- Timestamp: $timestamp",
        "- Build: PASS",
        "- Targeted tests: PASS (Presentation + Autostart + Settings)",
        "- appsettings EcWriteSafety.EnableHardwareAccess (default): $hardwareAccessDefault",
        "",
        "## Scope",
        "",
        "- 4.1 System Tray and autostart logic",
        "- 4.2 Onboarding and consent UX logic",
        "- 4.3 Settings validation, persistence, and reset flow"
    )

    Set-Content -LiteralPath $ReportPath -Value $report -Encoding UTF8
    Write-Host "Report generated: $ReportPath"
}
finally {
    Pop-Location
}
