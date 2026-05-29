using System.Numerics;
using Radiant.Input;

namespace Radiant.Gestures;

/// <summary>
/// Press-and-release recognition. Activates on release inside bounds provided the
/// pointer travelled less than <see cref="MaxTravel"/>. Instantaneous: completes in the
/// frame it activates (fires <see cref="Gesture.OnBegin"/> then <see cref="Gesture.OnEnd"/>)
/// and never retains ownership. Compose with a <see cref="PanGesture"/> via
/// <see cref="Gesture.RequireToFail"/> so a drag doesn't also register as a tap.
/// </summary>
public sealed class TapGesture : Gesture
{
    public TapGesture() : base(GestureKind.Tap) { }

    /// <summary>Maximum pointer travel still considered a tap.</summary>
    public float MaxTravel { get; set; } = 5f;

    internal override bool Instantaneous => true;

    internal override void Observe(in PointerFrame f, bool inside)
    {
        Position = f.Position;
        WantsActivate = false;

        switch (State)
        {
            case GestureState.Idle:
                if (f.PrimaryPressed && inside)
                {
                    State = GestureState.Possible;
                    StartPosition = f.Position;
                    Translation = Vector2.Zero;
                }
                break;

            case GestureState.Possible:
                if (f.PrimaryDown)
                {
                    Translation += f.Delta;
                    if (Translation.Length() > MaxTravel) Fail();
                }
                else
                {
                    if (inside && Translation.Length() <= MaxTravel) WantsActivate = true;
                    else Fail();
                }
                break;
        }
    }
}
