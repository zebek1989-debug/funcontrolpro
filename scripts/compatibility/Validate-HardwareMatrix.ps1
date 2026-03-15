param(
    [string]$Path = "docs/qa/hardware-matrix.csv",
    [int]$MinFullControl = 3,
    [int]$MinMonitoringOnly = 10,
    [switch]$RequireValidated
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path)) {
    throw "Matrix file not found: $Path"
}

$rows = Import-Csv -LiteralPath $Path

if (-not $rows -or $rows.Count -eq 0) {
    throw "Matrix file is empty: $Path"
}

$requiredColumns = @(
    "ConfigurationId",
    "SupportTarget",
    "ValidationStatus"
)

foreach ($column in $requiredColumns) {
    if (-not $rows[0].PSObject.Properties.Name.Contains($column)) {
        throw "Missing required column '$column' in $Path"
    }
}

$duplicates = $rows |
    Group-Object ConfigurationId |
    Where-Object { $_.Count -gt 1 }

if ($duplicates.Count -gt 0) {
    $duplicateIds = ($duplicates | ForEach-Object { $_.Name }) -join ", "
    throw "Duplicate ConfigurationId values detected: $duplicateIds"
}

$rowsByTarget = $rows | Group-Object SupportTarget -AsHashTable -AsString

$fullRows = @()
$monitoringRows = @()

if ($rowsByTarget.ContainsKey("FullControl")) {
    $fullRows = @($rowsByTarget["FullControl"])
}

if ($rowsByTarget.ContainsKey("MonitoringOnly")) {
    $monitoringRows = @($rowsByTarget["MonitoringOnly"])
}

if ($fullRows.Count -lt $MinFullControl) {
    throw "Not enough FullControl rows. Required: $MinFullControl, found: $($fullRows.Count)"
}

if ($monitoringRows.Count -lt $MinMonitoringOnly) {
    throw "Not enough MonitoringOnly rows. Required: $MinMonitoringOnly, found: $($monitoringRows.Count)"
}

$validatedRows = $rows | Where-Object { $_.ValidationStatus -eq "Validated" }
$validatedFull = @($validatedRows | Where-Object { $_.SupportTarget -eq "FullControl" }).Count
$validatedMonitoring = @($validatedRows | Where-Object { $_.SupportTarget -eq "MonitoringOnly" }).Count

Write-Host "Hardware matrix check:"
Write-Host "  Total rows: $($rows.Count)"
Write-Host "  FullControl rows: $($fullRows.Count)"
Write-Host "  MonitoringOnly rows: $($monitoringRows.Count)"
Write-Host "  Validated FullControl rows: $validatedFull"
Write-Host "  Validated MonitoringOnly rows: $validatedMonitoring"

if ($RequireValidated) {
    if ($validatedFull -lt $MinFullControl) {
        throw "Validated FullControl rows below threshold. Required: $MinFullControl, found: $validatedFull"
    }

    if ($validatedMonitoring -lt $MinMonitoringOnly) {
        throw "Validated MonitoringOnly rows below threshold. Required: $MinMonitoringOnly, found: $validatedMonitoring"
    }
}

Write-Host "Matrix validation passed."
