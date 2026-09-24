namespace Radiant.Host;

/// <summary>
/// Pure resolution of a live tear-off follower window's initial size: it should match the source host's
/// current window size (the torn tab was already rendering at that size), falling back to a default when
/// no in-flight drag names a readable source. Side-effect-free (the drag state + bounds reader are passed
/// in) so it can be unit-tested without a window.
/// </summary>
internal static class FollowerWindowSize
{
    /// <summary>
    /// The size to create the follower window at. Uses the active drag session's
    /// <see cref="DragSessionState.SourceHost"/> rect (via <paramref name="readBounds"/>) when available
    /// and positive; otherwise <paramref name="fallback"/>.
    /// </summary>
    public static (int Width, int Height) Resolve(
        DragSessionState? session,
        Func<string, WindowBounds?> readBounds,
        (int Width, int Height) fallback)
    {
        ArgumentNullException.ThrowIfNull(readBounds);
        if (session is { Active: true } s
            && !string.IsNullOrEmpty(s.SourceHost)
            && readBounds(s.SourceHost) is { Width: > 0, Height: > 0 } bounds)
        {
            return (bounds.Width, bounds.Height);
        }
        return fallback;
    }
}
