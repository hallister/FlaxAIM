using System;
using System.Collections.Generic;
using FlaxAIM.State;

namespace FlaxAIM;

/// <summary>
/// Returned by <see cref="InputManager.BindAction(InputAction, EnhancedInputActionState, Action)"/> and friends.
/// Dispose it to remove the binding(s) it represents.
/// </summary>
public sealed class InputBindingHandle : IDisposable
{
    internal static readonly InputBindingHandle Empty = new(null);

    private InputManager _manager;
    private readonly List<(ActionBindingKey Key, long Id)> _entries = [];

    internal InputBindingHandle(InputManager manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// True while at least one of the handle's bindings has not been disposed.
    /// </summary>
    public bool IsBound => _manager != null && _entries.Count > 0;

    internal void Add(ActionBindingKey key, long id) => _entries.Add((key, id));

    internal void AddRange(InputBindingHandle other)
    {
        if (other == null || other == Empty) return;
        _entries.AddRange(other._entries);
    }

    public void Dispose()
    {
        if (_manager == null) return;

        foreach (var (key, id) in _entries)
            _manager.RemoveHandler(key, id);

        _entries.Clear();
        _manager = null;
    }
}
