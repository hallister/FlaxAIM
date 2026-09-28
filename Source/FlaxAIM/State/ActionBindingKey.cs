using System;

namespace FlaxAIM.State;

public struct ActionBindingKey(InputAction action, EnhancedInputActionState targetState) : IEquatable<ActionBindingKey>
{
    public InputAction Action = action;
    public EnhancedInputActionState TargetState = targetState;

    public bool Equals(ActionBindingKey other) => Action == other.Action && TargetState == other.TargetState;
    public override bool Equals(object obj) => obj is ActionBindingKey other && Equals(other);
    
    public override int GetHashCode()
    {
        unchecked
        {
            return ((Action != null ? Action.GetHashCode() : 0) * 397) ^ (int)TargetState;
        }
    }
}