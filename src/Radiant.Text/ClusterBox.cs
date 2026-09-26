namespace Radiant.Text;

/// <summary>
/// Where a grapheme sits on its line: what carets, hit testing and selection are computed from. A
/// ligature standing for several graphemes is shared out between them evenly.
/// </summary>
/// <param name="Start">The grapheme's first UTF-16 index.</param>
/// <param name="End">One past its last.</param>
/// <param name="Left">Its left edge, in paragraph pixels.</param>
/// <param name="Right">Its right edge.</param>
/// <param name="IsRightToLeft">Whether it reads right to left, so its logical start is its right edge.</param>
/// <param name="Kind">What it stands for.</param>
internal readonly record struct ClusterBox(int Start, int End, float Left, float Right, bool IsRightToLeft, ClusterBoxKind Kind)
{
    /// <summary>Where a caret before the grapheme goes: its left edge, or its right edge in right-to-left text.</summary>
    public float LeadingEdge => IsRightToLeft ? Right : Left;

    /// <summary>Where a caret after the grapheme goes.</summary>
    public float TrailingEdge => IsRightToLeft ? Left : Right;

    /// <summary>The same box moved right.</summary>
    public ClusterBox Offset(float dx) => this with { Left = Left + dx, Right = Right + dx };
}
