using Radiant.Input;

namespace Radiant.Gestures;

/// <summary>
/// Binds a set of competing gestures to a widget and drives a <see cref="GestureArbiter"/>
/// each frame. Exposes <see cref="HasActiveOrClaimingOwner"/> — the signal a widget's
/// <c>IsCapturingInput</c> derives from, so input the detector is recognising can't fall
/// through to whatever lies underneath.
/// </summary>
public sealed class GestureDetector
{
    private readonly Gesture[] _gestures;
    private readonly GestureArbiter _arbiter = new();

    public GestureDetector(params Gesture[] gestures) => _gestures = gestures;

    /// <summary>The active owning gesture, if any.</summary>
    public Gesture? Owner => _arbiter.Owner;

    /// <summary>True when a gesture is active, or one is mid-recognition (pressed or wanting to activate).</summary>
    public bool HasActiveOrClaimingOwner
    {
        get
        {
            if (_arbiter.Owner != null) return true;
            foreach (var g in _gestures)
                if (g.WantsActivate || g.State == GestureState.Possible) return true;
            return false;
        }
    }

    public void Update(in PointerFrame frame, bool inside) => _arbiter.Update(_gestures, frame, inside);

    public void Reset() => _arbiter.Reset();
}
