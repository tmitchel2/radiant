namespace Radiant.Testing;

/// <summary>How <see cref="Golden.AssertMatches"/> compares an image with its golden, and where it keeps them.</summary>
public sealed record GoldenOptions
{
    /// <summary>
    /// How different two pixels' colours can be and still count as the same, from 0 (exactly) to 1
    /// (anything), on the perceptual YIQ scale; 0.1 by default, which passes anti-aliasing noise
    /// but not a changed colour.
    /// </summary>
    public double Threshold { get; init; } = 0.1;

    /// <summary>How many pixels may differ beyond the threshold before the images don't match.</summary>
    public int MaxDifferentPixels { get; init; }

    /// <summary>Where goldens are kept; by default the test project's <c>TestData/Golden</c>.</summary>
    public string? Directory { get; init; }

    /// <summary>Where a failed comparison's images go; by default the test project's <c>TestResults/Golden</c>.</summary>
    public string? ResultsDirectory { get; init; }
}
