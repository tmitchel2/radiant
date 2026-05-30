using System.Numerics;
using Silk.NET.Input;

namespace Radiant.Input;

/// <summary>
/// Immutable per-frame pointer snapshot synthesised from <see cref="InputState"/> and
/// handed to the gesture system. Collapses the polled, single-pointer Radiant input
/// model into the one "event" a gesture recognises each frame. Single-pointer today;
/// a multi-touch host would extend this with pointer id/count without reshaping the
/// gesture API.
/// </summary>
public readonly record struct PointerFrame(
    Vector2 Position,
    Vector2 Delta,
    Vector2 Wheel,
    bool PrimaryDown,
    bool PrimaryPressed,
    bool PrimaryReleased,
    double Dt)
{
    /// <summary>Project the current <see cref="InputState"/> into a frame snapshot.</summary>
    public static PointerFrame From(InputState s, double dt) => new(
        s.MousePosition,
        s.MouseDelta,
        s.ScrollDelta,
        s.IsLeftMouseDown,
        s.IsMouseButtonPressed(MouseButton.Left),
        s.IsMouseButtonReleased(MouseButton.Left),
        dt);
}
