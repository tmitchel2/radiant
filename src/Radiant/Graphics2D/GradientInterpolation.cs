namespace Radiant.Graphics2D;

/// <summary>
/// The color space a gradient blends its stops in. Each gives a different middle: black to white is
/// mid-grey (sRGB 128) in <see cref="Srgb"/>, a lighter 188 in <see cref="Linear"/>, and a darker 99
/// in <see cref="Oklab"/>. Alpha is always interpolated premultiplied, so fading to transparent
/// never passes through a darker color.
/// </summary>
public enum GradientInterpolation
{
    /// <summary>
    /// Gamma-encoded sRGB: what CSS gradients and design tools do by default, so a design matches
    /// its mock-up.
    /// </summary>
    Srgb = 0,

    /// <summary>Linear light: physically mixed light, with bright midpoints.</summary>
    Linear = 1,

    /// <summary>OKLab: perceptually even steps, and no grey dead zone between complementary hues.</summary>
    Oklab = 2,
}
