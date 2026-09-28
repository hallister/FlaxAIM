using System;

namespace FlaxAIM.Triggers;

/// <summary>
/// The events an action raised during a frame. Several can be raised at once
/// (for example Started | Triggered on the first frame of a button press).
/// </summary>
[Flags]
public enum TriggerEvent
{
    None      = 0,
    Started   = 1 << 0,
    Ongoing   = 1 << 1,
    Completed = 1 << 2,
    Canceled  = 1 << 3,
    Triggered = 1 << 4,
}
