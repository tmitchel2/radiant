namespace Radiant.Text;

/// <summary>A range of <see cref="AttributedText"/> in one style.</summary>
/// <param name="Start">The first UTF-16 index.</param>
/// <param name="Length">The length in UTF-16 code units.</param>
/// <param name="Style">The style of the range.</param>
public readonly record struct TextSpan(int Start, int Length, TextStyle Style)
{
    /// <summary>One past the last UTF-16 index.</summary>
    public int End => Start + Length;
}
