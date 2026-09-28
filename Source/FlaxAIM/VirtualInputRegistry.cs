using System.Collections.Generic;
using FlaxEngine;

namespace FlaxAIM;

/// <summary>
/// Owns Flax's global <see cref="Input.ActionMappings"/> / <see cref="Input.AxisMappings"/> while any
/// <see cref="InputManager"/> is active. The project's own mappings are kept alongside every manager's virtual
/// inputs, and restored once the last manager unregisters.
/// </summary>
internal static class VirtualInputRegistry
{
    /// <summary> Prefix of every virtual input name the plugin creates. </summary>
    public const string NamePrefix = "AIM:";

    private static readonly Dictionary<InputManager, (ActionConfig[] Actions, AxisConfig[] Axes)> Owners = new();
    private static ActionConfig[] _originalActions;
    private static AxisConfig[] _originalAxes;

    public static void Set(InputManager owner, ActionConfig[] actions, AxisConfig[] axes)
    {
        if (Owners.Count == 0) CaptureOriginals();

        Owners[owner] = (actions, axes);
        Commit();
    }

    public static void Remove(InputManager owner)
    {
        if (Owners.Remove(owner)) Commit();
    }

    private static void CaptureOriginals()
    {
        // Leftovers from a previous session (e.g. after a scripts reload) are not the project's mappings.
        _originalActions = System.Array.FindAll(Input.ActionMappings ?? [], a => a.Name == null || !a.Name.StartsWith(NamePrefix));
        _originalAxes = System.Array.FindAll(Input.AxisMappings ?? [], a => a.Name == null || !a.Name.StartsWith(NamePrefix));
    }

    private static void Commit()
    {
        var actions = new List<ActionConfig>(_originalActions);
        var axes = new List<AxisConfig>(_originalAxes);

        foreach (var (ownerActions, ownerAxes) in Owners.Values)
        {
            actions.AddRange(ownerActions);
            axes.AddRange(ownerAxes);
        }

        Input.ActionMappings = actions.ToArray();
        Input.AxisMappings = axes.ToArray();
    }
}
