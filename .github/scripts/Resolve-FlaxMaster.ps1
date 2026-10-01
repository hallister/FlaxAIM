#Requires -Version 7.0
# Select only official successful master builds; stable releases lack required editor fixes.
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$api = 'https://api.github.com/repos/FlaxEngine/FlaxEngine'
$headers = @{ Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
if ($env:GITHUB_TOKEN) { $headers.Authorization = "Bearer $env:GITHUB_TOKEN" }

$runs = Invoke-RestMethod "$api/actions/workflows/cd.yml/runs?branch=master&status=success&per_page=1" -Headers $headers
$selected = $null
foreach ($run in $runs.workflow_runs) {
    if ($run.head_branch -ne 'master' -or $run.head_repository.full_name -ne 'FlaxEngine/FlaxEngine') { continue }
    $artifacts = Invoke-RestMethod "$api/actions/runs/$($run.id)/artifacts?name=Windows-Editor&per_page=100" -Headers $headers
    $artifact = $artifacts.artifacts | Where-Object { $_.name -eq 'Windows-Editor' -and !$_.expired } | Select-Object -First 1
    if ($artifact) {
        $selected = @{ Run = $run; Artifact = $artifact }
        break
    }
}
if (!$selected) { throw 'No successful Flax master build with an unexpired Windows-Editor artifact was found. Stable Flax releases are not supported.' }

$run = $selected.Run
$artifact = $selected.Artifact
# This master fix covers editing read-only/scripting-object interface and abstract properties.
# Fail explicitly if the latest available package predates the fixes FlaxAIM requires.
$minimumCommit = 'a370aa29e9cfd9ba45804c5464ad05435dc4d33e'
$comparison = Invoke-RestMethod "$api/compare/$minimumCommit...$($run.head_sha)" -Headers $headers
if ($comparison.status -notin @('ahead', 'identical')) {
    throw "Flax master artifact $($artifact.id) predates the required editor fix $minimumCommit. Wait for a newer upstream master build."
}
Write-Output "Flax master commit: $($run.head_sha)"
Write-Output "Upstream run: $($run.html_url)"
Write-Output "Windows-Editor artifact: $($artifact.id)"
if ($env:GITHUB_OUTPUT) {
    @("run-id=$($run.id)", "artifact-id=$($artifact.id)", "commit=$($run.head_sha)") | Add-Content -LiteralPath $env:GITHUB_OUTPUT
}
if ($env:GITHUB_STEP_SUMMARY) {
    "Flax master: [$($run.head_sha)](https://github.com/FlaxEngine/FlaxEngine/commit/$($run.head_sha)), [upstream build]($($run.html_url)), artifact $($artifact.id)." | Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY
}
