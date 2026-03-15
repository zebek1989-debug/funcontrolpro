param(
    [string]$ProjectPath = "src/FanControlPro.Presentation/FanControlPro.Presentation.csproj",
    [string]$OutputDirectory = "artifacts/perf",
    [int]$DurationHours = 24,
    [int]$SampleIntervalSeconds = 30,
    [int]$MaxRamMb = 1200,
    [int]$MaxErrorLines = 20
)

$ErrorActionPreference = "Stop"

if ($DurationHours -lt 1) {
    throw "DurationHours must be >= 1."
}

if ($SampleIntervalSeconds -lt 5) {
    throw "SampleIntervalSeconds must be >= 5."
}

$projectFullPath = Resolve-Path -LiteralPath $ProjectPath
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runDirectory = Join-Path $OutputDirectory $timestamp
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

$samplesPath = Join-Path $runDirectory "process-samples.csv"
$summaryJsonPath = Join-Path $runDirectory "summary.json"
$summaryTextPath = Join-Path $runDirectory "summary.txt"

Write-Host "Building project..."
dotnet build "$projectFullPath" -c Release | Out-Host

Write-Host "Starting FanControl Pro soak run..."
$arguments = @(
    "run",
    "--no-build",
    "--project", "$projectFullPath",
    "--",
    "--start-minimized"
)

$process = Start-Process -FilePath "dotnet" -ArgumentList $arguments -PassThru

$samples = New-Object System.Collections.Generic.List[object]
$startUtc = [DateTimeOffset]::UtcNow
$endUtc = $startUtc.AddHours($DurationHours)

try {
    while ([DateTimeOffset]::UtcNow -lt $endUtc) {
        if ($process.HasExited) {
            throw "FanControl Pro exited unexpectedly with code $($process.ExitCode)."
        }

        $proc = Get-Process -Id $process.Id -ErrorAction Stop

        $samples.Add([pscustomobject]@{
            TimestampUtc = [DateTimeOffset]::UtcNow.ToString("o")
            CpuSeconds = [Math]::Round($proc.CPU, 3)
            WorkingSetMb = [Math]::Round($proc.WorkingSet64 / 1MB, 2)
            PrivateMemoryMb = [Math]::Round($proc.PrivateMemorySize64 / 1MB, 2)
            Handles = $proc.HandleCount
            Threads = $proc.Threads.Count
        })

        Start-Sleep -Seconds $SampleIntervalSeconds
    }
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
}

$samples | Export-Csv -Path $samplesPath -NoTypeInformation

if ($samples.Count -eq 0) {
    throw "No samples collected."
}

$firstSample = $samples[0]
$lastSample = $samples[$samples.Count - 1]
$elapsedSeconds = ($samples.Count - 1) * $SampleIntervalSeconds
if ($elapsedSeconds -le 0) {
    $elapsedSeconds = 1
}

$cpuDeltaSeconds = [double]$lastSample.CpuSeconds - [double]$firstSample.CpuSeconds
$avgCpuPercent = [Math]::Round(($cpuDeltaSeconds / $elapsedSeconds) * 100, 3)
$avgRamMb = [Math]::Round((($samples | Measure-Object -Property WorkingSetMb -Average).Average), 2)
$peakRamMb = [Math]::Round((($samples | Measure-Object -Property WorkingSetMb -Maximum).Maximum), 2)
$peakCpuSeconds = [Math]::Round((($samples | Measure-Object -Property CpuSeconds -Maximum).Maximum), 3)

$logsRoot = Join-Path $env:APPDATA "FanControlPro\logs"
$errorLines = 0
$failsafeLines = 0

if (Test-Path -LiteralPath $logsRoot) {
    $logFiles = Get-ChildItem -Path $logsRoot -Filter "app-*.log" -File | Sort-Object LastWriteTimeUtc -Descending
    foreach ($logFile in $logFiles) {
        $content = Get-Content -LiteralPath $logFile.FullName -ErrorAction SilentlyContinue
        if ($null -eq $content) {
            continue
        }

        $errorLines += @($content | Select-String -Pattern "\[ERR\]|Error|Unhandled|Exception").Count
        $failsafeLines += @($content | Select-String -Pattern "Failsafe|Emergency|Shutdown").Count
    }
}

$summary = [pscustomobject]@{
    RunTimestampUtc = $startUtc.ToString("o")
    ProjectPath = "$projectFullPath"
    DurationHours = $DurationHours
    SampleIntervalSeconds = $SampleIntervalSeconds
    SampleCount = $samples.Count
    AvgCpuPercent = $avgCpuPercent
    PeakCpuSeconds = $peakCpuSeconds
    AvgRamMb = $avgRamMb
    PeakRamMb = $peakRamMb
    ErrorLineCount = $errorLines
    FailsafeLineCount = $failsafeLines
    MaxRamBudgetMb = $MaxRamMb
    MaxErrorLineBudget = $MaxErrorLines
    RamBudgetExceeded = ($peakRamMb -gt $MaxRamMb)
    ErrorBudgetExceeded = ($errorLines -gt $MaxErrorLines)
}

$summary | ConvertTo-Json -Depth 5 | Set-Content -Path $summaryJsonPath -Encoding UTF8

$text = @(
    "Phase 5.2 Soak Summary",
    "Run directory: $runDirectory",
    "Samples: $($summary.SampleCount)",
    "Avg CPU %: $($summary.AvgCpuPercent)",
    "Avg RAM MB: $($summary.AvgRamMb)",
    "Peak RAM MB: $($summary.PeakRamMb)",
    "Error lines: $($summary.ErrorLineCount)",
    "Failsafe lines: $($summary.FailsafeLineCount)",
    "RAM budget exceeded: $($summary.RamBudgetExceeded)",
    "Error budget exceeded: $($summary.ErrorBudgetExceeded)"
)

$text | Set-Content -Path $summaryTextPath -Encoding UTF8

Write-Host "Soak artifacts created:"
Write-Host "  $samplesPath"
Write-Host "  $summaryJsonPath"
Write-Host "  $summaryTextPath"

if ($summary.RamBudgetExceeded -or $summary.ErrorBudgetExceeded) {
    throw "Soak run failed configured budgets."
}

Write-Host "Soak run completed within configured budgets."
