// Example Trigger: Hold (Fires after being held for X seconds)

using FlaxAIM.State;

namespace FlaxAIM.Triggers;

public class TriggerHold : InputTrigger
{
    public float HoldTimeThreshold = 1.0f;
    private float _currentHoldTime;

    public override EnhancedInputActionState UpdateState(InputManager manager, InputAction action, float deltaTime, float rawMagnitude)
    {
        if (rawMagnitude >= ActuationThreshold)
        {
            _currentHoldTime += deltaTime;
            if (_currentHoldTime >= HoldTimeThreshold)
            {
                return EnhancedInputActionState.Triggered;
            }
            return EnhancedInputActionState.Ongoing;
        }

        _currentHoldTime = 0.0f;
        return EnhancedInputActionState.None;
    }
}
