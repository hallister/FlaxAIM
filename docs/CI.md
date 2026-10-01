# Continuous integration

[CI](../.github/workflows/ci.yml) runs on pushes to `main`, version tags, pull requests, and manual dispatches.
The Windows job checks release metadata, builds the plugin and runs NUnit tests, then builds the examples and
runs the in-engine tests headlessly. Unit results (TRX), build output, and engine logs are uploaded even when
tests fail. The workflow has read-only repository permissions and requires no custom secrets or self-hosted runner.

## Flax master is required

FlaxAIM depends on editor fixes for interfaces and abstract classes that are only on Flax's current `master`
branch. A numbered Flax release is not supported until those fixes ship in the next major release.

CI selects the newest successful run of the official
[Flax Continuous Deployment workflow](https://github.com/FlaxEngine/FlaxEngine/actions/workflows/cd.yml)
with an available `Windows-Editor` artifact. It downloads using the built-in `GITHUB_TOKEN` and caches the
extracted engine by the artifact's immutable ID. There is no fallback to stable releases. The selected upstream
commit and run are recorded in the workflow summary, so a failure can be traced to the exact engine build.
The selected commit must include the interface/abstract-property editor fix `a370aa29e`; older artifacts are rejected.
The daily artifact can lag the source branch until the next successful upstream build; rerunning CI can select
a newer engine artifact. If none is available, setup fails explicitly.

.NET 8 is installed for Flax.Build and .NET 10 for the unit test project and current C# language features.
Flax is downloaded into the runner's temporary directory, not committed or installed globally.

## Local equivalent

```powershell
$env:FLAX_ENGINE_PATH = 'C:/Program Files (x86)/Flax/Flax_master'
./Scripts/Release.ps1 -Mode Check
dotnet test Tests/FlaxAIM.Tests --logger 'trx;LogFileName=unit-tests.trx' --results-directory TestResults/Unit
./Scripts/Test-Engine.ps1
```

The engine runner uses `-headless -null -mute`: it opens no editor window and needs no graphics device.
It fails on a build error, nonzero exit code, a timeout, missing test summary, zero executed tests, failures,
or skipped tests. A successful editor startup alone cannot pass the job. Headless tests exercise engine objects,
assets and input behavior; they do not validate rendered output or interactive editor UI.

The job's total timeout is 20 minutes; the editor test process has a separate three-minute timeout.

The Windows editor may leave redirected stdout empty even when all tests pass. The script reads Flax's native
log from `Examples/Logs`, verifies a unique run marker in its command line, and copies it to `TestResults/Engine`.
It never accepts a previous run's log as evidence of success.

Logs are retained for 14 days. Branch protection is separate: select `Unit and engine tests (Flax master)` as
a required check if desired after its first successful run.
