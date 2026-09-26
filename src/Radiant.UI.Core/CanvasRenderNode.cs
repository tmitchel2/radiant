namespace Radiant.UI.Core;

/// <summary>Lays out a <see cref="Canvas"/> and calls its paint callback.</summary>
internal sealed class CanvasRenderNode : RenderNode
{
    public Canvas Element { get; private set; } = null!;

    public override void Update(HostElement element, HostElement? previous)
    {
        var old = previous as Canvas;
        Element = (Canvas)element;
        if (old is null || old.Layout != Element.Layout)
        {
            YogaStyle.Set(Yoga, Element.Layout, reset: old is not null);
        }
    }

    public override void Paint(PaintContext context) => Element.Paint(context, context.Origin, Size);
}
