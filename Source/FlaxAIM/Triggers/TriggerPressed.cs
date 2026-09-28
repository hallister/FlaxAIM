// Example Trigger: Pressed (Fires once, on the frame the binding becomes actuated)

using FlaxAIM.State;

namespace FlaxAIM.Triggers;

public class TriggerPressed : InputTrigger
{
    public override EnhancedInputActionState UpdateState(InputProcessor input, InputAction action, float deltaTime, float magnitude)
    {
        if (IsActuated(magnitude) && !IsActuated(PreviousMagnitude))
            return EnhancedInputActionState.Triggered;

        return EnhancedInputActionState.None;
    }
}
