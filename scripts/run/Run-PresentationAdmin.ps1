[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$EnableHardwareAccess,

    [switch]$DisableHardwareAccess,

    [switch]$SkipSettingsUpdate,

    [switch]$StartMinimized,

    [switch]$StartupLite
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-RunningOnWindows {
    if ($env:OS -eq "Windows_NT") {
        return $true
    }

    return $false
}

if (-not (Test-RunningOnWindows)) {
    throw "This script can only run on Windows."
}

if ($EnableHardwareAccess -and $DisableHardwareAccess) {
    throw "Use either -EnableHardwareAccess or -DisableHardwareAccess, not both."
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Set-HardwareAccessFlag {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [bool]$Value
    )

    if (-not (Test-Path -LiteralPath $FilePath)) {
        throw "appsettings.json not found: $FilePath"
    }

    $jsonText = Get-Content -LiteralPath $FilePath -Raw
    $config = $jsonText | ConvertFrom-Json

    if ($null -eq $config.EcWriteSafety) {
        $config | Add-Member -MemberType NoteProperty -Name EcWriteSafety -Value ([pscustomobject]@{})
    }

    $config.EcWriteSafety.EnableHardwareAccess = $Value
    $updatedJson = $config | ConvertTo-Json -Depth 20

    # PowerShell 5.1-compatible UTF-8 without BOM writer.
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($FilePath, $updatedJson, $utf8NoBom)
}

function Show-LatestFanControlLogTail {
    $logDirectory = Join-Path $env:APPDATA "FanControlPro\logs"
    if (-not (Test-Path -LiteralPath $logDirectory)) {
        Write-Host "Log directory not found: $logDirectory"
        return
    }

    $latestLog = Get-ChildItem -LiteralPath $logDirectory -Filter "app-*.log" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $latestLog) {
        Write-Host "No app log files found in: $logDirectory"
        return
    }

    Write-Host ""
    Write-Host "Latest app log: $($latestLog.FullName)"
    Write-Host "----- LOG TAIL (last 80 lines) -----"
    Get-Content -LiteralPath $latestLog.FullName -Tail 80
    Write-Host "----- END LOG TAIL -----"
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

function Stop-RunningFanControlProcesses {
    $existing = Get-Process -Name "FanControlPro.Presentation" -ErrorAction SilentlyContinue
    if ($null -eq $existing) {
        return
    }

    Write-Host "Stopping existing FanControl Pro process(es)..."
    $existing | Stop-Process -Force -ErrorAction SilentlyContinue

    Start-Sleep -Milliseconds 600
}

function Prepare-LocalRunDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,
        [Parameter(Mandatory = $true)]
        [string]$Configuration
    )

    if (-not (Test-Path -LiteralPath $SourceDirectory)) {
        throw "Source output directory not found: $SourceDirectory"
    }

    $targetDirectory = Join-Path -Path $env:TEMP -ChildPath ("FanControlPro\run\" + $Configuration + "\net8.0")
    if (Test-Path -LiteralPath $targetDirectory) {
        Remove-Item -LiteralPath $targetDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }

    New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
    Copy-Item -Path (Join-Path $SourceDirectory "*") -Destination $targetDirectory -Recurse -Force

    return $targetDirectory
}

function Try-BringProcessWindowToFront {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process
    )

    try {
        $Process.Refresh()
    }
    catch {
        return
    }

    if ($Process.MainWindowHandle -eq 0) {
        Write-Host "Process has no visible main window handle (likely tray/background mode)."
        return
    }

    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class FanControlWindowApi
{
    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@ -ErrorAction SilentlyContinue | Out-Null

    [FanControlWindowApi]::ShowWindowAsync($Process.MainWindowHandle, 9) | Out-Null
    [FanControlWindowApi]::SetForegroundWindow($Process.MainWindowHandle) | Out-Null
}

$scriptPath = $MyInvocation.MyCommand.Path
$repoRootResolved = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")
$repoRoot = $repoRootResolved.ProviderPath
$appSettingsPath = Join-Path -Path $repoRoot -ChildPath "src\FanControlPro.Presentation\appsettings.json"
$projectPath = Join-Path -Path $repoRoot -ChildPath "src\FanControlPro.Presentation\FanControlPro.Presentation.csproj"
$solutionPath = Join-Path -Path $repoRoot -ChildPath "FanControlPro.sln"

if (-not (Test-IsAdministrator)) {
    if ($EnableHardwareAccess) {
        Set-HardwareAccessFlag -FilePath $appSettingsPath -Value $true
    }
    elseif ($DisableHardwareAccess) {
        Set-HardwareAccessFlag -FilePath $appSettingsPath -Value $false
    }

    $argumentParts = @(
        "-NoExit",
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", ('"{0}"' -f $scriptPath),
        "-Configuration", $Configuration,
        "-SkipSettingsUpdate"
    )

    if ($EnableHardwareAccess) {
        $argumentParts += "-EnableHardwareAccess"
    }
    elseif ($DisableHardwareAccess) {
        $argumentParts += "-DisableHardwareAccess"
    }

    if ($StartMinimized) {
        $argumentParts += "-StartMinimized"
    }

    if ($StartupLite) {
        $argumentParts += "-StartupLite"
    }

    $argumentLine = $argumentParts -join " "
    Write-Host "Requesting elevation (UAC)..."
    Start-Process -FilePath "powershell.exe" -Verb RunAs -ArgumentList $argumentLine | Out-Null
    return
}

if ($EnableHardwareAccess) {
    if (-not $SkipSettingsUpdate) {
        Set-HardwareAccessFlag -FilePath $appSettingsPath -Value $true
    }
}
elseif ($DisableHardwareAccess) {
    if (-not $SkipSettingsUpdate) {
        Set-HardwareAccessFlag -FilePath $appSettingsPath -Value $false
    }
}

Push-Location $repoRoot
try {
    if (-not (Test-Path -LiteralPath $projectPath)) {
        throw "Project file not found: $projectPath"
    }

    if (-not (Test-Path -LiteralPath $solutionPath)) {
        throw "Solution file not found: $solutionPath"
    }

    Stop-RunningFanControlProcesses

    Write-Host "Cleaning presentation app..."
    Invoke-NativeOrThrow `
        -FilePath "dotnet" `
        -Arguments @("clean", $projectPath, "--configuration", $Configuration, "--nologo") `
        -StepName "dotnet clean"

    Write-Host "Building presentation app..."
    Invoke-NativeOrThrow `
        -FilePath "dotnet" `
        -Arguments @("build", $projectPath, "--configuration", $Configuration, "--nologo", "--no-incremental") `
        -StepName "dotnet build"

    $sourceOutputDirectory = Join-Path -Path $repoRoot -ChildPath "src\FanControlPro.Presentation\bin\$Configuration\net8.0"
    $localRunDirectory = Prepare-LocalRunDirectory -SourceDirectory $sourceOutputDirectory -Configuration $Configuration
    $exePath = Join-Path -Path $localRunDirectory -ChildPath "FanControlPro.Presentation.exe"
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Executable not found in staged local directory: $exePath"
    }

    Write-Host "Launching app (local stage): $exePath"
    $appArguments = @()
    if ($StartMinimized) {
        $appArguments += "--start-minimized"
    }
    else {
        $appArguments += "--force-visible"
        $appArguments += "--no-tray"
    }

    if ($StartupLite -or -not $StartMinimized) {
        $appArguments += "--startup-lite"
    }

    $startedProcess = Start-Process `
        -FilePath $exePath `
        -ArgumentList $appArguments `
        -WorkingDirectory (Split-Path -Path $exePath -Parent) `
        -PassThru

    Start-Sleep -Seconds 3
    $startedProcess.Refresh()

    if ($startedProcess.HasExited) {
        Write-Host "FanControl Pro exited immediately (exit code: $($startedProcess.ExitCode))."
        Show-LatestFanControlLogTail
        throw "FanControl Pro process exited right after launch."
    }

    Write-Host "FanControl Pro started. PID: $($startedProcess.Id)"
    Try-BringProcessWindowToFront -Process $startedProcess

    if (-not $StartMinimized -and $startedProcess.MainWindowHandle -eq 0) {
        Write-Host "Main window handle still not available after initial check. Waiting longer..."
        Start-Sleep -Seconds 8
        $startedProcess.Refresh()

        if ($startedProcess.MainWindowHandle -eq 0) {
            Write-Host "No visible main window after extended wait."

            $processDetails = Get-CimInstance Win32_Process -Filter "ProcessId = $($startedProcess.Id)" -ErrorAction SilentlyContinue |
                Select-Object ProcessId, CommandLine

            if ($null -ne $processDetails) {
                Write-Host "Process details:"
                $processDetails | Format-Table -AutoSize
            }

            Show-LatestFanControlLogTail
            throw "FanControl Pro did not expose a main window."
        }

        Try-BringProcessWindowToFront -Process $startedProcess
    }

    Write-Host "If window is still not visible, run:"
    Write-Host "Get-Process FanControlPro.Presentation | Select-Object Id,MainWindowHandle,MainWindowTitle,StartTime"
}
finally {
    Pop-Location
}
