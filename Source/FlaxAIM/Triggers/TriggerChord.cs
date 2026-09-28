using FlaxAIM.State;
using FlaxEngine;

namespace FlaxAIM.Triggers;

/// <summary>
/// Implicit trigger: the binding can only trigger while another action (the chord) is active.
/// </summary>
/// <remarks>
/// The compiler evaluates chord actions before the actions that depend on them, so this reads the chord's
/// state for the current frame.
/// </remarks>
public class TriggerChord : InputTrigger
{
    [Tooltip("The prerequisite input action that must be active for this trigger to succeed.")]
    public JsonAssetReference<InputAction> ChordAction;

    [Tooltip("The state the chord action must be in. Triggered requires it to be triggered; Started or Ongoing also accept it while it is ongoing.")]
    public EnhancedInputActionState RequiredChordState = EnhancedInputActionState.Triggered;

    [HideInEditor, NoSerialize]
    public override TriggerType TriggerType => TriggerType.Implicit;

    public override EnhancedInputActionState UpdateState(InputManager manager, InputAction action, float deltaTime, float magnitude)
    {
        var chord = ChordAction.Instance;
        if (chord == null) return EnhancedInputActionState.None;

        var chordState = manager.GetActionState(chord);
        var acceptsOngoing = RequiredChordState is EnhancedInputActionState.Started or EnhancedInputActionState.Ongoing;

        var satisfied = chordState == EnhancedInputActionState.Triggered
                        || (acceptsOngoing && chordState == EnhancedInputActionState.Ongoing);

        return satisfied ? EnhancedInputActionState.Triggered : EnhancedInputActionState.None;
    }
}
