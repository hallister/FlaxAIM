using FlaxAIM.State;
using FlaxEngine;

namespace FlaxAIM.Triggers;

public class TriggerChord : InputTrigger
{
    [Tooltip("The prerequisite input action that must be active for this trigger to succeed.")]
    public JsonAssetReference<InputAction> ChordAction;

    [Tooltip("The minimum state required for the chord action to be considered active.")]
    public EnhancedInputActionState RequiredChordState = EnhancedInputActionState.Triggered;
    
    public override EnhancedInputActionState UpdateState(InputManager manager, InputAction action, float deltaTime, float rawMagnitude)
    {
        var chord = ChordAction.Instance;
        if (chord == null) return EnhancedInputActionState.None;

        // Fetch the current active state machine flag for our prerequisite action
        var activeChordState = manager.GetActionState(chord);

        // If the prerequisite button/action isn't being held/triggered, this whole mapping fails
        if (activeChordState != RequiredChordState && activeChordState != EnhancedInputActionState.Ongoing)
        {
            return EnhancedInputActionState.None;
        }

        // Prerequisite met! Fallback to default actuation evaluation rules
        if (rawMagnitude >= ActuationThreshold)
        {
            var wasActuated = manager.GetPreviousFrameMagnitude(action) >= ActuationThreshold;
            return wasActuated ? EnhancedInputActionState.Ongoing : EnhancedInputActionState.Triggered;
        }

        return EnhancedInputActionState.None;
    }
}
