// Example Trigger: Down / Pressed (Fires instantly on actuation)

using FlaxAIM.State;

namespace FlaxAIM.Triggers;

public class TriggerPressed : InputTrigger
{
    public override EnhancedInputActionState UpdateState(InputManager manager, InputAction action, float deltaTime, float rawMagnitude)
    {
        var isActuated = rawMagnitude >= ActuationThreshold;
        var wasActuatedLastFrame = manager.GetPreviousFrameMagnitude(action) >= ActuationThreshold;

        if (isActuated && !wasActuatedLastFrame)
            return EnhancedInputActionState.Triggered;

        return EnhancedInputActionState.None;
    }
}
