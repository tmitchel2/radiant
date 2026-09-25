namespace Radiant.Host;

/// <summary>
/// Pure placement math for the floating drag overlay: where to put the (small) overlay window so its
/// ghost tracks the global cursor. Kept side-effect-free so it can be unit-tested.
/// </summary>
internal static class DragOverlayPlacement
{
    /// <summary>The overlay window's top-left (screen coordinates) so its centre lands on the cursor.</summary>
    public static (int X, int Y) WindowTopLeft(float cursorX, float cursorY, int width, int height) =>
        ((int)MathF.Round(cursorX - width / 2f), (int)MathF.Round(cursorY - height / 2f));
}
