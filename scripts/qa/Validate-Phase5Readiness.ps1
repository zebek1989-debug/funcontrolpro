[CmdletBinding()]
param(
    [string]$TestsProjectPath = "tests/FanControlPro.Tests/FanControlPro.Tests.csproj",
    [string]$MatrixPath = "docs/qa/hardware-matrix.csv",
    [string]$PerformanceBaselinesPath = "docs/qa/performance-baselines.csv",
    [string]$ReportPath = "artifacts/qa/phase5-readiness-report.md",
    [int]$MinFullControl = 3,
    [int]$MinMonitoringOnly = 10,
    [switch]$RequireValidatedMatrix
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

function Get-MatrixSummary {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Matrix file not found: $Path"
    }

    $rows = @(Import-Csv -LiteralPath $Path)
    if (-not $rows -or $rows.Count -eq 0) {
        throw "Matrix file is empty: $Path"
    }

    $fullRows = @($rows | Where-Object { $_.SupportTarget -eq "FullControl" })
    $monitoringRows = @($rows | Where-Object { $_.SupportTarget -eq "MonitoringOnly" })

    $validatedFull = @($rows | Where-Object {
            $_.SupportTarget -eq "FullControl" -and $_.ValidationStatus -eq "Validated"
        })
    $validatedMonitoring = @($rows | Where-Object {
            $_.SupportTarget -eq "MonitoringOnly" -and $_.ValidationStatus -eq "Validated"
        })

    $statusGroups = @($rows |
        Group-Object ValidationStatus |
        Sort-Object Name)

    return [pscustomobject]@{
        TotalRows = $rows.Count
        FullRows = $fullRows.Count
        MonitoringRows = $monitoringRows.Count
        ValidatedFullRows = $validatedFull.Count
        ValidatedMonitoringRows = $validatedMonitoring.Count
        StatusBreakdown = $statusGroups
    }
}

function Get-PerformanceBaselineSummary {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Performance baseline file not found: $Path"
    }

    $rows = @(Import-Csv -LiteralPath $Path)
    if (-not $rows -or $rows.Count -eq 0) {
        throw "Performance baseline file is empty: $Path"
    }

    $statusGroups = @($rows |
        Group-Object Result |
        Sort-Object Name)

    $latest = $rows |
        Sort-Object DateUtc -Descending |
        Select-Object -First 1

    return [pscustomobject]@{
        TotalRows = $rows.Count
        LatestRunId = $latest.RunId
        LatestResult = $latest.Result
        LatestDurationHours = $latest.DurationHours
        StatusBreakdown = $statusGroups
    }
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).ProviderPath
$wslContext = Resolve-WslContext -WindowsPath $repoRoot

$matrixScriptPath = Join-Path $repoRoot "scripts/compatibility/Validate-HardwareMatrix.ps1"
$matrixFullPath = Join-Path $repoRoot $MatrixPath
$performanceBaselinesFullPath = Join-Path $repoRoot $PerformanceBaselinesPath
$testsProjectFullPath = Join-Path $repoRoot $TestsProjectPath
$reportFullPath = Join-Path $repoRoot $ReportPath

Push-Location $repoRoot

try {
    Write-Host "Phase 5 readiness: matrix + stress/perf validation"
    if ($null -ne $wslContext) {
        Write-Host "Detected WSL UNC path. dotnet commands will run via wsl.exe in distro '$($wslContext.Distro)'."
    }

    if (-not (Test-Path -LiteralPath $matrixScriptPath)) {
        throw "Matrix script not found: $matrixScriptPath"
    }

    $matrixParams = @{
        Path = $matrixFullPath
        MinFullControl = $MinFullControl
        MinMonitoringOnly = $MinMonitoringOnly
    }
    if ($RequireValidatedMatrix) {
        $matrixParams.RequireValidated = $true
    }

    & $matrixScriptPath @matrixParams

    $buildParams = @{
        Arguments = @("build", "--verbosity", "minimal")
        StepName = "dotnet build"
        WslContext = $wslContext
    }
    Invoke-DotNetOrThrow @buildParams

    $integrationParams = @{
        Arguments = @(
            "test",
            $testsProjectFullPath,
            "--verbosity",
            "minimal",
            "--no-build",
            "--no-restore",
            "--filter",
            "FullyQualifiedName~FanControlPro.Tests.Integration."
        )
        StepName = "dotnet test (integration scope)"
        WslContext = $wslContext
    }
    Invoke-DotNetOrThrow @integrationParams

    $stressParams = @{
        Arguments = @(
            "test",
            $testsProjectFullPath,
            "--verbosity",
            "minimal",
            "--no-build",
            "--no-restore",
            "--filter",
            "FullyQualifiedName~ProfileSwitchStressTests"
        )
        StepName = "dotnet test (profile stress scope)"
        WslContext = $wslContext
    }
    Invoke-DotNetOrThrow @stressParams

    $matrixSummary = Get-MatrixSummary -Path $matrixFullPath
    $perfSummary = Get-PerformanceBaselineSummary -Path $performanceBaselinesFullPath

    $statusLines = New-Object System.Collections.Generic.List[string]
    foreach ($group in $matrixSummary.StatusBreakdown) {
        $statusLines.Add("- matrix status '$($group.Name)': $($group.Count)")
    }
    foreach ($group in $perfSummary.StatusBreakdown) {
        $statusLines.Add("- performance result '$($group.Name)': $($group.Count)")
    }

    $reportDirectory = Split-Path -Path $reportFullPath -Parent
    if (-not [string]::IsNullOrWhiteSpace($reportDirectory)) {
        New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    }

    $timestamp = [DateTimeOffset]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
    $validatedGateInfo = if ($RequireValidatedMatrix) { "ENFORCED" } else { "NOT_ENFORCED" }

    $report = @(
        "# Phase 5 Readiness Report",
        "",
        "- Timestamp: $timestamp",
        "- Build: PASS",
        "- Integration tests: PASS",
        "- Profile stress tests: PASS",
        "- Matrix gate mode: $validatedGateInfo",
        "",
        "## Hardware Matrix",
        "",
        "- Total rows: $($matrixSummary.TotalRows)",
        "- FullControl rows: $($matrixSummary.FullRows) (required >= $MinFullControl)",
        "- MonitoringOnly rows: $($matrixSummary.MonitoringRows) (required >= $MinMonitoringOnly)",
        "- Validated FullControl rows: $($matrixSummary.ValidatedFullRows)",
        "- Validated MonitoringOnly rows: $($matrixSummary.ValidatedMonitoringRows)",
        "",
        "## Performance Baselines",
        "",
        "- Baseline rows: $($perfSummary.TotalRows)",
        "- Latest run id: $($perfSummary.LatestRunId)",
        "- Latest run result: $($perfSummary.LatestResult)",
        "- Latest duration hours: $($perfSummary.LatestDurationHours)",
        "",
        "## Status Breakdown"
    ) + $statusLines + @(
        "",
        "## Scope",
        "",
        "- 5.1 Hardware compatibility matrix and validation process",
        "- 5.2 Performance/stress verification baseline",
        "- 5.3 Distribution readiness remains in release scripts/docs"
    )

    Set-Content -LiteralPath $reportFullPath -Value $report -Encoding UTF8
    Write-Host "Report generated: $reportFullPath"
}
finally {
    Pop-Location
}
