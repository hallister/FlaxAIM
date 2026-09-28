using System;
using FlaxEngine;

namespace FlaxAIM.State;

public struct RegisteredCallbackHandler(Tag filterTag, Action<Tag> actionDelegate) : IEquatable<RegisteredCallbackHandler>
{
    public Tag FilterTag = filterTag;
    public readonly Action<Tag> ActionDelegate = actionDelegate;

    public bool Equals(RegisteredCallbackHandler other)
    {
        return FilterTag.Equals(other.FilterTag) && Equals(ActionDelegate, other.ActionDelegate);
    }

    public override bool Equals(object obj)
    {
        return obj is RegisteredCallbackHandler other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(FilterTag, ActionDelegate);
    }

    public static bool operator ==(RegisteredCallbackHandler left, RegisteredCallbackHandler right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(RegisteredCallbackHandler left, RegisteredCallbackHandler right)
    {
        return !(left == right);
    }
}