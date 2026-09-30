using System;
using System.Collections.Generic;
using FlaxEngine;

namespace FlaxAIM.Tests;

/// <summary>
/// Stands in for Flax's virtual input system: stores the published mapping tables and answers
/// <c>GetAction</c>/<c>GetAxis</c> by evaluating those configs against simulated keys, mouse and gamepad buttons, and axes.
/// </summary>
/// <remarks>
/// Axis values are instantaneous: Flax's keyboard smoothing (sensitivity, gravity, snap) isn't simulated.
/// Dead zone and scale are.
/// </remarks>
public sealed class FakeVirtualInput : IVirtualInputBackend
{
    private readonly HashSet<KeyboardKeys> _keys = [];
    private readonly HashSet<MouseButton> _mouseButtons = [];
    private readonly HashSet<(int Gamepad, GamepadButton Button)> _buttons = [];
    private readonly Dictionary<(int Gamepad, InputAxisType Axis), float> _axes = [];

    public ActionConfig[] ActionMappings { get; set; } = [];
    public AxisConfig[] AxisMappings { get; set; } = [];

    public void Press(params KeyboardKeys[] keys) => _keys.UnionWith(keys);
    public void Release(params KeyboardKeys[] keys) => _keys.ExceptWith(keys);

    public void Press(MouseButton button) => _mouseButtons.Add(button);
    public void Release(MouseButton button) => _mouseButtons.Remove(button);

    public void Press(GamepadButton button, int gamepad = 0) => _buttons.Add((gamepad, button));
    public void Release(GamepadButton button, int gamepad = 0) => _buttons.Remove((gamepad, button));

    /// <summary>
    /// Sets a gamepad or mouse axis. The gamepad index is ignored for mouse axes.
    /// </summary>
    public void SetAxis(InputAxisType axis, float value, int gamepad = 0) => _axes[(IsMouse(axis) ? -1 : gamepad, axis)] = value;

    public void ReleaseAll()
    {
        _keys.Clear();
        _mouseButtons.Clear();
        _buttons.Clear();
        _axes.Clear();
    }

    public bool GetAction(string name)
    {
        var index = Array.FindIndex(ActionMappings, c => c.Name == name);
        if (index < 0) return false;

        var config = ActionMappings[index];
        return IsDown(config.Key) || IsDown(config.MouseButton) || IsDown(config.Gamepad, config.GamepadButton);
    }

    public float GetAxis(string name)
    {
        var index = Array.FindIndex(AxisMappings, c => c.Name == name);
        if (index < 0) return 0f;

        var config = AxisMappings[index];
        var value = config.Axis == InputAxisType.KeyboardOnly ? 0f : AxisValue(config.Gamepad, config.Axis);

        if (IsDown(config.PositiveButton) || IsDown(config.Gamepad, config.GamepadPositiveButton)) value += 1f;
        if (IsDown(config.NegativeButton) || IsDown(config.Gamepad, config.GamepadNegativeButton)) value -= 1f;
        value = Math.Clamp(value, -1f, 1f);

        if (Math.Abs(value) < config.DeadZone) return 0f;
        return value * config.Scale;
    }

    private bool IsDown(KeyboardKeys key) => key != KeyboardKeys.None && _keys.Contains(key);

    private bool IsDown(MouseButton button) => button != MouseButton.None && _mouseButtons.Contains(button);

    private bool IsDown(InputGamepadIndex gamepad, GamepadButton button)
    {
        if (button == GamepadButton.None) return false;
        if (gamepad != InputGamepadIndex.All) return _buttons.Contains(((int)gamepad, button));

        foreach (var (_, pressed) in _buttons)
        {
            if (pressed == button) return true;
        }
        return false;
    }

    private float AxisValue(InputGamepadIndex gamepad, InputAxisType axis)
    {
        if (IsMouse(axis)) return _axes.GetValueOrDefault((-1, axis));
        if (gamepad != InputGamepadIndex.All) return _axes.GetValueOrDefault(((int)gamepad, axis));

        // Like Flax with Gamepad = All: the strongest input from any gamepad
        var strongest = 0f;
        foreach (var ((_, candidate), value) in _axes)
        {
            if (candidate == axis && Math.Abs(value) > Math.Abs(strongest)) strongest = value;
        }
        return strongest;
    }

    private static bool IsMouse(InputAxisType axis) => axis is InputAxisType.MouseX or InputAxisType.MouseY or InputAxisType.MouseWheel;
}
