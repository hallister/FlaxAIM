#Requires -Version 5.1
<#
.SYNOPSIS
Synchronize release metadata, check it, or create an annotated local release tag.
.EXAMPLE
./Scripts/Release.ps1 -Mode Sync
.EXAMPLE
./Scripts/Release.ps1 -Mode Check
.EXAMPLE
./Scripts/Release.ps1 -Mode Tag
#>
[CmdletBinding()]
param(
    [ValidateSet('Check', 'Sync', 'Tag')]
    [string] $Mode = 'Check'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$version = [IO.File]::ReadAllText((Join-Path $repoRoot 'VERSION')).Trim()
if ($version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(\.(0|[1-9][0-9]*))?$') {
    throw 'VERSION must contain a numeric MAJOR.MINOR or MAJOR.MINOR.PATCH version without leading zeros.'
}
$parsedVersion = [Version]::Parse($version)
$pluginVersion = if ($parsedVersion.Build -lt 0) {
    'Version = new Version({0}, {1}),' -f $parsedVersion.Major, $parsedVersion.Minor
} else {
    'Version = new Version({0}, {1}, {2}),' -f $parsedVersion.Major, $parsedVersion.Minor, $parsedVersion.Build
}
$targets = @(
    @{ Path = 'FlaxAIM.flaxproj'; Pattern = '"Version"\s*:\s*"[^"]+"'; Replacement = '"Version": "' + $version + '"' },
    @{ Path = 'Examples/FlaxAIMExamples.flaxproj'; Pattern = '"Version"\s*:\s*"[^"]+"'; Replacement = '"Version": "' + $version + '"' },
    @{ Path = 'Source/FlaxAIM/MyPlugin.cs'; Pattern = 'Version\s*=\s*new Version\([^\r\n]*\),'; Replacement = $pluginVersion }
)

# Validate every target before writing any file. Preserve all unrelated content and line endings.
$updates = foreach ($target in $targets) {
    $path = Join-Path $repoRoot $target.Path
    $content = [IO.File]::ReadAllText($path)
    $pattern = [regex]::new($target.Pattern)
    if ($pattern.Matches($content).Count -ne 1) {
        throw "Expected exactly one version declaration in $($target.Path)."
    }
    $updated = $pattern.Replace($content, $target.Replacement)
    if ($content -cne $updated) {
        [pscustomobject]@{ Path = $path; RelativePath = $target.Path; Content = $updated }
    }
}

if ($Mode -eq 'Sync') {
    foreach ($update in $updates) {
        [IO.File]::WriteAllText($update.Path, $update.Content, [Text.UTF8Encoding]::new($false))
        Write-Output "Updated $($update.RelativePath)"
    }
    Write-Output "Release metadata synchronized to $version. Update CHANGELOG.md before checking or tagging."
    return
}

if (@($updates).Count -gt 0) {
    throw "Release metadata differs from VERSION ($version). Run ./Scripts/Release.ps1 -Mode Sync."
}
$changelog = [IO.File]::ReadAllText((Join-Path $repoRoot 'CHANGELOG.md'))
$headingPattern = '(?m)^## \[' + [regex]::Escape($version) + '\] - (Unreleased|[0-9]{4}-[0-9]{2}-[0-9]{2})\r?$'
$headings = [regex]::Matches($changelog, $headingPattern)
if ($headings.Count -ne 1) {
    throw "CHANGELOG.md must contain exactly one '## [$version] - Unreleased' or dated release heading."
}
Write-Output "Release metadata and changelog match $version."
if ($Mode -eq 'Check') { return }

$releaseDate = $headings[0].Groups[1].Value
if ($releaseDate -eq 'Unreleased') { throw 'Date the changelog entry before tagging a release.' }
$null = [DateTime]::ParseExact($releaseDate, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)

function Invoke-ReleaseGit {
    param([string[]] $GitArguments)
    $result = & git -C $repoRoot @GitArguments
    if ($LASTEXITCODE -ne 0) { throw "git $($GitArguments -join ' ') failed with exit code $LASTEXITCODE." }
    return $result
}

$status = Invoke-ReleaseGit -GitArguments @('status', '--porcelain', '--untracked-files=all')
if ($status) { throw 'Commit or otherwise account for all working tree changes before tagging.' }
$tag = "v$version"
$existing = Invoke-ReleaseGit -GitArguments @('tag', '--list', $tag)
if ($existing) { throw "Tag $tag already exists; released versions must not be moved or reused." }
$null = Invoke-ReleaseGit -GitArguments @('tag', '-a', $tag, '-m', "FlaxAIM $version")
Write-Output "Created local annotated tag $tag at HEAD. Nothing was pushed."
