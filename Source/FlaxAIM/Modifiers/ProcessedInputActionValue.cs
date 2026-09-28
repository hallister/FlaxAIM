using FlaxEngine;

namespace FlaxAIM.Modifiers;

public struct ProcessedInputActionValue()
{
    public float Axis1D = 0f;
    public Float2 Axis2D = Float2.Zero;
    public Float3 Axis3D = Float3.Zero;
    public bool Digital = false;

    public static ProcessedInputActionValue Default = new ProcessedInputActionValue();
}