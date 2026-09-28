using System;
using FlaxEngine;

namespace FlaxAIM.Modifiers;

public abstract class InputModifier
{
    public abstract string Name { get; }

    public virtual float Modify(float value)
    {
        throw new NotImplementedException("[InputModifier] At least one Modify method must be overloaded.");
    }
    
    public virtual Float2 Modify(Float2 value)
    {
        throw new NotImplementedException("[InputModifier] At least one Modify method must be overloaded.");
    }
    
    public virtual Float3 Modify(Float3 value)
    {
        throw new NotImplementedException("[InputModifier] At least one Modify method must be overloaded.");
    }
}