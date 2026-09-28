using System;

namespace FlaxAIM.State;

/// <summary>
/// Keys a callback list by the action's <see cref="InputAction.ID"/> rather than the instance, so bindings
/// survive the action asset being reloaded.
/// </summary>
public struct ActionBindingKey(Guid actionId, EnhancedInputActionState targetState) : IEquatable<ActionBindingKey>
{
    public Guid ActionId = actionId;
    public EnhancedInputActionState TargetState = targetState;

    public bool Equals(ActionBindingKey other) => ActionId == other.ActionId && TargetState == other.TargetState;
    public override bool Equals(object obj) => obj is ActionBindingKey other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(ActionId, (int)TargetState);
}
