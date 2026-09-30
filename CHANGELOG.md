# Changelog

Development releases use tags such as `v0.1` and `v0.2`. During `0.x`, incompatible changes increment
the minor version; compatible additions and fixes use a patch version (for example `v0.2.1`). See [Releasing](docs/Releasing.md).

## [0.2] - 2026-09-30

### Breaking changes

- Replace `InputActionEntry` with `InputActionMapping`, and its `InputMapping` list with `Inputs`.
- Each `InputMappingEntry` now represents one control selected by `Control`. Replace `UseAxis` and `AxisType`
  with the appropriate `InputControl`, and move axis tuning into `AxisSettings`.
- Remove `InputAxisTarget.Auto`. Set `X`, `Y`, or `Z` explicitly for one-dimensional inputs. Two-dimensional
  controls can fill both components directly. Serialized target numbers have changed; rebuild bindings using
  named controls and targets rather than copying numeric enum values.
- Existing v0.1 IMCs require a one-time manual update. The previous asset format and C# API are not supported.

### Added

- Device and control picker, including directional keys, mouse controls, sticks, D-pad and gamepad triggers.
- Independent modifiers and triggers for each physical input.
- An authoritative `VERSION` file and a script to synchronize, check and tag releases.

### One-time IMC update from v0.1

1. Before updating the plugin, commit or back up the IMCs and record their keys, buttons, axis settings,
   modifiers and triggers. Existing InputAction assets can be retained.
2. Update to v0.2 and open each existing IMC. Its action references remain, but old bindings do not populate
   the new `Inputs` lists. Rebuild those lists using the Device / Control picker. Editing the existing IMC
   preserves its asset ID and references from scenes and scripts.
3. Split combined keyboard/gamepad bindings into separate inputs and reapply modifiers and triggers to each.
   Use `Key Axis` for a positive/negative pair, or `Directional Keys` for WASD. Set one-dimensional targets
   explicitly and restore axis settings. Add sticks and other controls as separate inputs.
4. Save each IMC after rebuilding its bindings and verify gameplay and menu input. The examples IMCs already
   use the new format.

There is no automatic asset conversion or backward compatibility layer. Do not save an old IMC in v0.2
before recording its bindings: unknown legacy fields are discarded on save. Reverting to v0.1 requires
restoring the backed-up IMCs along with the plugin.

For contexts built in C#, replace code like:

```csharp
new InputActionEntry
{
    RuntimeAction = jump,
    InputMapping = [new InputMappingEntry { Key = KeyboardKeys.Spacebar, GamepadButton = GamepadButton.A }],
}
```

with:

```csharp
new InputActionMapping
{
    RuntimeAction = jump,
    Inputs =
    [
        new InputMappingEntry { Control = InputControl.Key, Key = KeyboardKeys.Spacebar },
        new InputMappingEntry { Control = InputControl.GamepadButton, GamepadButton = GamepadButton.A },
    ],
}
```

For a keyboard axis use `Control = InputControl.KeyAxis`, retain `KeyPositive`/`KeyNegative`, set `Target`
explicitly, and replace fields such as `AxisDeadZone` with `AxisSettings = new InputAxisSettings { DeadZone = ... }`.
For a stick use `InputControl.LeftStick` or `RightStick`; use `Axes = InputControlAxes.X` or `Y` to read a single axis,
or the default `XY` for both.

## [0.1] - Baseline

The pre-change `origin/main` commit `3adddeb` is the v0.1 baseline. It retains the original combined bindings
and template `1.0` plugin metadata. The annotated `v0.1` tag identifies this historical version without
rewriting its source metadata.
