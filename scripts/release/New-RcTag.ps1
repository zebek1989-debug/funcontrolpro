param(
    [Parameter(Mandatory = $true)]
    [string]$RcVersion,
    [switch]$Push
)

$ErrorActionPreference = "Stop"

if ($RcVersion -notmatch '^\d+\.\d+\.\d+-rc\d+$') {
    throw "RcVersion must match SemVer RC pattern, for example: 1.0.0-rc1"
}

$tagName = "v$RcVersion"

Write-Host "Preparing RC tag: $tagName"

$gitStatus = git status --porcelain
if (-not [string]::IsNullOrWhiteSpace($gitStatus)) {
    throw "Working tree is not clean. Commit or stash changes before creating RC tag."
}

$existingTag = git tag --list $tagName
if (-not [string]::IsNullOrWhiteSpace($existingTag)) {
    throw "Tag already exists locally: $tagName"
}

$existingRemoteTag = git ls-remote --tags origin $tagName
if (-not [string]::IsNullOrWhiteSpace($existingRemoteTag)) {
    throw "Tag already exists on origin: $tagName"
}

git tag -a $tagName -m "release: $tagName"
Write-Host "Created local tag: $tagName"

if ($Push.IsPresent) {
    git push origin $tagName
    Write-Host "Pushed tag to origin: $tagName"
    Write-Host "Release workflow should start automatically for tag events."
}
else {
    Write-Host "Tag created locally. Use 'git push origin $tagName' to publish."
}
