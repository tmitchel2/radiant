namespace Radiant.Gestures;

/// <summary>
/// Lifecycle state of a <see cref="Gesture"/>, mirroring the React Native Gesture
/// Handler state machine.
/// </summary>
public enum GestureState
{
    /// <summary>Inactive; awaiting a trigger.</summary>
    Idle,

    /// <summary>Triggered (e.g. pointer down inside bounds) but not yet recognised.</summary>
    Possible,

    /// <summary>Recognised this frame; the activation edge.</summary>
    Began,

    /// <summary>Recognised and ongoing.</summary>
    Active,

    /// <summary>Completed successfully (e.g. pointer released).</summary>
    Ended,

    /// <summary>Did not meet its recognition criteria.</summary>
    Failed,

    /// <summary>Forcibly terminated because another gesture won.</summary>
    Cancelled,
}
