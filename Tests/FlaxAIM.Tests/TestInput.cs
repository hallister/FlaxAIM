using FlaxAIM.Modifiers;
using FlaxAIM.Triggers;
using FlaxEngine;

namespace FlaxAIM.Tests;

/// <summary>
/// Builders for actions, bindings and contexts made in code (no assets).
/// </summary>
internal static class TestInput
{
    public static InputAction Action(string name, InputActionType type = InputActionType.Digital) => new()
    {
        Name = name,
        ActionType = type,
    };

    public static InputMappingEntry Key(KeyboardKeys key, params InputTrigger[] triggers) => new()
    {
        Control = InputControl.Key,
        Key = key,
        Triggers = [..triggers],
    };

    public static InputMappingEntry Button(GamepadButton button, params InputTrigger[] triggers) => new()
    {
        Control = InputControl.GamepadButton,
        GamepadButton = button,
        Triggers = [..triggers],
    };

    public static InputMappingEntry Mouse(MouseButton button, params InputTrigger[] triggers) => new()
    {
        Control = InputControl.MouseButton,
        MouseButton = button,
        Triggers = [..triggers],
    };

    /// <summary>
    /// A keyboard axis: <paramref name="positive"/> gives +1, <paramref name="negative"/> gives -1.
    /// </summary>
    public static InputMappingEntry Keys(KeyboardKeys positive, KeyboardKeys negative, InputAxisTarget target = InputAxisTarget.X) => new()
    {
        Control = InputControl.KeyAxis,
        KeyPositive = positive,
        KeyNegative = negative,
        Target = target,
        AxisSettings = new InputAxisSettings { DeadZone = 0f },
    };

    /// <summary>
    /// Up/down/left/right keys driving X and Y.
    /// </summary>
    public static InputMappingEntry DirectionalKeys(KeyboardKeys up, KeyboardKeys down, KeyboardKeys left, KeyboardKeys right) => new()
    {
        Control = InputControl.DirectionalKeys,
        KeyUp = up,
        KeyDown = down,
        KeyLeft = left,
        KeyRight = right,
        AxisSettings = new InputAxisSettings { DeadZone = 0f },
    };

    /// <summary>
    /// A gamepad or mouse axis control, with no Flax dead zone so tests see raw values.
    /// </summary>
    public static InputMappingEntry Axis(InputControl control, InputControlAxes axes = InputControlAxes.XY, InputAxisTarget target = InputAxisTarget.X) => new()
    {
        Control = control,
        Axes = axes,
        Target = target,
        AxisSettings = new InputAxisSettings { DeadZone = 0f },
    };

    public static InputMappingEntry With(this InputMappingEntry entry, params InputModifier[] modifiers)
    {
        entry.Modifiers = [..modifiers];
        return entry;
    }

    public static InputMappingEntry With(this InputMappingEntry entry, params InputTrigger[] triggers)
    {
        entry.Triggers = [..triggers];
        return entry;
    }

    public static InputActionMapping Map(InputAction action, params InputMappingEntry[] inputs) => new()
    {
        RuntimeAction = action,
        Inputs = [..inputs],
    };

    public static InputMappingContext Context(string name, params InputActionMapping[] mappings) => new()
    {
        ContextName = name,
        Mappings = [..mappings],
    };

    public static TriggerChord Chord(InputAction chordAction) => new() { RuntimeChordAction = chordAction };
}
