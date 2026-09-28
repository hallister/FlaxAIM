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
- [ ] 🟠 **Radial processing of a stick is still per-axis.** Flax's `AxisConfig` is 1D, so a stick is still two bindings (X and Y), and each binding's modifiers only see its own component. A radial dead zone on those bindings therefore acts per-axis. **Fix:** add 2D stick bindings (one row reads both axes), or action-level modifiers (Milestone 2).

### Bindings & dispatch

- [x] 🔴 **Changing bindings or contexts from inside a callback crashed.** Fixed: handlers are snapshotted before dispatch using a pooled array, and context changes made during `OnUpdate` are compiled after the frame's evaluation.
- [x] 🟠 **`GetActionValue(Tag)` depended on script update order.** Fixed: `BindAction` records tag → action, and the value is read from the action's tracker.
- [x] 🟠 **Actions were identified by object reference.** Fixed: bindings and trackers are keyed by `InputAction.ID`, which survives an asset reload. The compiler logs an error when two assets share an ID; see the Milestone 2 item on duplicated IDs.
- [x] 🟠 **`UnbindAction` removed every handler that used the callback, whatever its tag.** Fixed: a new `UnbindAction(action, state, callback, tag)` overload removes only that tag, and the original overload is documented as removing the callback for every tag.

---

## Milestone 2 — Architecture & API

- [ ] 🟠 **Duplicated `InputAction` assets share an `ID`.** Virtual inputs are no longer keyed by `Name` (they now have unique per-binding names), and bindings and trackers use `InputAction.ID`. But the serialized `ID` is copied when an asset is duplicated. The compiler detects this and logs an error. **Fix:** derive the identity from the asset GUID, or regenerate `ID` on duplicate in the editor.
- [ ] 🟠 **Global engine state.** `InputMappingCompiler.Compile` overwrites `Input.ActionMappings`/`AxisMappings`:
  - It destroys the project's own `GameSettings` input mappings.
  - When several `InputManager`s exist, the last one to compile wins.
  - Nothing is restored in `OnDestroy`.

  **Fix:** save and restore the original mappings. Longer term, read raw devices (`Input.GetKey`, `Input.GetGamepadAxis`, …) directly instead of going through Flax's virtual-input tables.
- [ ] 🟢 **Real context priority and input consumption.** Priority is currently just stack order, and every context compiles into one global table, so a key mapped in both a high and a low context fires both. Add an explicit `Priority` value on `AddInputContext`, plus per-binding "consume input" so higher contexts block lower ones.
- [ ] 🟢 **Local multiplayer.** `Gamepad = InputGamepadIndex.All` is hard-coded. Give each `InputManager` its own player/device assignment.
- [ ] 🟢 **Bind through `InputConfig`.** `InputConfig` (action ↔ tag) exists, but no API consumes it yet. Add `BindAction(InputConfig, state, callback)`, plus a data-driven "input config set" asset that binds many actions at once.
- [ ] 🟢 **Native binding overloads without tags.** Add `BindAction(action, state, Action<ProcessedInputActionValue>)` and a generic typed variant, so callers get the value directly (README todo #1).
- [ ] 🟢 **Lifetime-safe bindings.** Add `UnbindAll(object owner)` and return a handle/`IDisposable` from `BindAction`, so destroyed scripts don't keep receiving callbacks.
- [ ] 🟢 **Remove the `IInputTrigger` workaround.** Reference the `InputTrigger` base type directly if Flax's editor can draw it inline, and make it `abstract` (README todo #3).
- [ ] 🟢 **Action-level triggers and modifiers.** Resolve the `@todo` on `InputAction` by applying them to every binding of the action.
- [ ] 🟢 **Expose the `AxisConfig` fields that are currently fixed.** `DeadZone = 0.1`, `Sensitivity = 1`, and `Scale = 1` are set in [`CreateNativeAxisConfig`](Source/FlaxAIM/InputMappingCompiler.cs#L113) and stack on top of any modifiers.

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

- [ ] 🟠 **README sample doesn't work.** It assigns `InputSystem = …` but declares `InputManager`, and it never calls `AddInputContext`, so nothing fires. It also refers to a "Input Mapping Context" menu entry, while the code registers `New/Adaptive Input/Input Mapping`.
- [x] 🟢 **Per-frame allocations.** Fixed in Milestone 1: virtual names are precomputed at compile time, and callback dispatch uses a pooled array.
- [ ] 🟢 **Log noise.** `BindAction` logs on every call (`Compile` now logs a single summary line). Put these logs behind a `Verbose` flag and use one consistent prefix (there are currently `[EnhancedInputService]`, `[EnhancedInput]`, and `[InputManager]`).
- [x] 🟢 **The missing-triggers error repeated every frame.** Fixed in Milestone 1: triggers are validated once, at compile time.
- [ ] 🟢 **Dead code.** `TriggerConfig`, `ActionStateTracker.InputTag`, the `MyPluginEditor._button` that is never created, and the commented-out debug logs.
- [ ] 🟢 **Naming consistency.**
  - `EnhancedInputActionState` becomes `InputActionState` (the file is already named that way).
  - `MyPlugin`/`MyPluginEditor` become `FlaxAIMPlugin`/`FlaxAIMEditorPlugin`.
  - `ModifierDeadzone.cs` becomes `ModifierDeadZone.cs`, and `ScaleModifier.cs` becomes `ModifierScale.cs`.
  - The plugin display name "Flax Adaptive Input" should match "FlaxAIM".
- [ ] 🟢 **Mutable static value.** Make `ProcessedInputActionValue.Default` `static readonly`, and move the type out of the `Modifiers` namespace.
- [ ] 🟢 **Tag comparison.** Use `Tag.Default` consistently rather than `new Tag()`.
- [ ] 🟢 **Unit tests.** Pull the pure logic (state machine, triggers, modifiers, compiler) out of Flax dependencies where you can, and cover it with a plain .NET test project. Most of the Milestone 1 bugs would have been caught this way.
- [ ] 🟢 **XML docs on the public API**, plus a samples folder: a character controller, a menu context push/pop, and a chord.
