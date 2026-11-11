using FlaxEngine;

namespace FlaxAIM.Modifiers;

/// <summary>
/// Base class for binding modifiers. Override the overload(s) you need: each overload not overridden
/// forwards to the next dimension up (float → Float2 → Float3), and the Float3 overload passes the value
/// through unchanged. Overriding only the Float3 overload therefore covers every action type.
/// </summary>
public abstract class InputModifier
{
    public abstract string Name { get; }

    public virtual float Modify(float value)
    {
        return Modify(new Float2(value, 0f)).X;
    }

    public virtual Float2 Modify(Float2 value)
    {
        var result = Modify(new Float3(value, 0f));
        return new Float2(result.X, result.Y);
    }

    public virtual Float3 Modify(Float3 value)
    {
        return value;
    }

    /// <summary>
    /// Modifies a value of an action of <paramref name="actionType"/> during a frame lasting <paramref name="deltaTime"/>
    /// seconds. This is what the processor calls: by default it calls the overload of the action's dimension (Axis2D →
    /// Float2, Axis3D → Float3, anything else → float, in X), so override it only for a modifier that needs the frame
    /// time, such as <see cref="ModifierScaleByDeltaTime"/>.
    /// </summary>
    public virtual Float3 Modify(Float3 value, InputActionType actionType, float deltaTime)
    {
        switch (actionType)
        {
            case InputActionType.Axis2D:
                var modified = Modify(new Float2(value.X, value.Y));
                return new Float3(modified.X, modified.Y, 0f);
            case InputActionType.Axis3D:
                return Modify(value);
            default:
                return new Float3(Modify(value.X), 0f, 0f);
        }
    }
}
