using System;

namespace Radiant.Platform;

/// <summary>
/// How the user has asked apps to look: dark or light, their accent colour, and the display
/// accessibility settings an app should honour. Themes follow these (see Radiant.Theming's
/// <c>FollowAppearance</c>), and <see cref="Changed"/> says when the user changes one.
/// </summary>
public interface IAppearance
{
    /// <summary>Whether the app should look dark.</summary>
    bool IsDark { get; }

    /// <summary>
    /// The user's accent colour, as straight (unpremultiplied) sRGB in 0xAARRGGBB: the encoded
    /// colour, as a colour picker shows it, not linear light.
    /// </summary>
    uint AccentColor { get; }

    /// <summary>Whether the user asked for more contrast between colours.</summary>
    bool IncreaseContrast { get; }

    /// <summary>Whether the user asked for less motion: skip or shorten animations.</summary>
    bool ReduceMotion { get; }

    /// <summary>One or more of the settings changed. Raised on the UI thread.</summary>
    event Action? Changed;
}
