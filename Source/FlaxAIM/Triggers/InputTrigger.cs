using FlaxAIM.State;
using FlaxEngine;

namespace FlaxAIM.Triggers;

// Only used in editor.
public interface IInputTrigger {}
// No abstract keyword here! Flax uses this as a generic inline base type

public class InputTrigger : IInputTrigger
{
    [Tooltip("An optional actuation barrier threshold value.")]
    public float ActuationThreshold = 0.5f;

    public virtual EnhancedInputActionState UpdateState(InputManager manager, InputAction action, float deltaTime, float rawMagnitude)
    {
        // Default baseline behavior matches explicit pressed actuation states
        if (rawMagnitude >= ActuationThreshold)
        {
            var wasActuated = manager.GetPreviousFrameMagnitude(action) >= ActuationThreshold;
            return wasActuated ? EnhancedInputActionState.Ongoing : EnhancedInputActionState.Triggered;
        }
        return EnhancedInputActionState.None;
    }
}
