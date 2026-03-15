param(
    [string]$ProjectPath = "src/FanControlPro.Presentation/FanControlPro.Presentation.csproj",
    [string]$OutputDirectory = "artifacts/perf",
    [int]$DurationHours = 24,
    [int]$SampleIntervalSeconds = 30,
    [int]$MaxRamMb = 1200,
    [int]$MaxErrorLines = 20
)

$ErrorActionPreference = "Stop"

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

function Convert-WindowsWslPathToLinuxPath {
    param([string]$WindowsPath)

    if ($WindowsPath -match "^\\\\wsl(?:\.localhost)?\\[^\\]+\\(.+)$") {
        $linuxSuffix = $Matches[1] -replace "\\", "/"
        return "/$linuxSuffix"
    }

    return $null
}

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

function Prepare-LocalRunDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,
        [Parameter(Mandatory = $true)]
        [string]$RunId
    )

    if (-not (Test-Path -LiteralPath $SourceDirectory)) {
        throw "Source output directory not found: $SourceDirectory"
    }

    $targetDirectory = Join-Path -Path $env:TEMP -ChildPath ("FanControlPro\soak\" + $RunId + "\net8.0")
    if (Test-Path -LiteralPath $targetDirectory) {
        Remove-Item -LiteralPath $targetDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }

    New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
    Copy-Item -Path (Join-Path $SourceDirectory "*") -Destination $targetDirectory -Recurse -Force

    return $targetDirectory
}

if ($DurationHours -lt 1) {
    throw "DurationHours must be >= 1."
}

if ($SampleIntervalSeconds -lt 5) {
    throw "SampleIntervalSeconds must be >= 5."
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).ProviderPath
$projectFullPath = (Resolve-Path -LiteralPath $ProjectPath).ProviderPath
$wslContext = Resolve-WslContext -WindowsPath $repoRoot

$outputRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
}
else {
    Join-Path $repoRoot $OutputDirectory
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runDirectory = Join-Path $outputRoot $timestamp
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

$samplesPath = Join-Path $runDirectory "process-samples.csv"
$summaryJsonPath = Join-Path $runDirectory "summary.json"
$summaryTextPath = Join-Path $runDirectory "summary.txt"

Write-Host "Building project..."
$projectDirectory = Split-Path -Path $projectFullPath -Parent
$projectLinuxPath = Convert-WindowsWslPathToLinuxPath -WindowsPath $projectFullPath

if ($null -ne $wslContext -and -not [string]::IsNullOrWhiteSpace($projectLinuxPath)) {
    Write-Host "Detected WSL UNC path. Building via wsl.exe in distro '$($wslContext.Distro)'."
    Invoke-NativeOrThrow `
        -FilePath "wsl.exe" `
        -Arguments @(
            "-d",
            $wslContext.Distro,
            "--cd",
            $wslContext.LinuxPath,
            "--exec",
            "dotnet",
            "build",
            $projectLinuxPath,
            "-c",
            "Release"
        ) `
        -StepName "dotnet build (wsl)"
}
else {
    Invoke-NativeOrThrow `
        -FilePath "dotnet" `
        -Arguments @(
            "build",
            $projectFullPath,
            "-c",
            "Release"
        ) `
        -StepName "dotnet build"
}

$sourceOutputDirectory = Join-Path -Path $projectDirectory -ChildPath "bin\Release\net8.0"
$localRunDirectory = Prepare-LocalRunDirectory -SourceDirectory $sourceOutputDirectory -RunId $timestamp
$exePath = Join-Path -Path $localRunDirectory -ChildPath "FanControlPro.Presentation.exe"
$dllPath = Join-Path -Path $localRunDirectory -ChildPath "FanControlPro.Presentation.dll"
$startMode = ""

Write-Host "Starting FanControl Pro soak run (local stage)..."
if (Test-Path -LiteralPath $exePath) {
    $process = Start-Process `
        -FilePath $exePath `
        -ArgumentList @("--start-minimized") `
        -WorkingDirectory (Split-Path -Path $exePath -Parent) `
        -PassThru
    $startMode = "exe"
}
elseif (Test-Path -LiteralPath $dllPath) {
    $process = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList @($dllPath, "--start-minimized") `
        -WorkingDirectory (Split-Path -Path $dllPath -Parent) `
        -PassThru
    $startMode = "dotnet-dll"
}
else {
    throw "Neither executable nor DLL found in staged local directory: $localRunDirectory"
}

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
    ExecutablePath = "$exePath"
    DllPath = "$dllPath"
    StartMode = "$startMode"
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
