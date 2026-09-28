using System;
using FlaxEngine;

namespace FlaxAIM.Modifiers;

public enum DeadZoneType
{
    /// <summary> Applies the dead zone to the vector's length. </summary>
    Radial,

    /// <summary> Applies the dead zone to each component separately. </summary>
    Axial,
}

public class ModifierDeadZone : InputModifier
{
    [ShowInEditor]
    [EditorOrder(0)]
    public override string Name => "Deadzone";

    [Tooltip("Radial works on the vector's length; Axial works on each component separately. Has no effect on 1D values.")]
    public DeadZoneType Type = DeadZoneType.Radial;

    [Tooltip("Values below this magnitude are treated as zero.")]
    public float LowerThreshold = 0.2f;

    [Tooltip("Values at or above this magnitude are treated as full input. The range in between is remapped to 0-1.")]
    public float UpperThreshold = 0.95f;

    public override float Modify(float value)
    {
        return Math.Sign(value) * Remap(Math.Abs(value));
    }

    public override Float2 Modify(Float2 value)
    {
        if (Type == DeadZoneType.Axial)
            return new Float2(Modify(value.X), Modify(value.Y));

        var length = value.Length;
        return length > 0f ? value * (Remap(length) / length) : value;
    }

    public override Float3 Modify(Float3 value)
    {
        if (Type == DeadZoneType.Axial)
            return new Float3(Modify(value.X), Modify(value.Y), Modify(value.Z));

        var length = value.Length;
        return length > 0f ? value * (Remap(length) / length) : value;
    }

    private float Remap(float magnitude)
    {
        if (magnitude < LowerThreshold)
            return 0f;

        var range = UpperThreshold - LowerThreshold;
        if (range <= 0f)
            return 1f;

        return Mathf.Saturate((magnitude - LowerThreshold) / range);
    }
}
