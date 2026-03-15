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

$repoRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")
Push-Location $repoRoot

try {
    Write-Host "Phase 4 readiness: build + targeted tests"

    Invoke-NativeOrThrow `
        -FilePath "dotnet" `
        -Arguments @("build", "--verbosity", "minimal") `
        -StepName "dotnet build"

    $filter = "(" +
        "FullyQualifiedName~FanControlPro.Tests.Presentation." +
        "|FullyQualifiedName~TaskSchedulerAutostartServiceTests" +
        "|FullyQualifiedName~JsonApplicationSettingsServiceTests" +
        ")"

    Invoke-NativeOrThrow `
        -FilePath "dotnet" `
        -Arguments @("test", $ProjectPath, "--verbosity", "minimal", "--filter", $filter) `
        -StepName "dotnet test (phase 4 scope)"

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
