using System;
using System.Collections.Generic;
using FlaxEngine;

namespace FlaxAIM;

/// <summary>
/// A binding a player has changed: the context it's in, the action it drives and its row in that action's
/// <see cref="InputActionMapping.Inputs"/>.
/// </summary>
/// <param name="Context">The <see cref="InputMappingContext.ContextName"/> of the context the binding is in.</param>
/// <param name="Action">The action's stable identity: the ID of its asset (or its own ID for actions made in code).</param>
/// <param name="Row">The binding's index in the action's inputs within that context.</param>
public readonly record struct InputBindingKey(string Context, Guid Action, int Row);

/// <summary>An action's bindings on one device, scaled together (<see cref="InputOverrides.SetScale"/>).</summary>
/// <param name="Action">The action's stable identity, as in <see cref="InputBindingKey.Action"/>.</param>
/// <param name="Device">The device whose bindings of the action are scaled.</param>
public readonly record struct InputScaleKey(Guid Action, InputDevice Device);

/// <summary>
/// A player's changes to the mapping assets, for rebinding and sensitivity settings: replacement controls for single
/// bindings, and a scale on an action's bindings of each device. Give one to a processor (<see cref="InputProcessor.Overrides"/>)
/// and it recompiles whenever the overrides change; several processors can share one.
/// </summary>
/// <remarks>
/// A row past the end of the action's inputs in that context adds a binding the asset doesn't have (a gamepad button
/// for an action the asset only binds to a key), used whole: its modifiers, triggers, axis settings and target are the
/// given entry's, so copy them from the asset's binding it stands beside. A replacement of the asset's own row only
/// changes which control a binding reads (<see cref="InputMappingEntry.Control"/> and its keys or
/// buttons). The binding's modifiers, triggers, axis settings and target stay as the asset has them, so a rebound jump
/// still has its hold trigger. Overrides keyed to a context, action or row that doesn't exist are ignored.
/// </remarks>
public sealed class InputOverrides
{
    private readonly Dictionary<InputBindingKey, InputMappingEntry> _bindings = new();
    private readonly Dictionary<InputScaleKey, Float3> _scales = new();

    /// <summary>Raised after any change.</summary>
    public event Action Changed;

    /// <summary>The bindings with replacement controls, for saving them.</summary>
    public IReadOnlyDictionary<InputBindingKey, InputMappingEntry> Bindings => _bindings;

    /// <summary>The scaled actions and devices, for saving them.</summary>
    public IReadOnlyDictionary<InputScaleKey, Float3> Scales => _scales;

    /// <summary>
    /// Makes the binding at <paramref name="key"/> read <paramref name="controls"/>' control instead of the asset's.
    /// Only the control fields of <paramref name="controls"/> are used, unless the row is past the asset's, where the
    /// whole entry is the binding.
    /// </summary>
    public void SetBinding(InputBindingKey key, InputMappingEntry controls)
    {
        if (_bindings.TryGetValue(key, out var existing) && SameControls(existing, controls)) return;
        _bindings[key] = controls;
        Changed?.Invoke();
    }

    /// <summary>Puts the binding at <paramref name="key"/> back to the asset's control. False if it wasn't changed.</summary>
    public bool RemoveBinding(InputBindingKey key)
    {
        if (!_bindings.Remove(key)) return false;
        Changed?.Invoke();
        return true;
    }

    /// <summary>The replacement controls for the binding at <paramref name="key"/>, if it has any.</summary>
    public bool TryGetBinding(InputBindingKey key, out InputMappingEntry controls) => _bindings.TryGetValue(key, out controls);

    /// <summary>
    /// Multiplies what the action's bindings on <paramref name="key"/>'s device read, after each binding's own
    /// modifiers, by <paramref name="scale"/> per component: a mouse or gamepad look sensitivity, or -1 on Y to invert
    /// it. <see cref="Float3.One"/> removes the scale.
    /// </summary>
    public void SetScale(InputScaleKey key, Float3 scale)
    {
        var had = _scales.TryGetValue(key, out var existing);
        if (scale == Float3.One)
        {
            if (!had) return;
            _scales.Remove(key);
        }
        else
        {
            if (had && existing == scale) return;
            _scales[key] = scale;
        }
        Changed?.Invoke();
    }

    /// <summary>The scale on the action's bindings of that device; <see cref="Float3.One"/> when there's none.</summary>
    public Float3 ScaleOf(InputScaleKey key) => _scales.TryGetValue(key, out var scale) ? scale : Float3.One;

    /// <summary>Removes every override.</summary>
    public void Clear()
    {
        if (_bindings.Count == 0 && _scales.Count == 0) return;
        _bindings.Clear();
        _scales.Clear();
        Changed?.Invoke();
    }

    /// <summary>
    /// Replaces every override at once, raising <see cref="Changed"/> a single time: for loading saved settings.
    /// </summary>
    public void Load(IEnumerable<KeyValuePair<InputBindingKey, InputMappingEntry>> bindings, IEnumerable<KeyValuePair<InputScaleKey, Float3>> scales)
    {
        _bindings.Clear();
        _scales.Clear();
        if (bindings != null)
            foreach (var (key, controls) in bindings) _bindings[key] = controls;
        if (scales != null)
            foreach (var (key, scale) in scales)
                if (scale != Float3.One) _scales[key] = scale;
        Changed?.Invoke();
    }

    /// <summary>
    /// Adds the bindings past the asset's <paramref name="count"/> rows of <paramref name="action"/> in
    /// <paramref name="context"/> to <paramref name="rows"/>, in row order, each entry whole.
    /// </summary>
    internal void AddRowsBeyond(string context, Guid action, int count, List<(int Row, InputMappingEntry Entry)> rows)
    {
        var start = rows.Count;
        foreach (var (key, controls) in _bindings)
        {
            if (key.Row >= count && key.Action == action && key.Context == context)
                rows.Add((key.Row, controls));
        }
        if (rows.Count - start > 1) rows.Sort(start, rows.Count - start, Comparer<(int Row, InputMappingEntry Entry)>.Create((a, b) => a.Row.CompareTo(b.Row)));
    }

    /// <summary>
    /// <paramref name="asset"/> reading <paramref name="controls"/>' control: the control and its keys or buttons
    /// from <paramref name="controls"/>, everything else from <paramref name="asset"/>.
    /// </summary>
    public static InputMappingEntry Apply(InputMappingEntry asset, InputMappingEntry controls)
    {
        var entry = asset;
        entry.Control = controls.Control;
        entry.Key = controls.Key;
        entry.MouseButton = controls.MouseButton;
        entry.GamepadButton = controls.GamepadButton;
        entry.KeyPositive = controls.KeyPositive;
        entry.KeyNegative = controls.KeyNegative;
        entry.KeyUp = controls.KeyUp;
        entry.KeyDown = controls.KeyDown;
        entry.KeyLeft = controls.KeyLeft;
        entry.KeyRight = controls.KeyRight;
        entry.GamepadPositiveButton = controls.GamepadPositiveButton;
        entry.GamepadNegativeButton = controls.GamepadNegativeButton;
        entry.Axes = controls.Axes;
        return entry;
    }

    /// <summary>Whether two bindings read the same control (ignoring modifiers, triggers and axis settings).</summary>
    public static bool SameControls(InputMappingEntry a, InputMappingEntry b) =>
        a.Control == b.Control && a.Key == b.Key && a.MouseButton == b.MouseButton && a.GamepadButton == b.GamepadButton
        && a.KeyPositive == b.KeyPositive && a.KeyNegative == b.KeyNegative
        && a.KeyUp == b.KeyUp && a.KeyDown == b.KeyDown && a.KeyLeft == b.KeyLeft && a.KeyRight == b.KeyRight
        && a.GamepadPositiveButton == b.GamepadPositiveButton && a.GamepadNegativeButton == b.GamepadNegativeButton
        && a.Axes == b.Axes;
}
