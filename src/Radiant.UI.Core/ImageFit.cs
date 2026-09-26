namespace Radiant.UI.Core;

/// <summary>How an <see cref="Image"/>'s picture fills its box.</summary>
public enum ImageFit
{
    /// <summary>Covers the box, keeping its proportions, cropping what overflows.</summary>
    Cover,

    /// <summary>Fits inside the box, keeping its proportions, leaving bands where they differ.</summary>
    Contain,

    /// <summary>Stretches to the box.</summary>
    Fill,
}
