namespace Radiant.Scrolling;

/// <summary>The motion phase of a single scroll axis.</summary>
public enum AxisPhase
{
    /// <summary>At rest; no motion.</summary>
    Idle,

    /// <summary>Being actively dragged by the user (offset driven directly by input).</summary>
    Dragging,

    /// <summary>Free inertial glide after release, decaying under <see cref="DecelerationRate"/>.</summary>
    Momentum,

    /// <summary>Spring-driven toward a target offset (programmatic scroll, snap settle, or bounce return).</summary>
    Animating,
}
