using System.Numerics;
using Radiant.Layout;
using Radiant.Text;

namespace Radiant.UI.Core;

/// <summary>
/// Text being edited, drawn with its selection, the input method's unfinished text underlined, and
/// the caret. Only draws: <see cref="TextInput"/> handles the editing. A single line scrolls
/// sideways to keep the caret in view.
/// </summary>
public sealed record EditableText : HostElement
{
    /// <summary>Text in an edit state.</summary>
    public EditableText(TextEditState state) => State = state;

    /// <summary>The text, selection and composition.</summary>
    public TextEditState State { get; init; }

    /// <summary>The text's style.</summary>
    public TextStyle Style { get; init; } = TextStyle.Default;

    /// <summary>Whether the text wraps onto several lines; otherwise it's one line that scrolls.</summary>
    public bool Multiline { get; init; }

    /// <summary>Whether to draw the caret (focused, and blinked on).</summary>
    public bool ShowCaret { get; init; }

    /// <summary>The caret's colour.</summary>
    public Vector4 CaretColor { get; init; } = new(0f, 0f, 0f, 1f);

    /// <summary>The selection highlight's colour.</summary>
    public Vector4 SelectionColor { get; init; } = new(0.2f, 0.4f, 1f, 0.3f);

    /// <summary>Text shown, in <see cref="PlaceholderColor"/>, while there's none.</summary>
    public string? Placeholder { get; init; }

    /// <summary>The placeholder's colour.</summary>
    public Vector4 PlaceholderColor { get; init; } = new(0f, 0f, 0f, 0.4f);

    /// <summary>Where lines sit within the width.</summary>
    public TextAlignment Alignment { get; init; }

    /// <summary>A handle for hit testing and caret bounds.</summary>
    public EditableTextRef? Ref { get; init; }

    /// <summary>Size and placement.</summary>
    public LayoutStyle Layout { get; init; }

    internal override RenderNode CreateRenderNode() => new EditableTextRenderNode();
}
