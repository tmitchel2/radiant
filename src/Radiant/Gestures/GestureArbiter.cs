using System.Collections.Generic;
using Radiant.Input;

namespace Radiant.Gestures;

/// <summary>
/// Resolves a single active gesture owner per frame from a set of competing gestures.
/// Negotiation collapses RNGH's multi-event capture/bubble into one polled pass:
/// observe every gesture, then grant ownership to the first eligible candidate that
/// wants to activate (list order = priority). Ownership is <em>sticky</em> — an active
/// gesture is never stolen mid-interaction. Honours <see cref="Gesture.RequireToFail"/>,
/// <see cref="Gesture.BlocksGesture"/>, and <see cref="Gesture.SimultaneousWith"/>.
/// </summary>
public sealed class GestureArbiter
{
    /// <summary>The gesture currently driving the interaction, if any.</summary>
    public Gesture? Owner { get; private set; }

    public void Update(IReadOnlyList<Gesture> gestures, in PointerFrame frame, bool inside)
    {
        for (var i = 0; i < gestures.Count; i++) gestures[i].ClearTerminal();

        // Sticky ownership: drive the active owner, never re-negotiate while it holds.
        if (Owner != null)
        {
            Owner.Observe(frame, EffectiveInside(Owner, frame, inside));
            if (Owner.WantsEnd) { Owner.EndActive(); Owner = null; }
            else if (Owner.State == GestureState.Failed) { Owner = null; }
            else { Owner.Continue(); }
            return;
        }

        for (var i = 0; i < gestures.Count; i++)
            gestures[i].Observe(frame, EffectiveInside(gestures[i], frame, inside));

        Gesture? winner = null;
        for (var i = 0; i < gestures.Count; i++)
        {
            if (gestures[i].WantsActivate && Eligible(gestures[i])) { winner = gestures[i]; break; }
        }
        if (winner == null) return;

        winner.Grant();

        for (var i = 0; i < gestures.Count; i++)
        {
            var g = gestures[i];
            if (g == winner) continue;
            var contend = g.WantsActivate || g.State == GestureState.Possible || g.IsActive;
            if (winner.Blocked.Contains(g) || (contend && !winner.Simultaneous.Contains(g)))
                g.CancelActive();
        }

        if (winner.Instantaneous) { winner.EndActive(); Owner = null; }
        else { Owner = winner; winner.Continue(); }
    }

    public void Reset() => Owner = null;

    private static bool EffectiveInside(Gesture g, in PointerFrame frame, bool detectorInside) =>
        g.HitArea?.Invoke(frame.Position) ?? detectorInside;

    private static bool Eligible(Gesture g)
    {
        foreach (var r in g.RequiredToFail)
            if (r.State is GestureState.Possible or GestureState.Began or GestureState.Active)
                return false;
        return true;
    }
}
