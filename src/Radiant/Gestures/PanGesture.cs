using System;
using System.Numerics;
using Radiant.Input;
using Radiant.Scrolling;

namespace Radiant.Gestures;

/// <summary>
/// Press-and-drag recognition. Becomes <see cref="GestureState.Possible"/> on pointer
/// down inside bounds, then wants to activate once travel exceeds
/// <see cref="ActivationThreshold"/> on the gated <see cref="Axis"/>. While active it
/// tracks per-frame delta and velocity for the consumer (e.g. scroll drag).
/// </summary>
public sealed class PanGesture : Gesture
{
    public PanGesture() : base(GestureKind.Pan) { }

    /// <summary>Which axis of travel gates activation (also used for directional intent).</summary>
    public ScrollAxes Axis { get; init; } = ScrollAxes.Both;

    /// <summary>Pixels of travel before the pan activates.</summary>
    public float ActivationThreshold { get; set; } = 3f;

    internal override void Observe(in PointerFrame f, bool inside)
    {
        Position = f.Position;
        FrameDelta = f.Delta;
        Dt = f.Dt;
        WantsActivate = false;

        switch (State)
        {
            case GestureState.Idle:
                if (f.PrimaryPressed && inside)
                {
                    State = GestureState.Possible;
                    StartPosition = f.Position;
                    Translation = Vector2.Zero;
                    Velocity = Vector2.Zero;
                }
                break;

            case GestureState.Possible:
                if (!f.PrimaryDown)
                {
                    Fail(); // released without activating — defer to a tap, if any
                    break;
                }
                Translation += f.Delta;
                if (PastThreshold()) WantsActivate = true;
                break;

            case GestureState.Began:
            case GestureState.Active:
                Translation += f.Delta;
                Velocity = f.Dt > 0 ? f.Delta / (float)f.Dt : Vector2.Zero;
                if (f.PrimaryReleased || !f.PrimaryDown) WantsEnd = true;
                break;
        }
    }

    private bool PastThreshold()
    {
        var travel = Axis switch
        {
            ScrollAxes.Vertical => MathF.Abs(Translation.Y),
            ScrollAxes.Horizontal => MathF.Abs(Translation.X),
            _ => Translation.Length(),
        };
        return travel >= ActivationThreshold;
    }
}
