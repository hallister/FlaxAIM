using System;
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
    /// 
    /// </summary>
    Axis3D,   // Vector3 (Accelerometer?)
}

/// <summary>
/// Defines the type of action being performed.
/// @todo Add triggers and modifiers to the IA's so an action c
/// </summary>
[ContentContextMenu("New/Adaptive Input/Input Action")]

public class InputAction
{
    // ReSharper disable once InconsistentNaming
    [HideInEditor]
    public Guid ID = Guid.NewGuid();
    
    [Tooltip("Visual identifier only used for debugging purposes.")]
    public string Name = "DefaultInputAction";
    
    [Tooltip("The type of input expected by this InputAction.")]
    public InputActionType ActionType = InputActionType.Digital;
}