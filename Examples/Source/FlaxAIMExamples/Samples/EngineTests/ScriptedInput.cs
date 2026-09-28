using System;
using System.Collections.Generic;
using FlaxEngine;

namespace FlaxAIM.Samples.EngineTests;

/// <summary>
/// Simulated devices for engine tests, which can't press real keys: stores the published mapping tables and answers
/// queries by evaluating them against scripted keys and buttons. The unit tests' <c>FakeVirtualInput</c> does the same
/// with more detail (axes, gamepad indices).
/// </summary>
public sealed class ScriptedInput : IVirtualInputBackend
{
    private readonly HashSet<KeyboardKeys> _keys = [];
    private readonly HashSet<GamepadButton> _buttons = [];

    public ActionConfig[] ActionMappings { get; set; } = [];
    public AxisConfig[] AxisMappings { get; set; } = [];

    public void Press(params KeyboardKeys[] keys) => _keys.UnionWith(keys);
    public void Press(GamepadButton button) => _buttons.Add(button);

    public void ReleaseAll()
    {
        _keys.Clear();
        _buttons.Clear();
    }

    public bool GetAction(string name)
    {
        var index = Array.FindIndex(ActionMappings, c => c.Name == name);
        if (index < 0) return false;

        var config = ActionMappings[index];
        return _keys.Contains(config.Key) || _buttons.Contains(config.GamepadButton);
    }

    public float GetAxis(string name)
    {
        var index = Array.FindIndex(AxisMappings, c => c.Name == name);
        if (index < 0) return 0f;

        var config = AxisMappings[index];
        var value = 0f;
        if (_keys.Contains(config.PositiveButton) || _buttons.Contains(config.GamepadPositiveButton)) value += 1f;
        if (_keys.Contains(config.NegativeButton) || _buttons.Contains(config.GamepadNegativeButton)) value -= 1f;
        return value * config.Scale;
    }
}
