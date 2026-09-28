using System.Collections.Generic;
using System.Threading;
using FlaxEngine;

namespace FlaxAIM;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class InputManager : Script
{
    private struct ContextEntry
    {
        public InputMappingContext Context;
        public int Priority;
        public int Order;
    }

    private static int _nextManagerId;

    [Tooltip("Which gamepad this manager reads. Use a specific gamepad per player for local multiplayer.")]
    public InputGamepadIndex Gamepad = InputGamepadIndex.All;

    [Tooltip("Whether this manager reads keyboard and mouse bindings. Disable for players that only use a gamepad.")]
    public bool UseKeyboardAndMouse = true;

    private readonly int _managerId = Interlocked.Increment(ref _nextManagerId);
    private readonly List<ContextEntry> _contexts = [];
    private int _nextContextOrder;
    private CompiledInputMap _compiled = CompiledInputMap.Empty;

    // Only an enabled manager owns virtual inputs; contexts added before OnEnable are compiled there.
    private bool _isEnabled;

    // Context changes made from callbacks during OnUpdate are compiled once the frame's evaluation has finished.
    private bool _isUpdating;
    private bool _rebuildPending;

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
        RebuildVirtualMappings();
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

        if (changed) RebuildVirtualMappings();
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

        if (changed) RebuildVirtualMappings();
    }

    public void ClearInputContexts()
    {
        if (_contexts.Count == 0) return;
        _contexts.Clear();
        RebuildVirtualMappings();
    }

    public bool HasInputContext(InputMappingContext context) => _contexts.Exists(e => e.Context == context);

    /// <summary>
    /// Recompiles the active contexts. Call after changing context/action assets or this manager's device settings
    /// at runtime.
    /// </summary>
    public void RebuildMappings() => RebuildVirtualMappings();

    public override void OnEnable()
    {
        _isEnabled = true;
        RebuildVirtualMappings();
    }

    public override void OnDisable()
    {
        _isEnabled = false;
        VirtualInputRegistry.Remove(this);
        _compiled = CompiledInputMap.Empty;
        _trackers.Clear();
    }

    public override void OnDestroy()
    {
        VirtualInputRegistry.Remove(this);
    }

    /// <summary>
    /// Flattens the active contexts from highest to lowest priority and
    /// commits them to Flax Engine's live runtime input tables.
    /// Actions that are no longer mapped are flushed (Completed/Canceled) on the next update.
    /// </summary>
    private void RebuildVirtualMappings()
    {
        if (!_isEnabled) return;

        if (_isUpdating)
        {
            _rebuildPending = true;
            return;
        }

        var ordered = new List<ContextEntry>(_contexts);
        ordered.Sort((a, b) => a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority) : b.Order.CompareTo(a.Order));

        var options = new CompileOptions
        {
            ManagerId = _managerId,
            Gamepad = Gamepad,
            UseKeyboardAndMouse = UseKeyboardAndMouse,
        };

        _compiled = InputMappingCompiler.Compile(ordered.ConvertAll(e => e.Context), options);

        if (_compiled.ActionConfigs.Length + _compiled.AxisConfigs.Length > 0)
            VirtualInputRegistry.Set(this, _compiled.ActionConfigs, _compiled.AxisConfigs);
        else
            VirtualInputRegistry.Remove(this);
    }
}
