using System;
using System.Numerics;
using Facebook.Yoga;
using Radiant.Text;
using static Facebook.Yoga.YGNodeAPI;

namespace Radiant.UI.Core;

/// <summary>Lays out and draws an <see cref="EditableText"/>.</summary>
internal sealed class EditableTextRenderNode : RenderNode
{
    private Paragraph? _shaped;
    private Paragraph? _placeholder;
    private float _scroll;

    public EditableTextRenderNode() => YGNodeSetMeasureFunc(Yoga, Measure);

    public EditableText Element { get; private set; } = null!;

    /// <summary>The text laid out at the node's width.</summary>
    public Paragraph? Paragraph { get; private set; }

    /// <summary>Where the paragraph's top left is, in root coordinates (scrolling included).</summary>
    public Vector2 TextOrigin => AbsolutePosition - new Vector2(_scroll, 0);

    public override bool ClipsChildren => true;

    public override void Update(HostElement element, HostElement? previous)
    {
        var old = previous as EditableText;
        Element = (EditableText)element;
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
        if (old is null || old.State.Text != Element.State.Text || old.Style != Element.Style
            || old.Multiline != Element.Multiline || old.Alignment != Element.Alignment || old.Placeholder != Element.Placeholder)
        {
            _shaped = null;
            _placeholder = null;
            Paragraph = null;
            YGNodeMarkDirty(Yoga);
        }
    }

    private Paragraph LayoutAt(float width)
    {
        if (!Element.Multiline)
        {
            width = float.PositiveInfinity;
        }
        var direction = LayoutDirection.Of(Yoga);
        if (_shaped is not null && _shaped.Style.Direction != direction)
        {
            _shaped = null;
            _placeholder = null;
            Paragraph = null;
        }
        _shaped ??= Radiant.Text.Paragraph.Layout(Element.State.Text, Element.Style,
            new ParagraphStyle { Alignment = Element.Alignment, Direction = direction }, Owner.Root.Fonts);
        if (Paragraph is null || Paragraph.Style.MaxWidth != width)
        {
            Paragraph = float.IsPositiveInfinity(width) ? _shaped : _shaped.WithMaxWidth(width);
        }
        return Paragraph;
    }

    private Paragraph? PlaceholderAt(float width)
    {
        if (Element.State.Text.Length > 0 || Element.Placeholder is not { Length: > 0 } text)
        {
            return null;
        }
        _placeholder ??= Radiant.Text.Paragraph.Layout(text, Element.Style with { Color = Element.PlaceholderColor },
            new ParagraphStyle { Alignment = Element.Alignment, MaxLines = Element.Multiline ? null : 1, Direction = LayoutDirection.Of(Yoga) }, Owner.Root.Fonts);
        return Element.Multiline && float.IsFinite(width) ? _placeholder.WithMaxWidth(width) : _placeholder;
    }

    public override void Paint(PaintContext context)
    {
        var renderer = context.Renderer;
        var size = Size;
        var paragraph = LayoutAt(size.X);
        var state = Element.State;

        // Keep the caret in view on a single line, by scrolling as little as possible.
        var caret = paragraph.GetCaretRect(state.Selection.FocusPosition);
        if (!Element.Multiline)
        {
            // Reading right to left, a line shorter than the field sits against its right edge
            // (a negative scroll), and a longer one starts scrolled to its end.
            var overflow = paragraph.Width + 2f - size.X;
            var (minScroll, maxScroll) = LayoutDirection.Of(Yoga) is null ? (0f, MathF.Max(0f, overflow))
                : overflow <= 0f ? (overflow, overflow) : (0f, overflow);
            if (caret.Left - _scroll > size.X - 2f)
            {
                _scroll = caret.Left - size.X + 2f;
            }
            if (caret.Left - _scroll < 0f)
            {
                _scroll = caret.Left;
            }
            _scroll = Math.Clamp(_scroll, minScroll, maxScroll);
        }
        var origin = context.Origin - new Vector2(_scroll, 0f);

        renderer.PushClip(context.Origin.X, context.Origin.Y, size.X, size.Y);
        if (!state.Selection.IsCollapsed)
        {
            foreach (var box in paragraph.GetSelectionRects(state.Selection.Start, state.Selection.End))
            {
                renderer.DrawRectangleFilled(origin.X + box.Left, origin.Y + box.Top, box.Width, box.Height, Element.SelectionColor);
            }
        }
        if (PlaceholderAt(size.X) is { } placeholder)
        {
            // A single-line placeholder, unwrapped, is placed against the start edge by hand.
            var at = !Element.Multiline && LayoutDirection.Of(Yoga) is not null
                ? context.Origin + new Vector2(size.X - 2f - placeholder.Width, 0f)
                : origin;
            renderer.DrawParagraph(placeholder, at);
        }
        renderer.DrawParagraph(paragraph, origin);
        if (state.Composing is { } composing)
        {
            foreach (var box in paragraph.GetSelectionRects(composing.Start, composing.End))
            {
                renderer.DrawRectangleFilled(origin.X + box.Left, origin.Y + box.Bottom - 2f, box.Width, 1f, Element.CaretColor);
            }
        }
        if (Element.ShowCaret)
        {
            renderer.DrawRectangleFilled(origin.X + caret.Left - 1f, origin.Y + caret.Top, 2f, caret.Height, Element.CaretColor);
        }
        renderer.PopClip();
    }

    private YGSize Measure(Node node, float width, MeasureMode widthMode, float height, MeasureMode heightMode)
    {
        var available = widthMode == MeasureMode.Undefined || float.IsNaN(width) ? float.PositiveInfinity : width;
        var paragraph = LayoutAt(available);
        var placeholder = PlaceholderAt(available);
        var contentWidth = MathF.Max(paragraph.LongestLine, placeholder?.LongestLine ?? 0f) + 2f;
        var measuredWidth = widthMode == MeasureMode.Exactly ? width : MathF.Min(MathF.Ceiling(contentWidth), available);
        var measuredHeight = MathF.Ceiling(MathF.Max(paragraph.Height, placeholder?.Height ?? 0f));
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

    public override void Dispose()
    {
        if (Element?.Ref is { } elementRef && ReferenceEquals(elementRef.Node, this))
        {
            elementRef.Node = null;
        }
        base.Dispose();
    }
}
