namespace Radiant.Text;

/// <summary>How a paragraph is laid out: its width, alignment, direction and line limit.</summary>
public sealed record ParagraphStyle
{
    /// <summary>Unwrapped, start-aligned, direction from the text.</summary>
    public static ParagraphStyle Default { get; } = new();

    /// <summary>The width lines wrap at, in pixels; infinity for no wrapping.</summary>
    public float MaxWidth { get; init; } = float.PositiveInfinity;

    /// <summary>Where lines sit within <see cref="MaxWidth"/> (or within the widest line when unwrapped).</summary>
    public TextAlignment Alignment { get; init; } = TextAlignment.Start;

    /// <summary>The paragraph's base direction, or null to take it from its first strong character.</summary>
    public TextDirection? Direction { get; init; }

    /// <summary>The most lines to show, or null for all. The last shown line ends with <see cref="Ellipsis"/> if text was cut.</summary>
    public int? MaxLines { get; init; }

    /// <summary>What marks cut text; null to cut without a mark.</summary>
    public string? Ellipsis { get; init; } = "…";

    /// <summary>
    /// Whether a word too long for a line on its own may break between characters (CSS
    /// <c>overflow-wrap: anywhere</c>). Otherwise it overflows the width.
    /// </summary>
    public bool BreakLongWords { get; init; } = true;

    /// <summary>How wide a tab is, in spaces of the font it's set in.</summary>
    public float TabSize { get; init; } = 4f;
}
