using System.Collections.Generic;
using FlaxEngine;

namespace FlaxAIM;

/// <summary>
/// Flax's virtual input system: the global mapping tables and the per-name queries.
/// Replaced in tests, which run without the engine.
/// </summary>
internal interface IVirtualInputBackend
{
    ActionConfig[] ActionMappings { get; set; }
    AxisConfig[] AxisMappings { get; set; }
    bool GetAction(string name);
    float GetAxis(string name);
}

internal sealed class FlaxVirtualInputBackend : IVirtualInputBackend
{
    public ActionConfig[] ActionMappings
    {
        get => Input.ActionMappings;
        set => Input.ActionMappings = value;
    }

    public AxisConfig[] AxisMappings
    {
        get => Input.AxisMappings;
        set => Input.AxisMappings = value;
    }

    public bool GetAction(string name) => Input.GetAction(name);
    public float GetAxis(string name) => Input.GetAxis(name);
}

/// <summary>
/// Owns Flax's global <see cref="Input.ActionMappings"/> / <see cref="Input.AxisMappings"/> while any
/// <see cref="InputProcessor"/> is enabled. The project's own mappings are kept alongside every processor's virtual
/// inputs, and restored once the last processor unregisters.
/// </summary>
internal static class VirtualInputRegistry
{
    /// <summary> Prefix of every virtual input name the plugin creates. </summary>
    public const string NamePrefix = "AIM:";

    private static IVirtualInputBackend _backend = new FlaxVirtualInputBackend();
    private static readonly Dictionary<InputProcessor, (ActionConfig[] Actions, AxisConfig[] Axes)> Owners = new();
    private static ActionConfig[] _originalActions;
    private static AxisConfig[] _originalAxes;

    /// <summary>
    /// The virtual input system in use. Setting it forgets every registration (tests set it per test).
    /// </summary>
    public static IVirtualInputBackend Backend
    {
        get => _backend;
        set
        {
            Owners.Clear();
            _backend = value ?? new FlaxVirtualInputBackend();
        }
    }

    public static void Set(InputProcessor owner, ActionConfig[] actions, AxisConfig[] axes)
    {
        if (Owners.Count == 0) CaptureOriginals();

        Owners[owner] = (actions, axes);
        Commit();
    }

    public static void Remove(InputProcessor owner)
    {
        if (Owners.Remove(owner)) Commit();
    }

    private static void CaptureOriginals()
    {
        // Leftovers from a previous session (e.g. after a scripts reload) are not the project's mappings.
        _originalActions = System.Array.FindAll(_backend.ActionMappings ?? [], a => a.Name == null || !a.Name.StartsWith(NamePrefix));
        _originalAxes = System.Array.FindAll(_backend.AxisMappings ?? [], a => a.Name == null || !a.Name.StartsWith(NamePrefix));
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

        _backend.ActionMappings = actions.ToArray();
        _backend.AxisMappings = axes.ToArray();
    }
}
