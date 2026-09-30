using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using FlaxAIM.Modifiers;
using FlaxAIM.Triggers;
using FlaxEngine;

namespace FlaxAIM;

/// <summary>
/// Which component of an action's value a one-dimensional binding drives.
/// </summary>
public enum InputAxisTarget
{
    X,
    Y,
    Z,
}

/// <summary>
/// Which axes of a two-dimensional control (a stick, the D-pad or the mouse) a binding reads.
/// </summary>
public enum InputControlAxes
{
    /// <summary> Both axes: X drives the action's X and Y drives its Y. </summary>
    XY,

    /// <summary> Only the horizontal axis, placed in the binding's <see cref="InputMappingEntry.Target"/>. </summary>
    X,

    /// <summary> Only the vertical axis, placed in the binding's <see cref="InputMappingEntry.Target"/>. </summary>
    Y,
}

public enum InputDevice
{
    Keyboard,
    Mouse,
    Gamepad,
}

/// <summary>
/// The physical input a binding reads. Values are grouped by device and serialized, so never renumber them.
/// </summary>
public enum InputControl
{
    [Tooltip("A single key.")]
    Key = 0,

    [Tooltip("A positive and a negative key, giving -1..1.")]
    KeyAxis = 1,

    [Tooltip("Up, down, left and right keys (like WASD), giving a 2D value.")]
    DirectionalKeys = 2,

    [Tooltip("A single mouse button.")]
    MouseButton = 10,

    [Tooltip("Mouse movement since the last frame, giving a 2D value.")]
    MouseDelta = 11,

    [Tooltip("The scroll wheel.")]
    MouseWheel = 12,

    [Tooltip("A single gamepad button.")]
    GamepadButton = 20,

    [Tooltip("A positive and a negative gamepad button, giving -1..1.")]
    GamepadButtonAxis = 21,

    [Tooltip("The left stick, giving a 2D value.")]
    LeftStick = 22,

    [Tooltip("The right stick, giving a 2D value.")]
    RightStick = 23,

    [Tooltip("The D-pad as an axis pair, giving a 2D value.")]
    DPad = 24,

    [Tooltip("The left trigger, giving 0..1.")]
    LeftTrigger = 25,

    [Tooltip("The right trigger, giving 0..1.")]
    RightTrigger = 26,
}

/// <summary>
/// Describes each <see cref="InputControl"/>: its device, how many components it produces and its display name.
/// </summary>
public static class InputControls
{
    /// <summary> Every control, in menu order. </summary>
    public static readonly InputControl[] All = (InputControl[])Enum.GetValues(typeof(InputControl));

    public static InputDevice Device(this InputControl control) => (int)control switch
    {
        < 10 => InputDevice.Keyboard,
        < 20 => InputDevice.Mouse,
        _    => InputDevice.Gamepad,
    };

    /// <summary>
    /// True for controls that read a Flax virtual button (on/off), false for the ones that read a virtual axis.
    /// </summary>
    public static bool IsButton(this InputControl control) => control is InputControl.Key or InputControl.MouseButton or InputControl.GamepadButton;

    /// <summary>
    /// True for controls with an X and a Y axis. <see cref="InputControl.DirectionalKeys"/> always reads both;
    /// the others can be narrowed with <see cref="InputMappingEntry.Axes"/>.
    /// </summary>
    public static bool Is2D(this InputControl control) => control is InputControl.DirectionalKeys or InputControl.MouseDelta
        or InputControl.LeftStick or InputControl.RightStick or InputControl.DPad;

    public static string DisplayName(this InputControl control) => control switch
    {
        InputControl.Key               => "Key",
        InputControl.KeyAxis           => "Key Axis",
        InputControl.DirectionalKeys   => "Directional Keys",
        InputControl.MouseButton       => "Button",
        InputControl.MouseDelta        => "Delta",
        InputControl.MouseWheel        => "Wheel",
        InputControl.GamepadButton     => "Button",
        InputControl.GamepadButtonAxis => "Button Axis",
        InputControl.LeftStick         => "Left Stick",
        InputControl.RightStick        => "Right Stick",
        InputControl.DPad              => "D-Pad",
        InputControl.LeftTrigger       => "Left Trigger",
        InputControl.RightTrigger      => "Right Trigger",
        _                              => control.ToString(),
    };
}

/// <summary>
/// Flax's axis settings for a binding that reads a virtual axis.
/// </summary>
public struct InputAxisSettings()
{
    [Tooltip("Positive or negative values smaller than this register as zero.")]
    public float DeadZone = 0.1f;

    [Tooltip("For keys and buttons, how fast the value moves towards its target (units/s). For the mouse, a multiplier on the delta.")]
    public float Sensitivity = 1.0f;

    [Tooltip("For keys and buttons, how fast the value returns to zero when released (units/s).")]
    public float Gravity = 0.0f;

    [Tooltip("Multiplier applied to the axis value by Flax, before this binding's modifiers.")]
    public float Scale = 1.0f;

    [Tooltip("For keys and buttons, jump to zero immediately when the opposite one is pressed.")]
    public bool Snap = false;
}

/// <summary>
/// One binding of an action: the one input that drives it, with that input's own modifiers and triggers.
/// </summary>
public struct InputMappingEntry()
{
    [EditorOrder(10), Tooltip("The device and control this binding reads.")]
    public InputControl Control = InputControl.Key;

    [EditorOrder(20), VisibleIf(nameof(ShowKey))]
    public KeyboardKeys Key = KeyboardKeys.None;

    [EditorOrder(20), VisibleIf(nameof(ShowMouseButton))]
    public MouseButton MouseButton = MouseButton.None;

    [EditorOrder(20), VisibleIf(nameof(ShowGamepadButton))]
    public GamepadButton GamepadButton = GamepadButton.None;

    [EditorOrder(20), VisibleIf(nameof(ShowKeyAxis))]
    public KeyboardKeys KeyPositive = KeyboardKeys.None;

    [EditorOrder(21), VisibleIf(nameof(ShowKeyAxis))]
    public KeyboardKeys KeyNegative = KeyboardKeys.None;

    [EditorOrder(20), VisibleIf(nameof(ShowDirectionalKeys))]
    public KeyboardKeys KeyUp = KeyboardKeys.None;

    [EditorOrder(21), VisibleIf(nameof(ShowDirectionalKeys))]
    public KeyboardKeys KeyDown = KeyboardKeys.None;

    [EditorOrder(22), VisibleIf(nameof(ShowDirectionalKeys))]
    public KeyboardKeys KeyLeft = KeyboardKeys.None;

    [EditorOrder(23), VisibleIf(nameof(ShowDirectionalKeys))]
    public KeyboardKeys KeyRight = KeyboardKeys.None;

    [EditorOrder(20), VisibleIf(nameof(ShowGamepadButtonAxis))]
    public GamepadButton GamepadPositiveButton = GamepadButton.None;

    [EditorOrder(21), VisibleIf(nameof(ShowGamepadButtonAxis))]
    public GamepadButton GamepadNegativeButton = GamepadButton.None;

    [EditorOrder(30), VisibleIf(nameof(ShowAxes))]
    [Tooltip("Which axes of the control to read. XY fills the action's X and Y; X or Y reads one axis into Target.")]
    public InputControlAxes Axes = InputControlAxes.XY;

    [EditorOrder(40), VisibleIf(nameof(ShowTarget))]
    [Tooltip("Which component of the action value this one-value binding drives. Ignored by Digital and Axis1D actions.")]
    public InputAxisTarget Target = InputAxisTarget.X;

    [EditorOrder(50), VisibleIf(nameof(ShowAxisSettings))]
    public InputAxisSettings AxisSettings = new();

    [EditorOrder(60)]
    [Collection(Display = CollectionAttribute.DisplayType.Header)]
    public List<InputModifier> Modifiers = [];

    [EditorOrder(70)]
    [Tooltip("Triggers that determine the exact state rules for this binding.")]
    [Collection(Display = CollectionAttribute.DisplayType.Header)]
    public List<IInputTrigger> Triggers = [];

    /// <summary>
    /// How many components this binding produces: 2 for a control read on both axes, 1 otherwise.
    /// </summary>
    [HideInEditor]
    public int ComponentCount => Control.Is2D() && (Axes == InputControlAxes.XY || Control == InputControl.DirectionalKeys) ? 2 : 1;

    private bool ShowKey => Control == InputControl.Key;
    private bool ShowMouseButton => Control == InputControl.MouseButton;
    private bool ShowGamepadButton => Control == InputControl.GamepadButton;
    private bool ShowKeyAxis => Control == InputControl.KeyAxis;
    private bool ShowDirectionalKeys => Control == InputControl.DirectionalKeys;
    private bool ShowGamepadButtonAxis => Control == InputControl.GamepadButtonAxis;
    private bool ShowAxes => Control.Is2D() && Control != InputControl.DirectionalKeys;
    private bool ShowAxisSettings => !Control.IsButton();

    // A binding that fills X and Y places itself. Buttons are nearly always on Digital actions, which ignore Target.
    private bool ShowTarget => ComponentCount == 1 && !Control.IsButton();

    // Force Modifiers and Triggers into an empty array, since Flax will null them if you force them to zero entries.
    [OnSerializing]
    internal void OnSerializing(StreamingContext context)
    {
        Modifiers ??= [];
        Triggers ??= [];
    }

    // Fixes an issue with deserialization resulting in new Modifiers/Triggers mirroring
    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context)
    {
        // If the lists are null, or if they were shallow-cloned from an adjacent row, give this row its own
        Modifiers = Modifiers != null ? [..Modifiers] : [];
        Triggers = Triggers != null ? [..Triggers] : [];
    }
}

/// <summary>
/// An action and the inputs that drive it.
/// </summary>
public struct InputActionMapping()
{
    [EditorOrder(0), Tooltip("The Input Action these inputs drive.")]
    public JsonAssetReference<InputAction> InputAction;

    /// <summary>
    /// An action that isn't an asset, for contexts built in code. Takes precedence over <see cref="InputAction"/>.
    /// Not serialized.
    /// </summary>
    [NoSerialize, HideInEditor]
    public InputAction RuntimeAction;

    [EditorOrder(10), Tooltip("Each input that drives the action, with its own modifiers and triggers.")]
    [Collection(Display = CollectionAttribute.DisplayType.Header)]
    public List<InputMappingEntry> Inputs = [];

    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context)
    {
        // If the list is null, or if it was shallow-cloned from an adjacent action, give this action its own
        Inputs = Inputs != null ? [..Inputs] : [];
    }
}

[ContentContextMenu("New/Adaptive Input/Input Mapping")]
public class InputMappingContext
{
    public string ContextName;

    [Tooltip("The actions in this context, each with the inputs that drive it.")]
    [Collection(Display = CollectionAttribute.DisplayType.Header)]
    public List<InputActionMapping> Mappings = [];

    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context)
    {
        // If the list is null, or if it was shallow-cloned from an adjacent row, give this context its own
        Mappings = Mappings != null ? [..Mappings] : [];
    }
}
