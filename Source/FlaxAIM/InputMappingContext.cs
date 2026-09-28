using System.Collections.Generic;
using System.Runtime.Serialization;
using FlaxAIM.Modifiers;
using FlaxAIM.Triggers;
using FlaxEngine;

namespace FlaxAIM;

/// <summary>
/// Which component of an action's value a binding drives.
/// </summary>
public enum InputAxisTarget
{
    /// <summary>
    /// Legacy row-based behaviour: for Axis2D/Axis3D actions row 0 drives X, row 1 drives Y and row 2 drives Z.
    /// Digital and Axis1D actions always use X.
    /// </summary>
    Auto,
    X,
    Y,
    Z,
}

public struct InputMappingEntry()
{
    [Tooltip("Which component of the action value this binding drives. Auto keeps the row-based behaviour (row 0 = X, row 1 = Y, row 2 = Z). Ignored for Digital and Axis1D actions.")]
    public InputAxisTarget Target = InputAxisTarget.Auto;

    public bool UseAxis = false;

    [VisibleIf(nameof(UseAxis))] public InputAxisType AxisType = InputAxisType.KeyboardOnly;
    
    [Space(3)]
    [Header("Inputs")]
    [VisibleIf(nameof(UseAxis), true)]
    public KeyboardKeys Key = KeyboardKeys.None;
    
    [VisibleIf(nameof(UseAxis), true)]
    public GamepadButton GamepadButton = GamepadButton.None;

    [Space(3)]
    [VisibleIf(nameof(UseAxis))]
    public KeyboardKeys KeyPositive = KeyboardKeys.None;
    [VisibleIf(nameof(UseAxis))]
    public KeyboardKeys KeyNegative = KeyboardKeys.None;
    [VisibleIf(nameof(UseAxis))]
    public GamepadButton GamepadPositiveButton = GamepadButton.None;
    [VisibleIf(nameof(UseAxis))]
    public GamepadButton GamepadNegativeButton = GamepadButton.None;

    // Flax's native JSON asset editor natively draws and manages polymorphic classes inline!
    [Collection(Display = CollectionAttribute.DisplayType.Header)]
    public List<InputModifier> Modifiers = [];
    
    [Tooltip("Triggers that determine the exact state rules for this binding edited cleanly inline.")]
    [Collection(Display = CollectionAttribute.DisplayType.Header)]
    public List<IInputTrigger> Triggers = [];

    // Force Modifiers and Triggers into an empty array, since Flax will null them if you force them to zero entries.
    [OnSerializing]
    internal void OnSerializing(StreamingContext context)
    {
        Modifiers ??= [];
        Triggers ??= [];
    }
    
    // Fixes an issue with deserialziztion resulting in new Modifiers/Triggers mirroring 
    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context)
    {
        // If the lists are null, or if they were shallow-cloned from an adjacent row, 
        Modifiers = Modifiers != null ? [..Modifiers] : [];
        Triggers = Triggers != null ? [..Triggers] : [];
    }
}

public struct InputActionEntry()
{
    [Tooltip("The abstract Input Action asset this mapping fulfills.")]
    public JsonAssetReference<InputAction> InputAction;
    
    [Collection(Display = CollectionAttribute.DisplayType.Header)]
    public List<InputMappingEntry> InputMapping = [];
    
    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context)
    {
        // If the lists are null, or if they were shallow-cloned from an adjacent row, 
        InputMapping = InputMapping != null ? [..InputMapping] : [];
    }
}

[ContentContextMenu("New/Adaptive Input/Input Mapping")]
public class InputMappingContext
{
    public string ContextName;
    
    [Collection(Display = CollectionAttribute.DisplayType.Header)]
    public List<InputActionEntry> Mappings = [];
    
    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context)
    {
        // If the lists are null, or if they were shallow-cloned from an adjacent row, 
        Mappings = Mappings != null ? [..Mappings] : [];
    }
}