namespace Radiant.Text;

/// <summary>A setting of one variation axis, such as <c>wght</c> = 600.</summary>
/// <param name="Tag">The four-character axis tag.</param>
/// <param name="Value">The axis value, in the axis's own units (e.g. 100–900 for weight).</param>
public readonly record struct FontVariation(string Tag, float Value)
{
    /// <summary>The weight axis: 400 is regular, 700 bold.</summary>
    public const string Weight = "wght";

    /// <summary>The optical size axis: the text size, in points, the design is tuned for.</summary>
    public const string OpticalSize = "opsz";

    /// <summary>The slant axis, in degrees.</summary>
    public const string Slant = "slnt";

    /// <summary>The width axis, as a percentage of normal.</summary>
    public const string Width = "wdth";

    /// <summary>Fill: 0 outlined to 1 filled, in icon fonts such as Material Symbols.</summary>
    public const string Fill = "FILL";
}
