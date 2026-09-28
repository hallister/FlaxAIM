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
        Key = key,
        Triggers = [..triggers],
    };

    public static InputMappingEntry Button(GamepadButton button, params InputTrigger[] triggers) => new()
    {
        GamepadButton = button,
        Triggers = [..triggers],
    };

    /// <summary>
    /// A keyboard axis: <paramref name="positive"/> gives +1, <paramref name="negative"/> gives -1.
    /// </summary>
    public static InputMappingEntry Keys(KeyboardKeys positive, KeyboardKeys negative, InputAxisTarget target = InputAxisTarget.Auto) => new()
    {
        UseAxis = true,
        AxisType = InputAxisType.KeyboardOnly,
        KeyPositive = positive,
        KeyNegative = negative,
        Target = target,
        AxisDeadZone = 0f,
    };

    /// <summary>
    /// A gamepad or mouse axis, with no Flax dead zone so tests see raw values.
    /// </summary>
    public static InputMappingEntry Axis(InputAxisType axis, InputAxisTarget target = InputAxisTarget.Auto) => new()
    {
        UseAxis = true,
        AxisType = axis,
        Target = target,
        AxisDeadZone = 0f,
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

    public static InputActionEntry Map(InputAction action, params InputMappingEntry[] bindings) => new()
    {
        RuntimeAction = action,
        InputMapping = [..bindings],
    };

    public static InputMappingContext Context(string name, params InputActionEntry[] mappings) => new()
    {
        ContextName = name,
        Mappings = [..mappings],
    };

    public static TriggerChord Chord(InputAction chordAction) => new() { RuntimeChordAction = chordAction };
}
