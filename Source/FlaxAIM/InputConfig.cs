using System.Collections.Generic;
using System.Runtime.Serialization;
using FlaxEngine;

namespace FlaxAIM;

/// <summary>
/// Links an input action to a gameplay tag, so systems can react to input by tag without knowing about the action.
/// </summary>
[ContentContextMenu("New/Adaptive Input/Input Config")]
public class InputConfig
{
    [Tooltip("The input action to listen for.")]
    public JsonAssetReference<InputAction> InputAction;

    [Tooltip("The tag passed to bound callbacks when the action triggers.")]
    public Tag InputTag;
}

/// <summary>
/// A list of action ↔ tag links that can be bound in one call with <see cref="InputManager.BindActions"/>.
/// </summary>
[ContentContextMenu("New/Adaptive Input/Input Config Set")]
public class InputConfigSet
{
    [Tooltip("The actions to bind, each with the tag passed to the callback.")]
    [Collection(Display = CollectionAttribute.DisplayType.Header)]
    public List<InputConfig> Configs = [];

    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context)
    {
        Configs = Configs != null ? [..Configs] : [];
    }
}
