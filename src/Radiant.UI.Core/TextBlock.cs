using Radiant.Layout;
using Radiant.Text;

namespace Radiant.UI.Core;

/// <summary>
/// Text, laid out and drawn by Radiant.Text: shaped, wrapped to the width it's given, in one style
/// or many (<see cref="Content"/>). It sizes itself to its text unless its layout says otherwise.
/// </summary>
public sealed record TextBlock : HostElement
{
    /// <summary>Text in one style.</summary>
    public TextBlock(string text) => Text = text;

    /// <summary>Text in several styles.</summary>
    public TextBlock(AttributedText content) => Content = content;

    /// <summary>The text, when in one style (<see cref="Style"/>).</summary>
    public string? Text { get; init; }

    /// <summary>Styled text, used instead of <see cref="Text"/> when set.</summary>
    public AttributedText? Content { get; init; }

    /// <summary>The style of <see cref="Text"/>.</summary>
    public TextStyle Style { get; init; } = TextStyle.Default;

    /// <summary>Where lines sit within the block's width.</summary>
    public TextAlignment Alignment { get; init; }

    /// <summary>The most lines to show, the last ending in an ellipsis if text is cut; null for all.</summary>
    public int? MaxLines { get; init; }

    /// <summary>Whether lines wrap at the block's width; false keeps each line whole.</summary>
    public bool Wrap { get; init; } = true;

    /// <summary>Whether the text is decoration (an icon's name) that assistive technology should skip.</summary>
    public bool IsDecorative { get; init; }

    /// <summary>
    /// The paragraph's base direction, which also decides which side <c>Start</c> alignment is; from
    /// the text itself (its first strong character) when null.
    /// </summary>
    public TextDirection? Direction { get; init; }

    /// <summary>The text's level as a heading (1 is the top), or 0 when it isn't one.</summary>
    public int HeadingLevel { get; init; }

    /// <summary>Size and placement within the parent.</summary>
    public LayoutStyle Layout { get; init; }

    internal AttributedText AttributedText => Content ?? AttributedText.Plain(Text ?? "", Style);

    internal override RenderNode CreateRenderNode() => new TextRenderNode();
}
