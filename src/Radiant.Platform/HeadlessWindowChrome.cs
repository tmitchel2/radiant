namespace Radiant.Platform;

/// <summary>Window chrome without a window: it records what's asked of it, for tests.</summary>
public sealed class HeadlessWindowChrome : IWindowChrome
{
    /// <inheritdoc/>
    public bool IsSupported { get; set; }

    /// <inheritdoc/>
    public bool ExtendsIntoTitleBar { get; private set; }

    /// <inheritdoc/>
    public float TitleBarHeight { get; set; } = 28f;

    /// <inheritdoc/>
    public float LeadingInset { get; set; }

    /// <inheritdoc/>
    public float TrailingInset { get; set; }

    /// <summary>How many times a drag began.</summary>
    public int DragCount { get; private set; }

    /// <summary>How many double clicks were passed on.</summary>
    public int DoubleClickCount { get; private set; }

    /// <inheritdoc/>
    public void ExtendIntoTitleBar(bool extend) => ExtendsIntoTitleBar = extend && IsSupported;

    /// <inheritdoc/>
    public void BeginDrag() => DragCount++;

    /// <inheritdoc/>
    public void TitleBarDoubleClick() => DoubleClickCount++;
}
