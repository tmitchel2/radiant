namespace Radiant.Text;

/// <summary>
/// Which side of a text index a caret belongs to where one index has two places on screen: the
/// end of a wrapped line and the start of the next, or either side of a change of direction.
/// </summary>
public enum TextAffinity
{
    /// <summary>With the character after the index: at a wrap, the start of the next line.</summary>
    Downstream,

    /// <summary>With the character before the index: at a wrap, the end of the line.</summary>
    Upstream,
}
