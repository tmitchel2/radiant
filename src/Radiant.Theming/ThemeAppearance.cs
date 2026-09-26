using System;
using Radiant.Graphics2D;
using Radiant.Platform;

namespace Radiant.Theming;

/// <summary>
/// Makes a theme follow the user's system appearance: dark mode, accent colour, increased
/// contrast and reduced motion (<see cref="IAppearance"/>).
/// </summary>
public static class ThemeAppearance
{
    /// <summary>
    /// <paramref name="theme"/> adjusted to <paramref name="appearance"/>: dark mode to
    /// <see cref="ThemeColors.IsDark"/>, the accent to the seed, increased contrast to contrast
    /// level 1 (otherwise <paramref name="standardContrast"/>), reduced motion to
    /// <see cref="MotionScheme.Reduced"/>, each only if <paramref name="following"/> says so.
    /// </summary>
    public static Theme WithAppearance(this Theme theme, IAppearance appearance, AppearanceFollowing? following = null, double standardContrast = 0)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(appearance);
        following ??= new AppearanceFollowing();
        var colors = theme.Colors;
        if (following.DarkMode)
        {
            colors = colors with { IsDark = appearance.IsDark };
        }
        if (following.Accent)
        {
            colors = colors with { Seed = Color.FromArgb(appearance.AccentColor) };
        }
        if (following.Contrast)
        {
            colors = colors with { ContrastLevel = appearance.IncreaseContrast ? 1 : standardContrast };
        }
        var motion = following.Motion ? theme.Motion with { Reduced = appearance.ReduceMotion } : theme.Motion;
        return theme with { Colors = colors, Motion = motion };
    }

    /// <summary>
    /// Keeps <paramref name="controller"/>'s theme following <paramref name="appearance"/>:
    /// adjusts it now, at once, and again whenever the user changes a setting, animating over
    /// <see cref="AppearanceFollowing.Transition"/> (unless they reduced motion). Contrast returns
    /// to the theme's level at the time of this call when "increase contrast" is turned off.
    /// Dispose the result to stop following.
    /// </summary>
    public static IDisposable FollowAppearance(this ThemeController controller, IAppearance appearance, AppearanceFollowing? following = null)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(appearance);
        following ??= new AppearanceFollowing();
        var standardContrast = controller.Theme.Colors.ContrastLevel;
        Apply(TimeSpan.Zero);
        Action changed = () => Apply(following.Transition ?? controller.Theme.Motion.MediumDuration);
        appearance.Changed += changed;
        return new Unsubscriber(() => appearance.Changed -= changed);

        void Apply(TimeSpan transition)
        {
            var next = controller.Theme.WithAppearance(appearance, following, standardContrast);
            if (next != controller.Theme)
            {
                controller.Set(next, transition);
            }
        }
    }

    private sealed class Unsubscriber(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;

        public void Dispose()
        {
            _unsubscribe?.Invoke();
            _unsubscribe = null;
        }
    }
}
