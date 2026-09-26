using System.Numerics;
using Radiant.Text;

namespace Radiant.UI.Core;

/// <summary>
/// A handle on a laid-out <see cref="EditableText"/>, for the component editing it: which text
/// position a pointer is over, and where the caret is on screen (for an input method's candidates).
/// </summary>
public sealed class EditableTextRef
{
    internal EditableTextRenderNode? Node { get; set; }

    /// <summary>The text as last laid out, or null before layout.</summary>
    public Paragraph? Paragraph => Node?.Paragraph;

    /// <summary>The text position under a point in root coordinates.</summary>
    public TextPosition HitTest(Vector2 rootPoint) =>
        Node is { Paragraph: { } paragraph } node ? paragraph.HitTest(rootPoint - node.TextOrigin) : default;

    /// <summary>The caret for a position, in root coordinates.</summary>
    public TextBox CaretRect(TextPosition position)
    {
        if (Node is not { Paragraph: { } paragraph } node)
        {
            return default;
        }
        var caret = paragraph.GetCaretRect(position);
        var origin = node.TextOrigin;
        return caret with { Left = caret.Left + origin.X, Right = caret.Right + origin.X, Top = caret.Top + origin.Y, Bottom = caret.Bottom + origin.Y };
    }
}
