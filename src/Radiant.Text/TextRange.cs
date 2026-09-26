namespace Radiant.Text;

/// <summary>A range of text by UTF-16 index, such as a word or a selection.</summary>
/// <param name="Start">The first index.</param>
/// <param name="End">One past the last.</param>
public readonly record struct TextRange(int Start, int End)
{
    /// <summary>The length in UTF-16 code units.</summary>
    public int Length => End - Start;

    /// <summary>Whether the range holds no text.</summary>
    public bool IsEmpty => End <= Start;
}
