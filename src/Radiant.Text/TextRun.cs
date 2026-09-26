using HarfBuzzSharp;

namespace Radiant.Text;

/// <summary>
/// A stretch of a paragraph that shapes as one piece: one style, one font face, one script, one
/// bidi level, inside one paragraph between line breaks.
/// </summary>
internal sealed class TextRun
{
    public TextRun(int start, int end, TextStyle style, FontFace face, byte level, int section, Script script)
    {
        Start = start;
        End = end;
        Style = style;
        Face = face;
        Level = level;
        Section = section;
        Script = script;
    }

    public int Start { get; }

    public int End { get; set; }

    public TextStyle Style { get; }

    public FontFace Face { get; }

    public byte Level { get; }

    /// <summary>The index of the <see cref="BidiSection"/> the run is in.</summary>
    public int Section { get; }

    /// <summary>The run's script; Common until a character with a real script joins it.</summary>
    public Script Script { get; set; }

    /// <summary>The run shaped whole; lines that split it reshape their part.</summary>
    public ShapedRun? Shaped { get; set; }
}
