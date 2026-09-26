using System.Numerics;

namespace Radiant.UI.Core;

/// <summary>Lays out and draws a <see cref="Box"/>.</summary>
internal sealed class BoxRenderNode : RenderNode
{
    public Box Element { get; private set; } = null!;

    public override Matrix3x2? LocalTransform
    {
        get
        {
            if (Element.Transform is not { } transform)
            {
                return null;
            }
            var origin = Size * Element.TransformOrigin;
            return Matrix3x2.CreateTranslation(-origin) * transform * Matrix3x2.CreateTranslation(origin);
        }
    }

    public override bool ClipsChildren => Element.ClipContent;

    public override bool IsHitTestVisible => Element.HitTestVisible;

    public override void Update(HostElement element, HostElement? previous)
    {
        Element = (Box)element;
        var old = previous as Box;
        if ((old?.TrapFocus ?? false) != Element.TrapFocus)
        {
            if (Element.TrapFocus)
            {
                Owner.Root.AddFocusTrap(this);
            }
            else
            {
                Owner.Root.RemoveFocusTrap(this);
            }
        }
        if (!ReferenceEquals(old?.Ref, Element.Ref))
        {
            if (old?.Ref is { } oldRef && ReferenceEquals(oldRef.Node, this))
            {
                oldRef.Node = null;
            }
            if (Element.Ref is { } newRef)
            {
                newRef.Node = this;
            }
        }
        if (old is null || old.Layout != Element.Layout)
        {
            YogaStyle.Set(this, Yoga, Element.Layout, reset: old is not null);
        }
    }

    public override void Dispose()
    {
        Owner.Root.RemoveFocusTrap(this);
        if (Element?.Ref is { } elementRef && ReferenceEquals(elementRef.Node, this))
        {
            elementRef.Node = null;
        }
        base.Dispose();
    }

    public override void Paint(PaintContext context)
    {
        var box = Element;
        if (box.Opacity <= 0f)
        {
            return;
        }
        var renderer = context.Renderer;
        var (x, y) = (context.Origin.X, context.Origin.Y);
        var (width, height) = (Size.X, Size.Y);
        var layered = box.Opacity < 1f;
        if (layered)
        {
            renderer.PushLayer(box.Opacity);
        }

        foreach (var shadow in box.Shadows)
        {
            renderer.DrawShadow(x, y, width, height, box.CornerRadii, shadow.Blur, shadow.Color, shadow.Offset, shadow.Spread);
        }
        if (box.BackgroundGradient is { } gradient)
        {
            renderer.DrawRoundedRect(x, y, width, height, box.CornerRadii, box.BorderWidth, gradient.Translated(context.Origin), box.BorderColor);
        }
        else if (box.Background is not null || box.BorderWidth > 0f)
        {
            renderer.DrawRoundedRect(x, y, width, height, box.CornerRadii, box.BorderWidth,
                box.Background ?? Vector4.Zero, box.BorderColor);
        }

        if (box.ClipContent)
        {
            renderer.PushClip(x, y, width, height, box.CornerRadii);
        }
        PaintChildren(context);
        if (box.ClipContent)
        {
            renderer.PopClip();
        }
        if (layered)
        {
            renderer.PopLayer();
        }
    }
}
