param(
    [string]$MetricsCsvPath = "docs/beta/beta-metrics-log.csv",
    [double]$CrashFreeThresholdPercent = 99.5,
    [switch]$FailOnGate
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $MetricsCsvPath)) {
    throw "Metrics file not found: $MetricsCsvPath"
}

$rows = Import-Csv -LiteralPath $MetricsCsvPath
if (-not $rows -or $rows.Count -eq 0) {
    throw "Metrics file is empty: $MetricsCsvPath"
}

$latest = $rows |
    Sort-Object { [DateTime]::Parse($_.date) } -Descending |
    Select-Object -First 1

function To-Int([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return 0 }
    return [int]$value
}

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

$overall = $crashGate -and $p0Gate -and $p1Gate -and $topIssuesGate

$report = @(
    "Beta Readiness Snapshot: $($latest.date)"
    "Crash-free %: $crashFreePercent (threshold: $CrashFreeThresholdPercent) -> " + ($(if ($crashGate) { "PASS" } else { "FAIL" }))
    "Open P0: $p0Open -> " + ($(if ($p0Gate) { "PASS" } else { "FAIL" }))
    "P1 with workaround gate: open=$p1Open workaround=$p1WithWorkaround -> " + ($(if ($p1Gate) { "PASS" } else { "FAIL" }))
    "Top issues addressed: $topIssuesAddressed / $topIssuesTotal -> " + ($(if ($topIssuesGate) { "PASS" } else { "FAIL" }))
    "Overall recommendation: " + ($(if ($overall) { "GO" } else { "NO-GO" }))
)

$report -join [Environment]::NewLine | Write-Host

if ($FailOnGate -and -not $overall) {
    throw "Beta readiness gates failed."
}
