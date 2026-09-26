using System.Collections.Generic;

namespace Radiant.Text;

/// <summary>How a run of text is shaped.</summary>
public sealed record ShapeOptions
{
    /// <summary>Plain left-to-right shaping with the font's default features.</summary>
    public static ShapeOptions Default { get; } = new();

    /// <summary>The run's direction; a run is one direction (bidi resolution splits text into runs).</summary>
    public TextDirection Direction { get; init; } = TextDirection.LeftToRight;

    /// <summary>The ISO 15924 script, such as <c>Latn</c> or <c>Arab</c>; null to infer it from the text.</summary>
    public string? Script { get; init; }

    /// <summary>A BCP 47 language, such as <c>tr</c>, for language-specific forms; null for none.</summary>
    public string? Language { get; init; }

    /// <summary>OpenType features to turn on or off, on top of the font's defaults.</summary>
    public IReadOnlyList<FontFeature> Features { get; init; } = [];

    /// <summary>Extra space after every glyph, in ems (tracking, letter-spacing): 0.01 is 1%.</summary>
    public float Tracking { get; init; }

    /// <summary>How wide a tab is, in spaces of the run's font.</summary>
    public float TabSize { get; init; } = 4f;
}
