using FlaxEngine;

namespace FlaxAIM.Modifiers;

/// <summary>
/// Multiplies the value by the frame's length in seconds, turning a rate into an amount for this frame. Use it on a
/// stick bound to an action that otherwise reads a per-frame amount, such as a look action also bound to the mouse
/// delta: the stick then turns at a speed that doesn't depend on the frame rate. Follow it with a
/// <see cref="ModifierScale"/> to set that rate (for a look in degrees, degrees per second at full tilt).
/// </summary>
/// <remarks>
/// Only the processor knows the frame time, so the plain <see cref="InputModifier.Modify(float)"/> overloads pass
/// values through unchanged.
/// </remarks>
public class ModifierScaleByDeltaTime : InputModifier
{
    [ShowInEditor]
    [EditorOrder(0)]
    public override string Name => "Scale By Delta Time";

    public override Float3 Modify(Float3 value, InputActionType actionType, float deltaTime)
        => base.Modify(value, actionType, deltaTime) * deltaTime;
}
