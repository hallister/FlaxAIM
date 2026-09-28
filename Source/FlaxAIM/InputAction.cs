using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using FlaxAIM.Modifiers;
using FlaxAIM.Triggers;
using FlaxEngine;

namespace FlaxAIM;

/// <summary>
/// The underlying action type (boolean, float, Float2, Float3)
/// </summary>
public enum InputActionType
{
    /// <summary>
    /// On/Off (Button press)
    /// </summary>
    Digital,

    /// <summary>
    /// Float (Trigger/Throttle)
    /// </summary>
    Axis1D,

    /// <summary>
    /// Vector2 (Thumbstick/Mouse movement)
    /// </summary>
    Axis2D,

    /// <summary>
    /// Vector3 (e.g. free-flight movement or motion sensors)
    /// </summary>
    Axis3D,
}

/// <summary>
/// Defines the type of action being performed.
/// </summary>
[ContentContextMenu("New/Adaptive Input/Input Action")]
public class InputAction
{
    /// <summary>
    /// Fallback identity for actions that weren't loaded from an asset. Actions loaded from an asset are
    /// identified by the asset's ID, so duplicated assets never collide.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [HideInEditor]
    public Guid ID = Guid.NewGuid();

    [Tooltip("Visual identifier only used for debugging purposes.")]
    public string Name = "DefaultInputAction";

    [Tooltip("The type of input expected by this InputAction.")]
    public InputActionType ActionType = InputActionType.Digital;

    [Tooltip("If enabled, the keys, buttons and axes bound to this action block the same inputs in lower-priority contexts.")]
    public bool ConsumeInput = true;

    [Tooltip("Modifiers applied to the combined action value, after each binding's own modifiers.")]
    [Collection(Display = CollectionAttribute.DisplayType.Header)]
    public List<InputModifier> Modifiers = [];

    [Tooltip("Triggers evaluated against the combined action value. When present, both these and the binding's triggers must pass.")]
    [Collection(Display = CollectionAttribute.DisplayType.Header)]
    public List<IInputTrigger> Triggers = [];

    // Force Modifiers and Triggers into an empty array, since Flax will null them if you force them to zero entries.
    [OnSerializing]
    internal void OnSerializing(StreamingContext context)
    {
        Modifiers ??= [];
        Triggers ??= [];
    }

    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context)
    {
        Modifiers = Modifiers != null ? [..Modifiers] : [];
        Triggers = Triggers != null ? [..Triggers] : [];
    }
}
