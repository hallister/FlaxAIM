using System;
using FlaxAIM.Modifiers;
using FlaxEngine;

namespace FlaxAIM.State;

/// <summary>
/// A bound callback. <see cref="Callback"/> is the delegate the caller passed in (used for unbinding and ownership);
/// <see cref="Invoke"/> adapts it to the action value.
/// </summary>
public readonly struct RegisteredCallbackHandler(long id, Tag filterTag, Delegate callback, Action<ProcessedInputActionValue> invoke)
{
    public readonly long Id = id;
    public readonly Tag FilterTag = filterTag;
    public readonly Delegate Callback = callback;
    internal readonly Action<ProcessedInputActionValue> Invoke = invoke;

    /// <summary>
    /// False once the callback's owner is a Flax object (e.g. a script) that has been destroyed.
    /// </summary>
    internal bool IsOwnerAlive => Callback.Target is not FlaxEngine.Object owner || owner;
}
