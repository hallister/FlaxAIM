using System.Collections.Generic;
using FlaxEngine;

namespace FlaxAIM;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class InputManager : Script
{
    private readonly List<InputMappingContext> _contextStack = [];
    private CompiledInputMap _compiled = CompiledInputMap.Empty;

    // Context changes made from callbacks during OnUpdate are compiled once the frame's evaluation has finished.
    private bool _isUpdating;
    private bool _rebuildPending;

    public void AddInputContext(InputMappingContext context) => AddInputContext([context]);
    public void RemoveInputContext(InputMappingContext context) => RemoveInputContext([context]);

    public void AddInputContext(InputMappingContext[] contexts)
    {
        if (contexts == null) return;

        var changed = false;
        foreach (var context in contexts)
        {
            if (context == null || _contextStack.Contains(context)) continue;
            _contextStack.Add(context);
            changed = true;
        }

        if (changed) RebuildVirtualMappings();
    }

    public void RemoveInputContext(InputMappingContext[] contexts)
    {
        if (contexts == null) return;

        var changed = false;
        foreach (var context in contexts)
        {
            changed |= context != null && _contextStack.Remove(context);
        }

        if (changed) RebuildVirtualMappings();
    }

    /// <summary>
    /// Flattens the active context stack from lowest to highest priority and
    /// commits them straight down to Flax Engine's live static runtime input tables.
    /// Actions that are no longer mapped are flushed (Completed/Canceled) on the next update.
    /// </summary>
    private void RebuildVirtualMappings()
    {
        if (_isUpdating)
        {
            _rebuildPending = true;
            return;
        }

        _compiled = InputMappingCompiler.Compile(_contextStack);
    }
}
