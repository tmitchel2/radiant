namespace Radiant.Text;

/// <summary>A variation axis of a variable font, such as weight (<c>wght</c>) or optical size (<c>opsz</c>).</summary>
/// <param name="Tag">The four-character axis tag.</param>
/// <param name="Min">The smallest value the font supports.</param>
/// <param name="Default">The value the font uses when none is given.</param>
/// <param name="Max">The largest value the font supports.</param>
public readonly record struct FontAxis(string Tag, float Min, float Default, float Max)
{
    /// <summary>A value clamped to what the font supports.</summary>
    public float Clamp(float value) => value < Min ? Min : value > Max ? Max : value;
}
