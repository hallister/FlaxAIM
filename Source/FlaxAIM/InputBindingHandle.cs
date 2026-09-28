using System;
using System.Collections.Generic;
using FlaxAIM.State;

namespace FlaxAIM;

/// <summary>
/// Returned by <see cref="InputProcessor.BindAction(InputAction, EnhancedInputActionState, Action)"/> and friends.
/// Dispose it to remove the binding(s) it represents.
/// </summary>
public sealed class InputBindingHandle : IDisposable
{
    internal static readonly InputBindingHandle Empty = new(null);

    private InputProcessor _processor;
    private readonly List<(ActionBindingKey Key, long Id)> _entries = [];

    internal InputBindingHandle(InputProcessor processor)
    {
        _processor = processor;
    }

    /// <summary>
    /// True while at least one of the handle's bindings has not been disposed.
    /// </summary>
    public bool IsBound => _processor != null && _entries.Count > 0;

    internal void Add(ActionBindingKey key, long id) => _entries.Add((key, id));

    internal void AddRange(InputBindingHandle other)
    {
        if (other == null || other == Empty) return;
        _entries.AddRange(other._entries);
    }

    public void Dispose()
    {
        if (_processor == null) return;

        foreach (var (key, id) in _entries)
            _processor.RemoveHandler(key, id);

        _entries.Clear();
        _processor = null;
    }
}
