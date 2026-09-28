using FlaxAIM.Modifiers;
using FlaxAIM.Triggers;
using FlaxEngine;

namespace FlaxAIM.State;

public class ActionStateTracker
{
    public Tag InputTag;
    public float CurrentMagnitude;
    public float PreviousMagnitude;

    /// <summary>
    /// The combined trigger state: <see cref="EnhancedInputActionState.None"/>,
    /// <see cref="EnhancedInputActionState.Ongoing"/> or <see cref="EnhancedInputActionState.Triggered"/>.
    /// </summary>
    public EnhancedInputActionState TriggerState = EnhancedInputActionState.None;

    /// <summary>
    /// The events raised by the most recent transition.
    /// </summary>
    public TriggerEvent Events;

    public ProcessedInputActionValue CurrentValue;

    /// <summary>
    /// Whether the action was evaluated this frame. Trackers that were not are flushed by the manager.
    /// </summary>
    internal bool Visited;

    public void MoveToNextFrame()
    {
        PreviousMagnitude = CurrentMagnitude;
        CurrentMagnitude = 0f;
        Events = TriggerEvent.None;
        Visited = false;
    }

    public void AdvanceStateMachine(EnhancedInputActionState evaluation)
    {
        if (evaluation is not (EnhancedInputActionState.Ongoing or EnhancedInputActionState.Triggered))
            evaluation = EnhancedInputActionState.None;

        Events = TransitionEvents(TriggerState, evaluation);
        TriggerState = evaluation;
    }

    // Unreal Enhanced Input transition table.
    private static TriggerEvent TransitionEvents(EnhancedInputActionState from, EnhancedInputActionState to)
    {
        return (from, to) switch
        {
            (EnhancedInputActionState.None, EnhancedInputActionState.Ongoing)        => TriggerEvent.Started | TriggerEvent.Ongoing,
            (EnhancedInputActionState.None, EnhancedInputActionState.Triggered)      => TriggerEvent.Started | TriggerEvent.Triggered,
            (EnhancedInputActionState.Ongoing, EnhancedInputActionState.Ongoing)     => TriggerEvent.Ongoing,
            (EnhancedInputActionState.Ongoing, EnhancedInputActionState.Triggered)   => TriggerEvent.Triggered,
            (EnhancedInputActionState.Ongoing, EnhancedInputActionState.None)        => TriggerEvent.Canceled,
            (EnhancedInputActionState.Triggered, EnhancedInputActionState.Triggered) => TriggerEvent.Triggered,
            (EnhancedInputActionState.Triggered, EnhancedInputActionState.Ongoing)   => TriggerEvent.Ongoing,
            (EnhancedInputActionState.Triggered, EnhancedInputActionState.None)      => TriggerEvent.Completed,
            _                                                                         => TriggerEvent.None,
        };
    }
}
