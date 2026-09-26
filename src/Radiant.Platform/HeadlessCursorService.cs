namespace Radiant.Platform;

/// <summary>Cursors with nothing to show them on: the shape is recorded, and counted, for tests.</summary>
public sealed class HeadlessCursorService : ICursorService
{
    /// <inheritdoc/>
    public CursorShape Current { get; private set; }

    /// <summary>How many times <see cref="Show"/> was called, to check the UI doesn't set the cursor needlessly.</summary>
    public int ShowCount { get; private set; }

    /// <inheritdoc/>
    public void Show(CursorShape shape)
    {
        Current = shape;
        ShowCount++;
    }
}
