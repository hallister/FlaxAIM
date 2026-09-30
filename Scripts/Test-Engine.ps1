#Requires -Version 7.0
<##
.SYNOPSIS
Build the examples and run their engine tests without a window or graphics device.
##>
[CmdletBinding()]
param(
    [string] $FlaxEnginePath = $env:FLAX_ENGINE_PATH,
    [ValidateRange(10, 600)]
    [int] $TimeoutSeconds = 180
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (!$FlaxEnginePath) { throw 'Set FLAX_ENGINE_PATH or pass -FlaxEnginePath pointing to a Flax master installation.' }
$FlaxEnginePath = (Resolve-Path -LiteralPath $FlaxEnginePath).Path
$repoRoot = Split-Path -Parent $PSScriptRoot
$examples = Join-Path $repoRoot 'Examples'
$results = Join-Path $repoRoot 'TestResults/Engine'
$null = New-Item -ItemType Directory -Path $results -Force
$buildTool = Join-Path $FlaxEnginePath 'Binaries/Tools/Flax.Build.exe'
$editor = Join-Path $FlaxEnginePath 'Binaries/Editor/Win64/Development/FlaxEditor.exe'
foreach ($file in @($buildTool, $editor)) {
    if (!(Test-Path -LiteralPath $file)) { throw "Missing Flax executable: $file" }
}

& $buildTool -build -mutex "-workspace=$examples" -arch=x64 -configuration=Development -platform=Windows -buildtargets=FlaxAIMExamplesEditorTarget 2>&1 | Tee-Object -FilePath (Join-Path $results 'build.log')
if ($LASTEXITCODE -ne 0) { throw "Examples build failed with exit code $LASTEXITCODE." }

# Unique stdout files prevent a previous run's success from masking a failed startup.
$runId = [guid]::NewGuid().ToString('N')
$stdout = Join-Path $results "$runId.stdout.log"
$stderr = Join-Path $results "$runId.stderr.log"
$editorArguments = @('-project', ('"' + $examples + '"'), '-play', 'acaa95464cfc481c8ee1ed907f3bb015', '-flaxaim-tests', '-skipcompile', '-std', '-headless', '-null', '-mute')
$process = Start-Process -FilePath $editor -ArgumentList $editorArguments -WorkingDirectory $examples -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
try {
    if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill($true)
        $process.WaitForExit()
        throw "Engine tests exceeded $TimeoutSeconds seconds. See $stdout and $stderr."
    }
    $process.WaitForExit()
    $exitCode = $process.ExitCode
} finally {
    $process.Dispose()
}

$output = [IO.File]::ReadAllText($stdout)
$output -split '\r?\n' | Where-Object { $_ -match '\[EngineTests\]' } | Write-Output
if ($exitCode -ne 0) { throw "Engine tests exited with code $exitCode. See $stdout and $stderr." }
$summary = [regex]::Match($output, '\[EngineTests\] (\d+) passed, (\d+) failed, (\d+) skipped')
if (!$summary.Success -or [int]$summary.Groups[1].Value -eq 0 -or [int]$summary.Groups[2].Value -ne 0 -or [int]$summary.Groups[3].Value -ne 0) {
    throw "Engine tests did not report a complete passing run. See $stdout and $stderr."
}
Write-Output "Engine tests completed successfully. Logs: $results"
