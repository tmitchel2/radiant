namespace Radiant.Text;

/// <summary>What a <see cref="ClusterBox"/> stands for.</summary>
internal enum ClusterBoxKind
{
    /// <summary>A grapheme of the paragraph's text.</summary>
    Text,

    /// <summary>A line break character: no width, not hit by the pointer.</summary>
    LineBreak,

    /// <summary>The ellipsis marking cut text.</summary>
    Ellipsis,
}
