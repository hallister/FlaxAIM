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

    [Space(3)]
    [Header("Axis Settings")]
    [VisibleIf(nameof(UseAxis))]
    [Tooltip("Positive or negative values smaller than this register as zero.")]
    public float AxisDeadZone = 0.1f;

    [VisibleIf(nameof(UseAxis))]
    [Tooltip("For keyboard input, how fast the value moves towards its target (units/s). For mouse delta, a multiplier on the delta.")]
    public float AxisSensitivity = 1.0f;

    [VisibleIf(nameof(UseAxis))]
    [Tooltip("For keyboard input, how fast the value returns to zero when released (units/s).")]
    public float AxisGravity = 0.0f;

    [VisibleIf(nameof(UseAxis))]
    [Tooltip("Multiplier applied to the axis value by Flax, before this binding's modifiers.")]
    public float AxisScale = 1.0f;

    [VisibleIf(nameof(UseAxis))]
    [Tooltip("For keyboard input, jump to zero immediately when the opposite key is pressed.")]
    public bool AxisSnap = false;

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

    /// <summary>
    /// An action that isn't an asset, for contexts built in code. Takes precedence over <see cref="InputAction"/>.
    /// Not serialized.
    /// </summary>
    [NoSerialize, HideInEditor]
    public InputAction RuntimeAction;

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