namespace Radiant.Gestures;

/// <summary>The recognised kind of a gesture (for defaults and debugging).</summary>
public enum GestureKind
{
    /// <summary>Press-and-drag.</summary>
    Pan,

    /// <summary>Press-and-release without significant travel.</summary>
    Tap,

    /// <summary>Mouse-wheel pulse.</summary>
    Wheel,
}
