namespace FlaxAIM.State;

public enum EnhancedInputActionState
{
    /// <summary> No input activity detected. </summary>
    None,

    /// <summary> Input has been actuated this frame but has not met full trigger requirements yet. </summary>
    Started,

    /// <summary> Trigger conditions are actively being evaluated over time (e.g., holding down a button). </summary>
    Ongoing,

    /// <summary> All trigger requirements have been successfully met this frame. </summary>
    Triggered,

    /// <summary> Actuation stopped after a successful trigger event completed. </summary>
    Completed,

    /// <summary> Actuation stopped or failed before meeting full trigger requirements. </summary>
    Canceled
}