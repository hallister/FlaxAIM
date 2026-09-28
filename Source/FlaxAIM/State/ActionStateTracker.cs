using FlaxAIM.Modifiers;
using FlaxEngine;

namespace FlaxAIM.State;

public class ActionStateTracker
{
    public Tag InputTag;
    public float CurrentMagnitude;
    public float PreviousMagnitude;
    public EnhancedInputActionState CurrentState = EnhancedInputActionState.None;
    public ProcessedInputActionValue CurrentValue;

    public void MoveToNextFrame()
    {
        PreviousMagnitude = CurrentMagnitude;
        CurrentMagnitude = 0f;
    }

    public void AdvanceStateMachine(EnhancedInputActionState evaluation)
    {
        CurrentState = TransitionLogic(CurrentState, evaluation);
    }

    private EnhancedInputActionState TransitionLogic(EnhancedInputActionState oldState, EnhancedInputActionState triggerEvaluation)
    {
        switch (triggerEvaluation)
        {
            // Unreal State Rule Matrix Transformation
            case EnhancedInputActionState.Triggered when oldState is EnhancedInputActionState.None or EnhancedInputActionState.Canceled or EnhancedInputActionState.Completed:
                return EnhancedInputActionState.Started;
            case EnhancedInputActionState.Triggered:
                return EnhancedInputActionState.Triggered;
            case EnhancedInputActionState.Ongoing when oldState == EnhancedInputActionState.None:
                return EnhancedInputActionState.Started;
            case EnhancedInputActionState.Ongoing:
                return EnhancedInputActionState.Ongoing;
        }

        // Context turned off completely this frame
        if (oldState is EnhancedInputActionState.Ongoing or EnhancedInputActionState.Started)
            return EnhancedInputActionState.Canceled;

        if (oldState == EnhancedInputActionState.Triggered)
            return EnhancedInputActionState.Completed;

        return EnhancedInputActionState.None;
    }
}