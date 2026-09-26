using System;
using System.Collections.Generic;
using Radiant.ColorSystem;
using Radiant.Graphics2D;
using Radiant.Text;
using Radiant.UI.Core;

namespace Radiant.Theming;

/// <summary>
/// A <see cref="Theme"/> worked out: every colour role computed once from the scheme, so looking
/// a colour up while building or drawing is an array index.
/// </summary>
public sealed class ResolvedTheme
{
    private static readonly Lazy<ResolvedTheme> s_default = new(() => Resolve(new Theme()));
    private static readonly int s_names = Enum.GetValues<SurfaceName>().Length;

    // Four colours per surface family: colour, on, container, on container.
    private readonly Color[] _roles;

    private ResolvedTheme(Theme theme, Color[] roles, Color outline, Color outlineVariant, Color scrim, Color shadow, Color background)
    {
        Theme = theme;
        _roles = roles;
        Outline = outline;
        OutlineVariant = outlineVariant;
        Scrim = scrim;
        Shadow = shadow;
        Background = background;
    }

    /// <summary>The default theme, resolved.</summary>
    public static ResolvedTheme Default => s_default.Value;

    /// <summary>The theme this was resolved from.</summary>
    public Theme Theme { get; }

    /// <summary>Borders that need to be seen: text fields, outlined buttons.</summary>
    public Color Outline { get; }

    /// <summary>Quieter borders: dividers, cards.</summary>
    public Color OutlineVariant { get; }

    /// <summary>What dims the app behind a modal.</summary>
    public Color Scrim { get; }

    /// <summary>The colour of elevation shadows.</summary>
    public Color Shadow { get; }

    /// <summary>The window's background.</summary>
    public Color Background { get; }

    /// <summary>Works out a theme.</summary>
    public static ResolvedTheme Resolve(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var colors = theme.Colors;
        var seed = (int)colors.Seed.ToArgb();
        var scheme = Scheme(seed, colors);
        var roles = new Color[s_names * 4];

        void Set(SurfaceName name, DynamicScheme source, DynamicColor color, DynamicColor on, DynamicColor container, DynamicColor onContainer)
        {
            var i = (int)name * 4;
            roles[i] = Argb(source, color);
            roles[i + 1] = Argb(source, on);
            roles[i + 2] = Argb(source, container);
            roles[i + 3] = Argb(source, onContainer);
        }

        Set(SurfaceName.Surface, scheme, RadiantDynamicColors.Surface(), RadiantDynamicColors.OnSurface(), RadiantDynamicColors.SurfaceContainer(), RadiantDynamicColors.OnSurface());
        Set(SurfaceName.SurfaceDim, scheme, RadiantDynamicColors.SurfaceDim(), RadiantDynamicColors.OnSurface(), RadiantDynamicColors.SurfaceContainer(), RadiantDynamicColors.OnSurface());
        Set(SurfaceName.SurfaceBright, scheme, RadiantDynamicColors.SurfaceBright(), RadiantDynamicColors.OnSurface(), RadiantDynamicColors.SurfaceContainer(), RadiantDynamicColors.OnSurface());
        foreach (var (name, container) in new[]
        {
            (SurfaceName.SurfaceContainerLowest, RadiantDynamicColors.SurfaceContainerLowest()),
            (SurfaceName.SurfaceContainerLow, RadiantDynamicColors.SurfaceContainerLow()),
            (SurfaceName.SurfaceContainer, RadiantDynamicColors.SurfaceContainer()),
            (SurfaceName.SurfaceContainerHigh, RadiantDynamicColors.SurfaceContainerHigh()),
            (SurfaceName.SurfaceContainerHighest, RadiantDynamicColors.SurfaceContainerHighest()),
        })
        {
            Set(name, scheme, container, RadiantDynamicColors.OnSurface(), container, RadiantDynamicColors.OnSurfaceVariant());
        }
        Set(SurfaceName.SurfaceVariant, scheme, RadiantDynamicColors.Surface(), RadiantDynamicColors.OnSurfaceVariant(), RadiantDynamicColors.SurfaceVariant(), RadiantDynamicColors.OnSurfaceVariant());
        Set(SurfaceName.Inverse, scheme, RadiantDynamicColors.InverseSurface(), RadiantDynamicColors.InverseOnSurface(), RadiantDynamicColors.InversePrimary(), RadiantDynamicColors.InverseSurface());
        Set(SurfaceName.Primary, scheme, RadiantDynamicColors.Primary(), RadiantDynamicColors.OnPrimary(), RadiantDynamicColors.PrimaryContainer(), RadiantDynamicColors.OnPrimaryContainer());
        Set(SurfaceName.PrimaryFixed, scheme, RadiantDynamicColors.PrimaryFixed(), RadiantDynamicColors.OnPrimaryFixed(), RadiantDynamicColors.PrimaryFixedDim(), RadiantDynamicColors.OnPrimaryFixedVariant());
        Set(SurfaceName.Secondary, scheme, RadiantDynamicColors.Secondary(), RadiantDynamicColors.OnSecondary(), RadiantDynamicColors.SecondaryContainer(), RadiantDynamicColors.OnSecondaryContainer());
        Set(SurfaceName.SecondaryFixed, scheme, RadiantDynamicColors.SecondaryFixed(), RadiantDynamicColors.OnSecondaryFixed(), RadiantDynamicColors.SecondaryFixedDim(), RadiantDynamicColors.OnSecondaryFixedVariant());
        Set(SurfaceName.Tertiary, scheme, RadiantDynamicColors.Tertiary(), RadiantDynamicColors.OnTertiary(), RadiantDynamicColors.TertiaryContainer(), RadiantDynamicColors.OnTertiaryContainer());
        Set(SurfaceName.TertiaryFixed, scheme, RadiantDynamicColors.TertiaryFixed(), RadiantDynamicColors.OnTertiaryFixed(), RadiantDynamicColors.TertiaryFixedDim(), RadiantDynamicColors.OnTertiaryFixedVariant());
        Set(SurfaceName.Error, scheme, RadiantDynamicColors.Error(), RadiantDynamicColors.OnError(), RadiantDynamicColors.ErrorContainer(), RadiantDynamicColors.OnErrorContainer());

        // Custom families are the primary roles of a scheme seeded with the (harmonised) custom
        // colour: they follow the variant, dark mode, contrast level and spec version like the rest.
        foreach (var (name, custom) in new[] { (SurfaceName.Success, colors.Success), (SurfaceName.Warning, colors.Warning), (SurfaceName.Info, colors.Info) })
        {
            var argb = (int)custom.ToArgb();
            if (colors.HarmonizeCustomColors)
            {
                argb = Blend.Harmonize(argb, seed);
            }
            var customScheme = Scheme(argb, colors);
            Set(name, customScheme, RadiantDynamicColors.Primary(), RadiantDynamicColors.OnPrimary(), RadiantDynamicColors.PrimaryContainer(), RadiantDynamicColors.OnPrimaryContainer());
        }

        return new ResolvedTheme(theme, roles,
            Argb(scheme, RadiantDynamicColors.Outline()),
            Argb(scheme, RadiantDynamicColors.OutlineVariant()),
            Argb(scheme, RadiantDynamicColors.Scrim()),
            Argb(scheme, RadiantDynamicColors.Shadow()),
            Argb(scheme, RadiantDynamicColors.Background()));
    }

    /// <summary>A family's colour, "on" colour, container or content on the container.</summary>
    public Color Get(SurfaceName name, bool on = false, bool container = false) =>
        _roles[(int)name * 4 + (container ? 2 : 0) + (on ? 1 : 0)];

    /// <summary>The colour a role state names, with its opacity applied.</summary>
    public Color Get(SurfaceRoleState role)
    {
        var color = Get(role.Name, role.On, role.Container);
        return role.Opacity is { } opacity ? color with { A = color.A * opacity } : color;
    }

    /// <summary>A shape role's corner radius.</summary>
    public float Radius(CornerShapeRole role) => Theme.Shape.Radius(role);

    /// <summary>A shape role's corners, all four the same.</summary>
    public CornerRadii Corners(CornerShapeRole role) => CornerRadii.All(Radius(role));

    /// <summary>A text type's style (in its colour-free form: the surface gives the colour).</summary>
    public TextStyle Text(TextType type) => Theme.Typography[type];

    /// <summary>An elevation level's shadows.</summary>
    public IReadOnlyList<BoxShadow> Elevation(ElevationLevel level) => Theme.Elevation.Shadows(level, Shadow);

    /// <summary><paramref name="steps"/> spacing units, in pixels.</summary>
    public float Space(float steps) => steps * Theme.SpacingUnit;

    /// <summary>How much to add to (or take from) a control's height for the theme's density.</summary>
    public float DensityOffset => Theme.Density * 4f;

    /// <summary>
    /// A theme part way from <paramref name="from"/> to <paramref name="to"/>, for transitions:
    /// colours mixed in OKLab, corner radii and shadows in between. Type, motion and density take
    /// <paramref name="to"/>'s at once (text changing size every frame would reflow every frame).
    /// </summary>
    public static ResolvedTheme Lerp(ResolvedTheme from, ResolvedTheme to, float t)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (t <= 0f)
        {
            return from;
        }
        if (t >= 1f)
        {
            return to;
        }
        var roles = new Color[to._roles.Length];
        for (var i = 0; i < roles.Length; i++)
        {
            roles[i] = Oklab.Lerp(from._roles[i], to._roles[i], t);
        }
        var shape = from.Theme.Shape;
        var target = to.Theme.Shape;
        var theme = to.Theme with
        {
            Shape = new ShapeScale
            {
                ExtraSmall = Mix(shape.ExtraSmall, target.ExtraSmall),
                Small = Mix(shape.Small, target.Small),
                Medium = Mix(shape.Medium, target.Medium),
                Large = Mix(shape.Large, target.Large),
                LargeIncreased = Mix(shape.LargeIncreased, target.LargeIncreased),
                SemiLarge = Mix(shape.SemiLarge, target.SemiLarge),
                ExtraLarge = Mix(shape.ExtraLarge, target.ExtraLarge),
                ExtraLargeIncreased = Mix(shape.ExtraLargeIncreased, target.ExtraLargeIncreased),
                ExtraExtraLarge = Mix(shape.ExtraExtraLarge, target.ExtraExtraLarge),
            },
        };
        return new ResolvedTheme(theme, roles,
            Oklab.Lerp(from.Outline, to.Outline, t),
            Oklab.Lerp(from.OutlineVariant, to.OutlineVariant, t),
            Oklab.Lerp(from.Scrim, to.Scrim, t),
            Oklab.Lerp(from.Shadow, to.Shadow, t),
            Oklab.Lerp(from.Background, to.Background, t));

        float Mix(float a, float b) => a + (b - a) * t;
    }

    private static DynamicScheme Scheme(int argb, ThemeColors colors) =>
        new(Hct.FromInt(argb), colors.Variant, colors.IsDark, colors.ContrastLevel, Platform.Phone, colors.SpecVersion);

    private static Color Argb(DynamicScheme scheme, DynamicColor color) => Color.FromArgb(scheme.GetArgb(color));
}
