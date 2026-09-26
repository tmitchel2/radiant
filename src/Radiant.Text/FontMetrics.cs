namespace Radiant.Text;

/// <summary>A font's vertical metrics at one size, in pixels. Distances above the baseline are positive.</summary>
/// <param name="Ascender">How far the font's ascent reaches above the baseline.</param>
/// <param name="Descender">How far the font's descent reaches below the baseline (positive).</param>
/// <param name="LineGap">Extra space the font asks for between lines.</param>
/// <param name="CapHeight">The height of flat capitals, such as H.</param>
/// <param name="XHeight">The height of flat lowercase letters, such as x.</param>
public readonly record struct FontMetrics(float Ascender, float Descender, float LineGap, float CapHeight, float XHeight)
{
    /// <summary>The font's natural line height: ascent, descent and gap.</summary>
    public float LineHeight => Ascender + Descender + LineGap;
}
