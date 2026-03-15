param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,
    [string]$OutputFile = "installer/wix/PublishedFiles.wxs"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $PublishDir)) {
    throw "Publish directory does not exist: $PublishDir"
}

$publishFullPath = (Resolve-Path -LiteralPath $PublishDir).Path
$outputFullPath = [System.IO.Path]::GetFullPath($OutputFile)
$outputDirectory = Split-Path -Parent $outputFullPath
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

function New-StableId {
    param(
        [string]$Prefix,
        [string]$Value
    )

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
    $sha = [System.Security.Cryptography.SHA1]::Create()
    try {
        $hash = $sha.ComputeHash($bytes)
    }
    finally {
        $sha.Dispose()
    }

    $hex = [System.BitConverter]::ToString($hash).Replace("-", "").Substring(0, 20)
    return "{0}_{1}" -f $Prefix, $hex
}

function Escape-Xml {
    param([string]$Value)
    return [System.Security.SecurityElement]::Escape($Value)
}

$files = Get-ChildItem -LiteralPath $publishFullPath -Recurse -File | Sort-Object FullName
if ($files.Count -eq 0) {
    throw "No files found in publish directory: $publishFullPath"
}

$directoryIds = @{}
$directoryIds[""] = "INSTALLFOLDER"
$childDirectoryNamesByParent = @{}

foreach ($file in $files) {
    $relativePath = [System.IO.Path]::GetRelativePath($publishFullPath, $file.FullName)
    $relativePath = $relativePath.Replace("\", "/")
    $directoryPath = [System.IO.Path]::GetDirectoryName($relativePath)

    if ([string]::IsNullOrWhiteSpace($directoryPath)) {
        continue
    }

    $segments = $directoryPath.Replace("\", "/").Split("/", [System.StringSplitOptions]::RemoveEmptyEntries)
    $current = ""

    foreach ($segment in $segments) {
        $next = if ([string]::IsNullOrWhiteSpace($current)) { $segment } else { "$current/$segment" }

        if (-not $directoryIds.ContainsKey($next)) {
            $directoryIds[$next] = New-StableId -Prefix "DIR" -Value $next
        }

        if (-not $childDirectoryNamesByParent.ContainsKey($current)) {
            $childDirectoryNamesByParent[$current] = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
        }

        [void]$childDirectoryNamesByParent[$current].Add($segment)
        $current = $next
    }
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('<?xml version="1.0" encoding="utf-8"?>')
$lines.Add('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
$lines.Add('  <Fragment>')
$lines.Add('    <DirectoryRef Id="INSTALLFOLDER">')

function Emit-DirectoryTree {
    param(
        [string]$ParentPath,
        [int]$Indent
    )

    if (-not $childDirectoryNamesByParent.ContainsKey($ParentPath)) {
        return
    }

    $children = @($childDirectoryNamesByParent[$ParentPath]) | Sort-Object
    foreach ($childName in $children) {
        $childPath = if ([string]::IsNullOrWhiteSpace($ParentPath)) { $childName } else { "$ParentPath/$childName" }
        $childId = $directoryIds[$childPath]
        $indentText = (" " * $Indent)
        $escapedName = Escape-Xml -Value $childName

        $lines.Add("$indentText<Directory Id=""$childId"" Name=""$escapedName"">")
        Emit-DirectoryTree -ParentPath $childPath -Indent ($Indent + 2)
        $lines.Add("$indentText</Directory>")
    }
}

Emit-DirectoryTree -ParentPath "" -Indent 6

$lines.Add('    </DirectoryRef>')
$lines.Add('  </Fragment>')
$lines.Add('')
$lines.Add('  <Fragment>')
$lines.Add('    <ComponentGroup Id="PublishedFilesGroup">')

$componentIds = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
$fileIds = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)

foreach ($file in $files) {
    $relativePath = [System.IO.Path]::GetRelativePath($publishFullPath, $file.FullName).Replace("\", "/")
    $directoryPath = [System.IO.Path]::GetDirectoryName($relativePath)
    $directoryKey = if ([string]::IsNullOrWhiteSpace($directoryPath)) { "" } else { $directoryPath.Replace("\", "/") }
    $directoryId = $directoryIds[$directoryKey]

    $componentId = New-StableId -Prefix "CMP" -Value $relativePath
    $fileId = New-StableId -Prefix "FIL" -Value $relativePath

    if (-not $componentIds.Add($componentId)) {
        throw "Duplicate component id generated for '$relativePath'."
    }

    if (-not $fileIds.Add($fileId)) {
        throw "Duplicate file id generated for '$relativePath'."
    }

    $source = Escape-Xml -Value $file.FullName
    $lines.Add("      <Component Id=""$componentId"" Directory=""$directoryId"" Guid=""*"">")
    $lines.Add("        <File Id=""$fileId"" Source=""$source"" KeyPath=""yes"" />")
    $lines.Add('      </Component>')
}

$lines.Add('    </ComponentGroup>')
$lines.Add('  </Fragment>')
$lines.Add('</Wix>')

[System.IO.File]::WriteAllLines($outputFullPath, $lines, [System.Text.Encoding]::UTF8)
Write-Host "Generated WiX manifest: $outputFullPath"
Write-Host "Included files: $($files.Count)"
