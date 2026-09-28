# In-engine tests

The unit tests in `Tests/FlaxAIM.Tests` run without the engine, so they replace Flax's virtual input system with
simulated devices and build actions and contexts in code. The engine tests in the examples project
(`Examples/Source/FlaxAIMExamples/Samples/EngineTests/`) run in play mode against real engine objects and cover:

- publishing to Flax's real `Input.ActionMappings`/`AxisMappings`: the project's own mappings kept and restored,
  several managers side by side, device settings (gamepad index, axis settings) and consumed keys
- actions, contexts, chords and input config sets as JSON assets, including duplicated action assets (same serialized
  ID, different asset IDs) and finding an action's asset when it's bound before its context is added
- the `InputManager` script in a scene: updated by the engine, disabled, destroyed, and dropping callbacks owned by
  destroyed scripts
- the demo's input assets (`Content/Input`): they load with their triggers and modifiers, and the controls behave as the
  demo describes

They build their actions and contexts as virtual (runtime-only) JSON assets and create their tags under `FlaxAIMTest.`
at runtime, so they need no content except the test scene (and the demo assets for `DemoContentEngineTests`).

## Running

**In the editor:** open `Examples/FlaxAIMExamples.flaxproj`, then `Content/Tests/EngineTests.scene`, and press Play.
Results are shown on screen and in the Output Log (`[EngineTests] PASS/FAIL/SKIP ...`). F6 runs them again. Set
**Filter** on the `EngineTestRunner` script to run only matching tests (e.g. `Asset`).

**From the command line** (the editor opens, runs the tests, and exits with the number of failures as its exit code):

```bash
"<Flax>/Binaries/Editor/Win64/Development/FlaxEditor.exe" -project "<path to FlaxAIM>/Examples" -play acaa95464cfc481c8ee1ed907f3bb015 -flaxaim-tests -skipcompile -mute
```

`acaa9546…` is the test scene's ID. Build first or drop `-skipcompile`:

```bash
"<Flax>/Binaries/Tools/Flax.Build.exe" -build -mutex -workspace="<path to FlaxAIM>/Examples" -arch=x64 -configuration=Development -platform=Windows -buildtargets=FlaxAIMExamplesEditorTarget
```

The tests use FlaxAIM internals, which `Source/FlaxAIM/AssemblyInfo.cs` exposes to the `FlaxAIMExamples.CSharp`
assembly. The examples editor plugin (`FlaxAIMExamplesEditor`) exits the editor after a command-line run. Results are
in the newest file in `Examples/Logs/`. A cooked build with the test scene as its first scene also accepts
`-flaxaim-tests`.

## Writing tests

Add a class deriving from `EngineTestFixture` with `[EngineTest]` methods. A new fixture instance runs each test,
and whatever its helpers create is destroyed afterwards.

- `CreateManager(...)` makes an actor with an `InputManager`. By default it isn't in a scene and is enabled directly,
  so nothing updates it: advance it with `Tick(manager)`. Pass `inScene: true` to have the engine enable and update
  it, and wait a frame first.
- `UseScriptedInput()` replaces Flax's virtual input system with scripted keys and buttons for the rest of the test,
  since tests can't press real keys. Call it before creating managers.
- `Asset(instance)` wraps an action, context or config in a virtual JSON asset; `ActionAsset(name, type)` makes an
  action asset. `Map`, `Key`, `Button` and `Context` build contexts that reference them.
- `Check.That`, `Check.Equal`, `Check.Near`, `Check.NotNull` and `Check.Skip` for assertions.
- Return `IEnumerator` for a test that spans frames: `yield return null` waits a frame, `yield return 0.5f` waits
  half a second, and yielding another `IEnumerator` runs it first.
- FlaxAIM's log output during a test is in `Logs` and is shown when the test fails.
- Asset IDs written as 32 hex characters (as in content files) must be parsed with `JsonSerializer.ParseID`, not
  `Guid.Parse`: Flax orders the bytes differently.
