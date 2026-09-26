using System;
using Facebook.Yoga;
using static Facebook.Yoga.YGNodeAPI;

namespace Radiant.UI.Core;

/// <summary>Lays out and draws an <see cref="Image"/>.</summary>
internal sealed class ImageRenderNode : RenderNode
{
    public ImageRenderNode() => YGNodeSetMeasureFunc(Yoga, Measure);

    public Image Element { get; private set; } = null!;

    public override void Update(HostElement element, HostElement? previous)
    {
        var old = previous as Image;
        Element = (Image)element;
        if (old is null || old.Layout != Element.Layout)
        {
            YogaStyle.Set(Yoga, Element.Layout, reset: old is not null);
        }
        if (old is null || !ReferenceEquals(old.Source, Element.Source))
        {
            YGNodeMarkDirty(Yoga);
        }
    }

    public override void Paint(PaintContext context)
    {
        var image = Element;
        var (x, y) = (context.Origin.X, context.Origin.Y);
        var (width, height) = (Size.X, Size.Y);
        var source = image.Source;
        if (width <= 0f || height <= 0f || source.Width == 0 || source.Height == 0)
        {
            return;
        }
        // Where the whole picture goes: covering or inside the box, or stretched to it.
        var scale = image.Fit switch
        {
            ImageFit.Cover => MathF.Max(width / source.Width, height / source.Height),
            ImageFit.Contain => MathF.Min(width / source.Width, height / source.Height),
            _ => 1f,
        };
        var (w, h) = image.Fit == ImageFit.Fill ? (width, height) : (source.Width * scale, source.Height * scale);
        var renderer = context.Renderer;
        renderer.PushClip(x, y, width, height, image.CornerRadii);
        renderer.DrawImage(source.TextureFor(renderer), x + (width - w) / 2f, y + (height - h) / 2f, w, h);
        renderer.PopClip();
    }

    private YGSize Measure(Node node, float width, MeasureMode widthMode, float height, MeasureMode heightMode)
    {
        var source = Element.Source;
        float w = source.Width, h = source.Height;
        // Given one side, keep the proportions for the other.
        if (widthMode == MeasureMode.Exactly && heightMode != MeasureMode.Exactly && source.Width > 0)
        {
            (w, h) = (width, width * source.Height / source.Width);
        }
        else if (heightMode == MeasureMode.Exactly && widthMode != MeasureMode.Exactly && source.Height > 0)
        {
            (w, h) = (height * source.Width / source.Height, height);
        }
        if (widthMode == MeasureMode.AtMost && w > width)
        {
            (w, h) = (width, source.Width > 0 ? width * source.Height / source.Width : h);
        }
        return new YGSize { Width = widthMode == MeasureMode.Exactly ? width : w, Height = heightMode == MeasureMode.Exactly ? height : h };
    }
}
