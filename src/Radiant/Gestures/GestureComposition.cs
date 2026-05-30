namespace Radiant.Gestures;

/// <summary>
/// Declarative gesture composition, mirroring React Native Gesture Handler's
/// <c>Race</c> / <c>Exclusive</c> / <c>Simultaneous</c>. Each helper wires the
/// appropriate cross-gesture relations and returns the set to hand to a
/// <see cref="GestureDetector"/>. List order is priority.
/// </summary>
public static class GestureComposition
{
    /// <summary>
    /// First gesture to activate wins; the arbiter's default single-owner behaviour.
    /// Order is priority when two want to activate on the same frame.
    /// </summary>
    public static Gesture[] Race(params Gesture[] gestures) => gestures;

    /// <summary>
    /// Priority fallback chain: each gesture activates only once all higher-priority
    /// gestures (earlier in the list) have failed.
    /// </summary>
    public static Gesture[] Exclusive(params Gesture[] gestures)
    {
        for (var i = 1; i < gestures.Length; i++)
            for (var j = 0; j < i; j++)
                gestures[i].RequireToFail(gestures[j]);
        return gestures;
    }

    /// <summary>
    /// Gestures that should not cancel one another. (The arbiter keeps a single primary
    /// owner; this relation prevents mutual cancellation. Full multi-owner simultaneity
    /// is a future extension.)
    /// </summary>
    public static Gesture[] Simultaneous(params Gesture[] gestures)
    {
        foreach (var a in gestures)
            foreach (var b in gestures)
                if (!ReferenceEquals(a, b))
                    a.SimultaneousWith(b);
        return gestures;
    }
}
