namespace Radiant.Layout;

/// <summary>
/// Marks an <see cref="Radiant.UI.IUiContainer"/> that the layout engine should size and position
/// like any flex item, but whose <em>children</em> it must not lay out. The subtree walk stops here,
/// leaving the container free to position its own children by other means (e.g. <c>ScrollPanel</c>'s
/// manual, scroll-shifted children). Without this, opting a root into layout would recursively
/// re-position a scroll region's contents and fight its scroll offset.
/// </summary>
public interface ILayoutBoundary
{
}
