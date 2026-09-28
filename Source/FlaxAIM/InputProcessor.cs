using System.Collections.Generic;
using System.Threading;
using FlaxEngine;

namespace FlaxAIM;

/// <summary>
/// The engine-independent core of FlaxAIM: active contexts, action evaluation and callbacks.
/// <see cref="InputManager"/> is a script that owns one and updates it every frame; use a processor directly to drive
/// input from your own update loop (or from tests).
/// </summary>
/// <remarks>
/// A processor only publishes its virtual inputs to Flax while enabled (<see cref="Enable"/>/<see cref="Disable"/>),
/// and <see cref="Update"/> does nothing while disabled.
/// </remarks>
public partial class InputProcessor
{
    private struct ContextEntry
    {
        public InputMappingContext Context;
        public int Priority;
        public int Order;
    }

    private static int _nextId;

    private readonly int _id = Interlocked.Increment(ref _nextId);
    private readonly List<ContextEntry> _contexts = [];
    private int _nextContextOrder;
    private CompiledInputMap _compiled = CompiledInputMap.Empty;
    private InputGamepadIndex _gamepad = InputGamepadIndex.All;
    private bool _useKeyboardAndMouse = true;

    // Context changes made from callbacks during Update are compiled once the frame's evaluation has finished.
    private bool _isUpdating;
    private bool _rebuildPending;

    /// <summary>
    /// Magnitude a binding with no triggers must reach to trigger its action.
    /// </summary>
    public float DefaultActuationThreshold { get; set; } = 0.1f;

    /// <summary>
    /// Which gamepad this processor reads. Use a specific gamepad per player for local multiplayer.
    /// </summary>
    public InputGamepadIndex Gamepad
    {
        get => _gamepad;
        set
        {
            if (_gamepad == value) return;
            _gamepad = value;
            RebuildMappings();
        }
    }

    /// <summary>
    /// Whether this processor reads keyboard and mouse bindings. Disable for players that only use a gamepad.
    /// </summary>
    public bool UseKeyboardAndMouse
    {
        get => _useKeyboardAndMouse;
        set
        {
            if (_useKeyboardAndMouse == value) return;
            _useKeyboardAndMouse = value;
            RebuildMappings();
        }
    }

    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Compiles the active contexts and publishes their virtual inputs.
    /// </summary>
    public void Enable()
    {
        if (IsEnabled) return;
        IsEnabled = true;
        RebuildMappings();
    }

    /// <summary>
    /// Withdraws this processor's virtual inputs and clears action states. Contexts and bindings are kept.
    /// </summary>
    public void Disable()
    {
        if (!IsEnabled) return;
        IsEnabled = false;
        VirtualInputRegistry.Remove(this);
        _compiled = CompiledInputMap.Empty;
        _trackers.Clear();
    }

    public void AddInputContext(InputMappingContext context) => AddInputContext(context, 0);

    /// <summary>
    /// Adds a context. Higher priorities are evaluated first, and their bindings consume the same inputs in lower
    /// contexts (see <see cref="InputAction.ConsumeInput"/>). Contexts with equal priority rank by most recently added.
    /// Adding a context that is already active updates its priority.
    /// </summary>
    public void AddInputContext(InputMappingContext context, int priority)
    {
        if (context == null) return;

        var index = _contexts.FindIndex(e => e.Context == context);
        if (index >= 0)
        {
            if (_contexts[index].Priority == priority) return;
            _contexts.RemoveAt(index);
        }

        _contexts.Add(new ContextEntry { Context = context, Priority = priority, Order = _nextContextOrder++ });
        RebuildMappings();
    }

    public void AddInputContext(InputMappingContext[] contexts)
    {
        if (contexts == null) return;

        var changed = false;
        foreach (var context in contexts)
        {
            if (context == null || HasInputContext(context)) continue;
            _contexts.Add(new ContextEntry { Context = context, Order = _nextContextOrder++ });
            changed = true;
        }

        if (changed) RebuildMappings();
    }

    public void RemoveInputContext(InputMappingContext context) => RemoveInputContext([context]);

    public void RemoveInputContext(InputMappingContext[] contexts)
    {
        if (contexts == null) return;

        var changed = false;
        foreach (var context in contexts)
        {
            changed |= context != null && _contexts.RemoveAll(e => e.Context == context) > 0;
        }

        if (changed) RebuildMappings();
    }

    public void ClearInputContexts()
    {
        if (_contexts.Count == 0) return;
        _contexts.Clear();
        RebuildMappings();
    }

    public bool HasInputContext(InputMappingContext context) => _contexts.Exists(e => e.Context == context);

    /// <summary>
    /// Recompiles the active contexts from highest to lowest priority and publishes them to Flax's input tables.
    /// Call after changing context or action data at runtime. Actions that are no longer mapped are flushed
    /// (Completed/Canceled) on the next update.
    /// </summary>
    public void RebuildMappings()
    {
        if (!IsEnabled) return;

        if (_isUpdating)
        {
            _rebuildPending = true;
            return;
        }

        var ordered = new List<ContextEntry>(_contexts);
        ordered.Sort((a, b) => a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority) : b.Order.CompareTo(a.Order));

        var options = new CompileOptions
        {
            ManagerId = _id,
            Gamepad = _gamepad,
            UseKeyboardAndMouse = _useKeyboardAndMouse,
        };

        _compiled = InputMappingCompiler.Compile(ordered.ConvertAll(e => e.Context), options);

        if (_compiled.ActionConfigs.Length + _compiled.AxisConfigs.Length > 0)
            VirtualInputRegistry.Set(this, _compiled.ActionConfigs, _compiled.AxisConfigs);
        else
            VirtualInputRegistry.Remove(this);
    }
}
