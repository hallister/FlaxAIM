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
}
