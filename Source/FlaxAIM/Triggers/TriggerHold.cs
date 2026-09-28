// Example Trigger: Hold (Fires after being held for X seconds)

using FlaxAIM.State;

namespace FlaxAIM.Triggers;

public class TriggerHold : InputTrigger
{
    public float HoldTimeThreshold = 1.0f;
    private float _currentHoldTime;

    public override EnhancedInputActionState UpdateState(InputProcessor input, InputAction action, float deltaTime, float magnitude)
    {
        if (IsActuated(magnitude))
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

    public override void Reset()
    {
        base.Reset();
        _currentHoldTime = 0.0f;
    }
}
