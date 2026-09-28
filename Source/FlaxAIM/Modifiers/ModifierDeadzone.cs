using FlaxEngine;

namespace FlaxAIM.Modifiers;

public class ModifierDeadZone : InputModifier
{
    [ShowInEditor]
    [EditorOrder(0)]
    public override string Name => "Deadzone";
    
    // The JSON engine will automatically map values into these fields from the struct
    public float LowerThreshold = 0.2f;
    public float UpperThreshold = 0.95f;
    
    public override float Modify(float currentVector)
    {
        Debug.LogWarning($"[ModifierDeadZone] Cannot apply a deadzone modifier to a Digital input.");
        return currentVector;
    }

    public override Float2 Modify(Float2 currentVector)
    {
        Debug.LogWarning($"[ModifierDeadZone] Cannot apply a deadzone modifier to a Axis2D input.");
        return currentVector;
    }

    public override Float3 Modify(Float3 currentVector)
    {
        float length = currentVector.Length;
        
        // Inside the lower bound deadzone -> clip to zero
        if (length < LowerThreshold) 
            return Float3.Zero;
            
        // Outside the upper bound deadzone -> clamp to max length boundary
        if (length > UpperThreshold) 
            return currentVector.Normalized * UpperThreshold;
            
        return currentVector;
    }
}
