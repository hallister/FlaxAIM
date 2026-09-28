using System;

namespace FlaxAIM.Triggers;

[Flags]
public enum TriggerEvent
{
    None      = 0,
    Started   = 1 << 0,
    Ongoing   = 1 << 1,
    Completed = 1 << 2,
    Canceled  = 1 << 3
}
