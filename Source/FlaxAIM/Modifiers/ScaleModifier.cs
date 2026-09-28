using FlaxEngine;

namespace FlaxAIM.Modifiers;

public class ModifierScale : InputModifier
{
    [ShowInEditor]
    [EditorOrder(0)]
    public override string Name => "Scale";
    
    [Tooltip("Scalar multiplier applied to the X axis / 1D values.")]
    public float ScaleX = 1.0f;

    [Tooltip("Scalar multiplier applied to the Y axis.")]
    public float ScaleY = 1.0f;

    [Tooltip("Scalar multiplier applied to the Z axis.")]
    public float ScaleZ = 1.0f;

    public override float Modify(float value) 
        => value * ScaleX;

    public override Float2 Modify(Float2 value) 
        => new Float2(value.X * ScaleX, value.Y * ScaleY);

    public override Float3 Modify(Float3 value) 
        => new Float3(value.X * ScaleX, value.Y * ScaleY, value.Z * ScaleZ);
}
