namespace Radiant.Text;

/// <summary>
/// An OpenType feature to turn on or off while shaping, such as tabular numbers (<c>tnum</c>) or
/// a stylistic set (<c>ss01</c>).
/// </summary>
/// <param name="Tag">The four-character feature tag.</param>
/// <param name="Value">1 to turn it on, 0 to turn it off; some features take other values (an alternate's index).</param>
public readonly record struct FontFeature(string Tag, int Value = 1)
{
    /// <summary>Digits all the same width, so columns of numbers line up.</summary>
    public static FontFeature TabularNumbers { get; } = new("tnum");

    /// <summary>Kerning; on by default, so this is useful to turn it off.</summary>
    public static FontFeature NoKerning { get; } = new("kern", 0);

    /// <summary>Standard ligatures; on by default, so this is useful to turn them off.</summary>
    public static FontFeature NoLigatures { get; } = new("liga", 0);

    /// <summary>A slashed zero, told apart from O.</summary>
    public static FontFeature SlashedZero { get; } = new("zero");
}
