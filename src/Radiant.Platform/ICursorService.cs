namespace Radiant.Platform;

/// <summary>
/// Sets the pointer's shape over the window. The UI decides the shape from what the pointer is
/// over (<c>Box.Cursor</c>); this is only how that decision reaches the screen.
/// </summary>
public interface ICursorService
{
    /// <summary>The shape shown last.</summary>
    CursorShape Current { get; }

    /// <summary>Shows <paramref name="shape"/> while the pointer is over the window.</summary>
    void Show(CursorShape shape);
}
