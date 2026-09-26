using System;
using Facebook.Yoga;
using Radiant.Text;
using static Facebook.Yoga.YGNodeAPI;
using static Facebook.Yoga.YGNodeLayoutAPI;

namespace Radiant.UI.Core;

/// <summary>
/// Lays out and draws a <see cref="TextBlock"/>. Yoga asks it for its size at a width (its measure
/// function); the text is shaped once per content, and laid out again only at a new width.
/// </summary>
internal sealed class TextRenderNode : RenderNode
{
    private Paragraph? _shaped;
    private Paragraph? _laidOut;

    public TextRenderNode() => YGNodeSetMeasureFunc(Yoga, Measure);

    public TextBlock Element { get; private set; } = null!;

    public override void Update(HostElement element, HostElement? previous)
    {
        Element = (TextBlock)element;
        var old = previous as TextBlock;
        if (old is null || old.Layout != Element.Layout)
        {
            YogaStyle.Set(Yoga, Element.Layout, reset: old is not null);
        }
        // Plain text's colour is applied as it's drawn, so a new colour alone (a theme change) keeps
        // the shaped text and its layout; styled spans carry their own colours, so any change reshapes.
        var styleChanged = Element.Content is null
            ? old is null || old.Style with { Color = Element.Style.Color } != Element.Style
            : old is null || old.Style != Element.Style;
        if (old is null || old.Text != Element.Text || !Equals(old.Content, Element.Content) || styleChanged
            || old.Alignment != Element.Alignment || old.MaxLines != Element.MaxLines || old.Wrap != Element.Wrap
            || old.Direction != Element.Direction)
        {
            _shaped = null;
            _laidOut = null;
            YGNodeMarkDirty(Yoga);
        }
    }

    /// <summary>The text laid out at a width (infinite for unwrapped).</summary>
    public Paragraph LayoutAt(float width)
    {
        if (!Element.Wrap)
        {
            width = float.PositiveInfinity;
        }
        _shaped ??= Paragraph.Layout(Element.AttributedText,
            new ParagraphStyle { Alignment = Element.Alignment, MaxLines = Element.MaxLines, Direction = Element.Direction },
            Owner.Root.Fonts);
        if (_laidOut is null || _laidOut.Style.MaxWidth != width)
        {
            _laidOut = float.IsPositiveInfinity(width) ? _shaped : _shaped.WithMaxWidth(width);
        }
        return _laidOut;
    }

    public override void Paint(PaintContext context)
    {
        var left = YGNodeLayoutGetPadding(Yoga, YGEdge.Left);
        var top = YGNodeLayoutGetPadding(Yoga, YGEdge.Top);
        var right = YGNodeLayoutGetPadding(Yoga, YGEdge.Right);
        var paragraph = LayoutAt(MathF.Max(0f, Size.X - left - right));
        context.Renderer.DrawParagraph(paragraph, context.Origin + new System.Numerics.Vector2(left, top),
            Element.Content is null ? Element.Style.Color : null);
    }

    private YGSize Measure(Node node, float width, MeasureMode widthMode, float height, MeasureMode heightMode)
    {
        var available = widthMode == MeasureMode.Undefined || float.IsNaN(width) ? float.PositiveInfinity : width;
        var paragraph = LayoutAt(available);
        // Whole pixels, rounded up: laid out again at exactly this width, the text must not wrap sooner.
        var measuredWidth = widthMode == MeasureMode.Exactly ? width : MathF.Min(MathF.Ceiling(paragraph.LongestLine), available);
        var measuredHeight = MathF.Ceiling(paragraph.Height);
        if (heightMode == MeasureMode.Exactly)
        {
            measuredHeight = height;
        }
        else if (heightMode == MeasureMode.AtMost)
        {
            measuredHeight = MathF.Min(measuredHeight, height);
        }
        return new YGSize { Width = measuredWidth, Height = measuredHeight };
    }
}
