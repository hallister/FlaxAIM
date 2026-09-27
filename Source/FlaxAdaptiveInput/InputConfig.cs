using FlaxEngine;

namespace FlaxAdaptiveInput;

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
