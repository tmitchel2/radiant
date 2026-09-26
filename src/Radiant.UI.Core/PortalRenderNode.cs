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
            YogaStyle.Set(this, Yoga, Element.Layout, reset: old is not null);
        }
        // Its content is laid out under the root, not where the portal is in the tree: it takes the
        // direction in force there (a menu in a right-to-left app reads right to left).
        if (Element.Layout.Direction is null && DirectionHere() is { } direction)
        {
            Facebook.Yoga.YGNodeStyleAPI.YGNodeStyleSetDirection(Yoga,
                direction == Radiant.Text.TextDirection.RightToLeft ? Facebook.Yoga.YGDirection.RTL : Facebook.Yoga.YGDirection.LTR);
        }
    }

    private Radiant.Text.TextDirection? DirectionHere()
    {
        for (var node = Owner.Parent; node is not null; node = node.Parent)
        {
            if (node.Element is Provider<Radiant.Text.TextDirection?> provider && ReferenceEquals(provider.Context, Directionality.Context))
            {
                return provider.Value;
            }
        }
        return null;
    }

    public override void Dispose()
    {
        Owner.Root.RemovePortal(this);
        base.Dispose();
    }
}
