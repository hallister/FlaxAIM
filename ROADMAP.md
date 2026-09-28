# FlaxAIM Roadmap

Findings from a code review of the `FlaxAIM` runtime module (September 2026), grouped into milestones.
Items within each milestone are roughly ordered by severity.

Legend: 🔴 bug that breaks documented behaviour · 🟠 bug in edge cases / design flaw · 🟢 enhancement

---

## Milestone 1 — Correctness fixes

The compiler now builds a runtime plan in which every binding has its own Flax virtual input, target component, modifiers and cloned triggers. `InputManager` evaluates that plan once per action per frame. Most of the fixes below follow from that change.

### State machine & triggers

- [x] 🔴 **`Triggered` never fired for `TriggerPressed`, `TriggerChord`, or the base `InputTrigger`.** Fixed: the tracker now follows Unreal's transition table and raises a set of `TriggerEvent` flags per frame. `None → Triggered` raises `Started | Triggered`, and callbacks are dispatched in the order Started, Ongoing, Triggered, Completed, Canceled. The base `InputTrigger` is now a "Down" trigger.
  - ⚠️ Behaviour change: `GetActionState` now returns the trigger state (`None`/`Ongoing`/`Triggered`). Use the new `GetActionEvents` for transitions.
- [x] 🔴 **Trigger state was stored on shared asset objects.** Fixed: each compile clones the triggers (`InputTrigger.CreateInstance`, which calls `Reset`), so every manager and binding has its own instances.
- [x] 🔴 **The trigger cache key collided across contexts and was never invalidated.** Fixed: the cache is gone. Triggers live in the compiled plan, which is rebuilt whenever the contexts change.
- [x] 🔴 **Per-binding triggers were evaluated against the whole action's magnitude.** Fixed: each binding's triggers see only that binding's magnitude, and the action takes the highest binding state. Within a binding, triggers now use explicit/implicit semantics: any explicit trigger (or actuation, if the binding has none) plus every implicit trigger.
- [x] 🟠 **`TriggerChord` depended on evaluation order.** Fixed: chord actions are topologically sorted ahead of the actions that depend on them, and a cycle logs a warning. `TriggerChord` is now implicit and accepts a chord that is `Triggered`, or also `Ongoing` if `RequiredChordState` is Started or Ongoing.
- [x] 🟠 **Hard-coded threshold mismatch.** Fixed: the no-trigger threshold is now `InputManager.DefaultActuationThreshold`, which defaults to 0.1 as before and can be set per manager. Triggers keep their own `ActuationThreshold`.

### Input gathering & compilation

- [x] 🔴 **Digital actions kept only the last binding row.** Fixed: every row compiles to its own virtual input.
- [x] 🔴 **Axis1D values were multiplied by the number of bindings.** Fixed: each binding reads its own input and applies its own modifiers. Bindings are combined by taking the highest absolute value per component, as in Unreal's default.
- [x] 🔴 **Axis3D did not work.** Fixed: Z bindings compile and are read, and 3D modifiers are applied.
- [x] 🔴 **Axis2D supported exactly two rows.** Fixed: `InputMappingEntry.Target` (Auto/X/Y/Z) selects the component a binding drives, so any number of rows can drive each axis. `Auto` keeps the old row-based layout, so existing assets still work. Also, non-axis (key/button) rows now contribute 1.0 to any action type.
- [x] 🟠 **`AddInputContext` / `RemoveInputContext` stopped at the first skipped entry.** Fixed.
- [x] 🟠 **Removing a context left its actions stuck.** Fixed: actions that aren't evaluated in a frame get a final `None` evaluation, which raises `Completed`/`Canceled`, and are dropped once idle.
- [x] 🟠 **An action in two contexts advanced its state machine twice.** Fixed: an action's bindings from every context are merged, and the action is evaluated once per frame.
  - ⚠️ Behaviour change: a higher context's bindings no longer hide a lower context's bindings for the same action; they're combined. Real input consumption is in Milestone 2.

### Modifiers

- [x] 🔴 **`ModifierDeadZone` did nothing for Axis1D and Axis2D.** Fixed: it now supports 1D, plus 2D and 3D in `Radial` (default) or `Axial` mode, and remaps `[Lower, Upper]` to `[0, 1]`. It no longer logs a warning every frame.
  - ⚠️ Behaviour change: values above `UpperThreshold` now become 1.0 instead of being clamped to `UpperThreshold`.
- [x] 🟠 **`InputModifier` threw `NotImplementedException` for any overload that wasn't overridden.** Fixed: an overload that isn't overridden forwards up a dimension (float → Float2 → Float3), and Float3 passes the value through unchanged.
- [x] 🟠 **Radial processing of a stick was per-axis.** Fixed in Milestone 2: put the dead zone in the action's own `Modifiers`, which see the combined 2D value. A binding that reads both axes of a stick in one row would still be a nice addition.

### Bindings & dispatch

- [x] 🔴 **Changing bindings or contexts from inside a callback crashed.** Fixed: handlers are snapshotted before dispatch using a pooled array, and context changes made during `OnUpdate` are compiled after the frame's evaluation.
- [x] 🟠 **`GetActionValue(Tag)` depended on script update order.** Fixed: `BindAction` records tag → action, and the value is read from the action's tracker.
- [x] 🟠 **Actions were identified by object reference.** Fixed: bindings and trackers are keyed by `InputAction.ID`, which survives an asset reload. The compiler logs an error when two assets share an ID; see the Milestone 2 item on duplicated IDs.
- [x] 🟠 **`UnbindAction` removed every handler that used the callback, whatever its tag.** Fixed: a new `UnbindAction(action, state, callback, tag)` overload removes only that tag, and the original overload is documented as removing the callback for every tag.

---

## Milestone 2 — Architecture & API

- [x] 🟠 **Duplicated `InputAction` assets shared an `ID`.** Fixed: actions are now identified by their asset's ID ([`ActionIdentity`](Source/FlaxAIM/ActionIdentity.cs)). The compiler records it from the asset reference, and other lookups find the owning asset once and cache the result. The serialized `ID` is only a fallback for actions created at runtime.
- [x] 🟠 **Global engine state.** Fixed: [`VirtualInputRegistry`](Source/FlaxAIM/VirtualInputRegistry.cs) captures the project's own mappings and publishes them together with every active manager's virtual inputs. Each manager's inputs are prefixed with `AIM:<managerId>:`, so managers never collide. A manager unregisters in `OnDisable`/`OnDestroy`, and the original mappings are restored once the last one is gone.
  - Reading raw devices directly is still an option for later, but it's no longer needed.
- [x] 🟢 **Real context priority and input consumption.** Done: `AddInputContext(context, priority)` sorts contexts by priority, and ties go to the most recently added. `InputAction.ConsumeInput` (on by default, as in Unreal) makes an action's keys, buttons and axes unavailable to lower-priority contexts. Consumption is worked out at compile time. Also added: `HasInputContext`, `ClearInputContexts`, and `RebuildMappings`.
  - ⚠️ Behaviour change: a key mapped in two contexts now only fires in the higher one, unless that action's `ConsumeInput` is off.
- [x] 🟢 **Local multiplayer.** Done: each `InputManager` has a `Gamepad` index (default `All`) and a `UseKeyboardAndMouse` toggle.
- [x] 🟢 **Bind through `InputConfig`.** Done: `BindAction(InputConfig, state, callback)`, plus a new `InputConfigSet` asset (New → Adaptive Input → Input Config Set) bound with `BindActions(set, state, callback)`.
- [x] 🟢 **Native binding overloads without tags.** Done: `BindAction(action, state, Action)`, `BindAction(action, state, Action<ProcessedInputActionValue>)` and `BindAction<T>` for `bool`, `float`, `Float2` and `Float3`. For a method group, pass the type argument explicitly: `BindAction<Float2>(Move, state, OnMove)`.
- [x] 🟢 **Lifetime-safe bindings.** Done: every `BindAction` returns an `InputBindingHandle` that you can dispose. `UnbindAll(owner)` removes every callback owned by an object, and callbacks whose owner is a destroyed Flax object are dropped automatically.
- [ ] 🟢 **Remove the `IInputTrigger` workaround.** Reference the `InputTrigger` base type directly if Flax's editor can draw it inline, and make it `abstract` (README todo #3). *Not attempted:* this depends on how Flax's editor draws polymorphic lists, so it needs testing in the editor.
- [x] 🟢 **Action-level triggers and modifiers.** Done: `InputAction.Modifiers` are applied to the combined value after the bindings' own modifiers. `InputAction.Triggers` are evaluated against the combined magnitude, and both they and the binding triggers must pass.
- [x] 🟢 **Expose the `AxisConfig` fields that were fixed.** Done: axis bindings now have `AxisDeadZone`, `AxisSensitivity`, `AxisGravity`, `AxisScale` and `AxisSnap`. The defaults match the old hard-coded values.

---

## Milestone 3 — Features

- [ ] 🟢 More triggers: `Released`, `Tap`, `Pulse`, `Combo`, and a `ChordBlocker` (so Shift+W doesn't also fire W); a one-shot option for `Hold` (README todo #2).
- [ ] 🟢 More modifiers: `Negate`, `Swizzle` (YXZ…), `Smooth`, a response curve (exponential or a user curve), `FOV scaling`, and a proper radial `DeadZone` (README todo #2).
- [ ] 🟢 Runtime key rebinding with saving/loading of player overrides (a "player mappable" flag on bindings).
- [ ] 🟢 Current-device detection and a device-changed event, for UI button glyphs.
- [ ] 🟢 Input injection (`InjectInput(action, value)`) for AI, replays, and automated tests.
- [ ] 🟢 Convenience queries: `WasTriggeredThisFrame(action)`, `IsActive(action)`, `GetElapsedTime(action)`.
- [ ] 🟢 Debug overlay (ImGui or Flax UI) showing the context stack, action states, values, and trigger timers (README todo #4).

---

## Milestone 4 — Code health, performance, docs

- [x] 🟠 **README sample didn't work.** Fixed: the Getting Started snippet uses the current API (`AddInputContext`, typed `BindAction`, `UnbindAll`) and the right menu entry, and points to the examples project.
- [x] 🟢 **Per-frame allocations.** Fixed in Milestone 1: virtual names are precomputed at compile time, and callback dispatch uses a pooled array.
- [x] 🟢 **Log noise.** Fixed: `BindAction` no longer logs, `Compile` logs one summary line, and every message goes through `InputLog` with a `[FlaxAIM]` prefix (tests capture it via `InputLog.Output`).
- [x] 🟢 **The missing-triggers error repeated every frame.** Fixed in Milestone 1: triggers are validated once, at compile time.
- [ ] 🟢 **Dead code.** `TriggerConfig`, `ActionStateTracker.InputTag`, the `MyPluginEditor._button` that is never created, and the commented-out debug logs.
- [ ] 🟢 **Naming consistency.**
  - `EnhancedInputActionState` becomes `InputActionState` (the file is already named that way).
  - `MyPlugin`/`MyPluginEditor` become `FlaxAIMPlugin`/`FlaxAIMEditorPlugin`.
  - `ModifierDeadzone.cs` becomes `ModifierDeadZone.cs`, and `ScaleModifier.cs` becomes `ModifierScale.cs`.
  - The plugin display name "Flax Adaptive Input" should match "FlaxAIM".
- [ ] 🟢 **Mutable static value.** Make `ProcessedInputActionValue.Default` `static readonly`, and move the type out of the `Modifiers` namespace.
- [x] 🟢 **Tag comparison.** Fixed: `Tag.Default` is used throughout.
- [x] 🟢 **Unit tests.** Done: `Tests/FlaxAIM.Tests` (NUnit, `dotnet test`, no engine), modelled on FlaxACE's. The core moved into the engine-independent `InputProcessor`, and replaceable hooks for logging, the virtual input backend and the asset lookup let tests drive it against simulated devices. Engine tests in the examples project cover what needs the engine (see [docs/EngineTests.md](docs/EngineTests.md)).
  - ⚠️ Breaking change: `InputTrigger.UpdateState` takes an `InputProcessor` instead of an `InputManager`.
- [x] 🟢 **Samples.** Done: `Examples/` is a separate Flax project (modelled on FlaxACE's) with a demo scene covering a character controller, a pause menu context that consumes keys, a chord, a hold, and a HUD of action states.
- [ ] 🟢 **XML docs on the public API.** The core types are documented; triggers, modifiers and the data types (`InputMappingEntry`, `InputActionEntry`, `ProcessedInputActionValue`) still need a pass.
- [x] 🟢 **Contexts built in code.** Done: `InputActionEntry.RuntimeAction` and `TriggerChord.RuntimeChordAction` accept actions that aren't assets (not serialized). Tests use them, and games can build contexts procedurally.
