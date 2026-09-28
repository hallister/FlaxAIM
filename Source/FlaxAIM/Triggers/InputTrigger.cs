using FlaxAIM.State;
using FlaxEngine;

namespace FlaxAIM.Triggers;

/// <summary>
/// How a trigger's result is combined with the other triggers on the same binding.
/// </summary>
public enum TriggerType
{
    /// <summary> The binding triggers if any explicit trigger is triggered. </summary>
    Explicit,

    /// <summary> Every implicit trigger must be triggered for the binding to trigger. </summary>
    Implicit,
}

// Only used in editor.
public interface IInputTrigger {}
// No abstract keyword here! Flax uses this as a generic inline base type

/// <summary>
/// Base trigger. On its own it behaves as a "Down" trigger: triggered every frame the binding is actuated.
/// </summary>
/// <remarks>
/// Trigger objects stored in an <see cref="InputMappingContext"/> asset are templates. Each
/// <see cref="InputProcessor"/> works on its own copies (see <see cref="CreateInstance"/>), so runtime state
/// kept in fields is never shared between processors, contexts or play sessions.
/// </remarks>
public class InputTrigger : IInputTrigger
{
    [Tooltip("An optional actuation barrier threshold value.")]
    public float ActuationThreshold = 0.5f;

    /// <summary>
    /// How this trigger is combined with other triggers on the same binding.
    /// </summary>
    [HideInEditor, NoSerialize]
    public virtual TriggerType TriggerType => TriggerType.Explicit;

    /// <summary>
    /// The binding magnitude this trigger instance saw on the previous frame.
    /// </summary>
    [HideInEditor, NoSerialize]
    protected float PreviousMagnitude { get; private set; }

    protected bool IsActuated(float magnitude) => magnitude >= ActuationThreshold;

    /// <summary>
    /// Evaluates the trigger for this frame. Return <see cref="EnhancedInputActionState.None"/>,
    /// <see cref="EnhancedInputActionState.Ongoing"/> or <see cref="EnhancedInputActionState.Triggered"/>;
    /// other values are treated as None.
    /// </summary>
    /// <param name="input">The processor evaluating the trigger (use it to query other actions).</param>
    /// <param name="action">The action the binding belongs to.</param>
    /// <param name="deltaTime">Frame delta time in seconds.</param>
    /// <param name="magnitude">The binding's modified magnitude this frame.</param>
    public virtual EnhancedInputActionState UpdateState(InputProcessor input, InputAction action, float deltaTime, float magnitude)
    {
        return IsActuated(magnitude) ? EnhancedInputActionState.Triggered : EnhancedInputActionState.None;
    }

    /// <summary>
    /// Clears runtime state. Overrides must call the base implementation.
    /// </summary>
    public virtual void Reset()
    {
        PreviousMagnitude = 0f;
    }

    /// <summary>
    /// Creates the runtime copy of this template. The default is a shallow copy, so override it if a
    /// subclass keeps mutable reference-type state.
    /// </summary>
    public virtual InputTrigger CreateInstance()
    {
        var instance = (InputTrigger)MemberwiseClone();
        instance.Reset();
        return instance;
    }

    internal EnhancedInputActionState Evaluate(InputProcessor input, InputAction action, float deltaTime, float magnitude)
    {
        var state = UpdateState(input, action, deltaTime, magnitude);
        PreviousMagnitude = magnitude;
        return state;
    }
}
