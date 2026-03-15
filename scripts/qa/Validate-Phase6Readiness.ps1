[CmdletBinding()]
param(
    [string]$MetricsCsvPath = "docs/beta/beta-metrics-log.csv",
    [double]$CrashFreeThresholdPercent = 99.5,
    [string]$ReleaseTag = "v1.0.0",
    [string]$ReportPath = "artifacts/qa/phase6-readiness-report.md",
    [switch]$FailOnGate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function To-Int {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return 0
    }

    return [int]$Value
}

function Resolve-LatestMetricsRow {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Metrics file not found: $Path"
    }

    $rows = @(Import-Csv -LiteralPath $Path)
    if (-not $rows -or $rows.Count -eq 0) {
        throw "Metrics file is empty: $Path"
    }

    return $rows |
        Sort-Object { [DateTimeOffset]::Parse($_.date) } -Descending |
        Select-Object -First 1
}

function Test-GitTagExists {
    param([string]$TagName)

    $directRefPath = Join-Path -Path ".git/refs/tags" -ChildPath $TagName
    if (Test-Path -LiteralPath $directRefPath) {
        return $true
    }

    $packedRefsPath = ".git/packed-refs"
    if (Test-Path -LiteralPath $packedRefsPath) {
        $matches = @(Select-String -Path $packedRefsPath -Pattern ("refs/tags/" + [regex]::Escape($TagName) + "$"))
        if ($matches.Count -gt 0) {
            return $true
        }
    }

    return $false
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).ProviderPath
Push-Location $repoRoot

try {
    Write-Host "Phase 6 readiness: beta + rc + public release evidence"

    $requiredFiles = @(
        "docs/beta/tester-recruitment-plan.md",
        "docs/beta/tester-roster.csv",
        "docs/beta/beta-feedback-triage.md",
        "docs/release/feature-freeze-policy.md",
        "docs/release/rc-verification-checklist.md",
        "docs/release/public-release-runbook.md",
        "docs/release/RELEASE_NOTES_1.0.0.md",
        "docs/release/KNOWN_ISSUES.md",
        "docs/release/hotfix-response-plan.md",
        "docs/release/post-release-72h-report-v1.0.0.md",
        "CHANGELOG.md",
        "supported-hardware.md"
    )

    $missingFiles = New-Object System.Collections.Generic.List[string]
    foreach ($path in $requiredFiles) {
        if (-not (Test-Path -LiteralPath $path)) {
            $missingFiles.Add($path)
        }
    }

    $latest = Resolve-LatestMetricsRow -Path $MetricsCsvPath
    $sessionsTotal = To-Int $latest.sessions_total
    $sessionsCrashFree = To-Int $latest.sessions_crash_free
    $p0Open = To-Int $latest.p0_open
    $p1Open = To-Int $latest.p1_open
    $p1WithWorkaround = To-Int $latest.p1_with_workaround
    $topIssuesAddressed = To-Int $latest.top_issues_addressed
    $topIssuesTotal = To-Int $latest.total_top_issues

    $crashFreePercent = if ($sessionsTotal -le 0) {
        0.0
    }
    else {
        [Math]::Round((100.0 * $sessionsCrashFree / $sessionsTotal), 2)
    }

    $crashGate = $crashFreePercent -ge $CrashFreeThresholdPercent
    $p0Gate = $p0Open -eq 0
    $p1Gate = $p1Open -le $p1WithWorkaround
    $topIssuesGate = ($topIssuesTotal -gt 0) -and ($topIssuesAddressed -ge [Math]::Min($topIssuesTotal, 10))
    $docsGate = $missingFiles.Count -eq 0
    $tagGate = Test-GitTagExists -TagName $ReleaseTag

    $overall = $crashGate -and $p0Gate -and $p1Gate -and $topIssuesGate -and $docsGate -and $tagGate
    $decision = if ($overall) { "GO" } else { "NO-GO" }

    $reportFullPath = Join-Path $repoRoot $ReportPath
    $reportDirectory = Split-Path -Path $reportFullPath -Parent
    if (-not [string]::IsNullOrWhiteSpace($reportDirectory)) {
        New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    }

    $timestamp = [DateTimeOffset]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
    $missingSection = if ($missingFiles.Count -eq 0) {
        @("- none")
    }
    else {
        $missingFiles | ForEach-Object { "- $_" }
    }

    $report = @(
        "# Phase 6 Readiness Report",
        "",
        "- Timestamp: $timestamp",
        "- Decision: $decision",
        "- Release tag '$ReleaseTag' present: $(if ($tagGate) { "YES" } else { "NO" })",
        "- Latest beta metrics row: $($latest.date)",
        "",
        "## Beta Gates",
        "",
        "- Crash-free %: $crashFreePercent (threshold: $CrashFreeThresholdPercent) -> $(if ($crashGate) { "PASS" } else { "FAIL" })",
        "- Open P0: $p0Open -> $(if ($p0Gate) { "PASS" } else { "FAIL" })",
        "- P1 with workaround gate: open=$p1Open workaround=$p1WithWorkaround -> $(if ($p1Gate) { "PASS" } else { "FAIL" })",
        "- Top issues addressed: $topIssuesAddressed / $topIssuesTotal -> $(if ($topIssuesGate) { "PASS" } else { "FAIL" })",
        "",
        "## Release Evidence",
        "",
        "- Required docs present: $(if ($docsGate) { "YES" } else { "NO" })",
        "",
        "### Missing files",
        ""
    ) + $missingSection + @(
        "",
        "## Scope",
        "",
        "- 6.1 Closed beta process and metrics",
        "- 6.2 RC policy/checklist/docs",
        "- 6.3 Public release artifacts/docs/hotfix readiness"
    )

    Set-Content -LiteralPath $reportFullPath -Value $report -Encoding UTF8
    Write-Host "Report generated: $reportFullPath"

    if ($FailOnGate -and -not $overall) {
        throw "Phase 6 readiness gates failed."
    }
}
finally {
    Pop-Location
}
