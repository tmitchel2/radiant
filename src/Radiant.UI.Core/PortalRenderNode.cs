namespace Radiant.UI.Core;

/// <summary>
/// A <see cref="Portal"/>'s layer: a child of the root's render node rather than of its nearest
/// host, so it is laid out, drawn and hit-tested above the app. Only its children can be hit.
/// </summary>
internal sealed class PortalRenderNode : RenderNode
{
    public Portal Element { get; private set; } = null!;

    public override bool IsHitTestVisible => false;

    public override void Update(HostElement element, HostElement? previous)
    {
        var old = previous as Portal;
        Element = (Portal)element;
        if (old is null)
        {
            Owner.Root.AddPortal(this);
        }
        if (old is null || old.Layout != Element.Layout)
        {
            YogaStyle.Set(Yoga, Element.Layout, reset: old is not null);
        }
    }

    public override void Dispose()
    {
        Owner.Root.RemovePortal(this);
        base.Dispose();
    }
}
