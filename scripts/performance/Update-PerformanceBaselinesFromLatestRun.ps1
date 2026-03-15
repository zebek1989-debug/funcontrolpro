[CmdletBinding()]
param(
    [string]$PerfArtifactsRoot = "artifacts/perf",
    [string]$BaselinesPath = "docs/qa/performance-baselines.csv",
    [string]$ConfigurationId = "FC-001",
    [string]$BuildVersion = "local-main",
    [string]$StartupTimeMs = "",
    [string]$Notes = "",
    [switch]$RemovePlaceholders
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-LatestRunDirectory {
    param([string]$Root)

    if (-not (Test-Path -LiteralPath $Root)) {
        throw "Perf artifacts directory not found: $Root"
    }

    $candidates = Get-ChildItem -LiteralPath $Root -Directory |
        Sort-Object LastWriteTimeUtc -Descending

    foreach ($candidate in $candidates) {
        $summaryPath = Join-Path $candidate.FullName "summary.json"
        $samplesPath = Join-Path $candidate.FullName "process-samples.csv"

        if ((Test-Path -LiteralPath $summaryPath) -and (Test-Path -LiteralPath $samplesPath)) {
            return [pscustomobject]@{
                Directory = $candidate.FullName
                SummaryPath = $summaryPath
                SamplesPath = $samplesPath
                RunId = "SOAK-$($candidate.Name)"
            }
        }
    }

    throw "No valid perf run found in '$Root' (missing summary.json/process-samples.csv)."
}

function Get-PeakCpuPercent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SamplesPath,
        [Parameter(Mandatory = $true)]
        [double]$SampleIntervalSeconds
    )

    function Convert-ToDoubleSafe {
        param([object]$Value)

        if ($null -eq $Value) {
            return 0.0
        }

        $text = [string]$Value
        $styles = [System.Globalization.NumberStyles]::Float
        $invariant = [System.Globalization.CultureInfo]::InvariantCulture
        $current = [System.Globalization.CultureInfo]::CurrentCulture
        $parsed = 0.0

        if ([double]::TryParse($text, $styles, $invariant, [ref]$parsed)) {
            return $parsed
        }

        if ([double]::TryParse($text, $styles, $current, [ref]$parsed)) {
            return $parsed
        }

        $normalized = $text -replace ",", "."
        if ([double]::TryParse($normalized, $styles, $invariant, [ref]$parsed)) {
            return $parsed
        }

        throw "Failed to parse numeric value '$text' from $SamplesPath"
    }

    $samples = @(Import-Csv -LiteralPath $SamplesPath)
    if ($samples.Count -lt 2 -or $SampleIntervalSeconds -le 0) {
        return ""
    }

    $peak = 0.0
    for ($i = 1; $i -lt $samples.Count; $i++) {
        $prevCpu = Convert-ToDoubleSafe -Value $samples[$i - 1].CpuSeconds
        $currCpu = Convert-ToDoubleSafe -Value $samples[$i].CpuSeconds
        $delta = $currCpu - $prevCpu
        if ($delta -lt 0) {
            continue
        }

        $percent = ($delta / $SampleIntervalSeconds) * 100.0
        if ($percent -gt $peak) {
            $peak = $percent
        }
    }

    return [Math]::Round($peak, 3).ToString([System.Globalization.CultureInfo]::InvariantCulture)
}

function Resolve-RunResult {
    param($Summary)

    if ($Summary.RamBudgetExceeded -or $Summary.ErrorBudgetExceeded) {
        return "Fail"
    }

    return "Pass"
}

$latestRun = Resolve-LatestRunDirectory -Root $PerfArtifactsRoot
$summary = Get-Content -LiteralPath $latestRun.SummaryPath -Raw | ConvertFrom-Json

$peakCpuPercent = Get-PeakCpuPercent `
    -SamplesPath $latestRun.SamplesPath `
    -SampleIntervalSeconds ([double]$summary.SampleIntervalSeconds)

$result = Resolve-RunResult -Summary $summary
$dateUtc = if ([string]::IsNullOrWhiteSpace($summary.RunTimestampUtc)) {
    [DateTimeOffset]::UtcNow.ToString("o")
}
else {
    [string]$summary.RunTimestampUtc
}

$effectiveNotes = @()
if (-not [string]::IsNullOrWhiteSpace($Notes)) {
    $effectiveNotes += $Notes.Trim()
}
$effectiveNotes += "source=$($latestRun.Directory)"
$notesValue = ($effectiveNotes -join " | ")

$newRow = [ordered]@{
    RunId = $latestRun.RunId
    DateUtc = $dateUtc
    ConfigurationId = $ConfigurationId
    BuildVersion = $BuildVersion
    DurationHours = [string]$summary.DurationHours
    AvgCpuPercent = [string]$summary.AvgCpuPercent
    PeakCpuPercent = $peakCpuPercent
    AvgRamMb = [string]$summary.AvgRamMb
    PeakRamMb = [string]$summary.PeakRamMb
    StartupTimeMs = $StartupTimeMs
    CriticalErrorLines = [string]$summary.ErrorLineCount
    FailsafeEvents = [string]$summary.FailsafeLineCount
    Result = $result
    Notes = $notesValue
}

$rows = @()
if (Test-Path -LiteralPath $BaselinesPath) {
    $rows = @(Import-Csv -LiteralPath $BaselinesPath)
}

if ($RemovePlaceholders) {
    $rows = @($rows | Where-Object { $_.RunId -notlike "SOAK-PLACEHOLDER-*" })
}

$rows = @($rows | Where-Object { $_.RunId -ne $newRow.RunId })
$rows += [pscustomobject]$newRow

$rows | Export-Csv -LiteralPath $BaselinesPath -NoTypeInformation -Encoding UTF8

Write-Host "Updated baselines file: $BaselinesPath"
Write-Host "Added/updated run: $($newRow.RunId)"
Write-Host "Result: $result"
