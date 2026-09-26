using System;
using System.Collections.Generic;
using Radiant.Animation;
using Radiant.Graphics2D;
using Radiant.Text;

namespace Radiant.Theming;

/// <summary>
/// Ready-made themes, each a different look for the same components. Switch between them with
/// <see cref="Theme.WithStyle"/> to keep the user's light or dark, contrast and motion settings.
/// </summary>
public static class ThemePresets
{
    /// <summary>
    /// Colour worked out from one seed in tonal palettes, pill-shaped controls, generous spacing,
    /// soft layered shadows and state layers. Radiant's default.
    /// </summary>
    public static Theme Tonal { get; } = new() { Name = "Tonal" };

    /// <summary>
    /// Crisp and neutral: cool greys with an indigo accent, flat white components edged with
    /// hairline borders, gently rounded controls, semibold headings and small tight shadows.
    /// </summary>
    public static Theme Quartz { get; } = new()
    {
        Name = "Quartz",
        Colors = new ThemeColors { Light = QuartzLight(), Dark = QuartzDark() },
        Shape = new ShapeScale
        {
            ExtraSmall = 6, Small = 6, Medium = 8, Large = 12, LargeIncreased = 12, SemiLarge = 16,
            ExtraLarge = 12, ExtraLargeIncreased = 16, ExtraExtraLarge = 24, Control = 6,
        },
        Typography = new TypeScale(new Dictionary<TextType, TextStyle>
        {
            [TextType.DisplayLarge] = Sans(60, 64, FontWeight.Bold, -0.025f),
            [TextType.DisplayMedium] = Sans(48, 52, FontWeight.Bold, -0.025f),
            [TextType.DisplaySmall] = Sans(36, 40, FontWeight.Bold, -0.025f),
            [TextType.HeadlineLarge] = Sans(30, 36, FontWeight.SemiBold, -0.02f),
            [TextType.HeadlineMedium] = Sans(24, 32, FontWeight.SemiBold, -0.015f),
            [TextType.HeadlineSmall] = Sans(20, 28, FontWeight.SemiBold, -0.01f),
            [TextType.HeadlineExtraSmall] = Sans(18, 28, FontWeight.SemiBold, 0f),
            [TextType.TitleLarge] = Sans(18, 28, FontWeight.SemiBold, -0.01f),
            [TextType.TitleSemiLarge] = Sans(16, 24, FontWeight.SemiBold, 0f),
            [TextType.TitleMedium] = Sans(15, 24, FontWeight.SemiBold, 0f),
            [TextType.TitleSmall] = Sans(14, 20, FontWeight.SemiBold, 0f),
            [TextType.LabelLarge] = Sans(14, 20, FontWeight.SemiBold, 0f),
            [TextType.LabelMedium] = Sans(12, 16, FontWeight.Medium, 0f),
            [TextType.LabelSmall] = Sans(11, 16, FontWeight.Medium, 0f),
            [TextType.BodyExtraLarge] = Sans(18, 28, FontWeight.Regular, 0f),
            [TextType.BodyLarge] = Sans(16, 24, FontWeight.Regular, 0f),
            [TextType.BodyMedium] = Sans(14, 20, FontWeight.Regular, 0f),
            [TextType.BodySmall] = Sans(12, 16, FontWeight.Regular, 0f),
            [TextType.BodyExtraSmall] = Sans(11, 16, FontWeight.Regular, 0f),
        }),
        Elevation = new ElevationScale
        {
            Levels =
            [
                [],
                [new(1, 3, 0, 0.10f), new(1, 2, -1, 0.10f)],
                [new(10, 15, -3, 0.10f), new(4, 6, -4, 0.10f)],
                [new(20, 25, -5, 0.10f), new(8, 10, -6, 0.10f)],
                [new(25, 50, -12, 0.25f)],
                [new(32, 64, -12, 0.30f)],
            ],
        },
        StateLayers = new StateLayerOpacities { Hover = 0.06f, Focus = 0.06f, Pressed = 0.10f, Dragged = 0.12f },
        Motion = new MotionScheme
        {
            ShortDuration = TimeSpan.FromMilliseconds(150),
            MediumDuration = TimeSpan.FromMilliseconds(200),
            LongDuration = TimeSpan.FromMilliseconds(300),
            Standard = new Easing(0.4f, 0f, 0.2f, 1f),
            Enter = new Easing(0f, 0f, 0.2f, 1f),
            Exit = new Easing(0.4f, 0f, 1f, 1f),
        },
        Density = -1,
        // Squarer chips and tags than the pill-shaped default.
        Components = ComponentStyles.Hairline with
        {
            Chip = ComponentStyles.Hairline.Chip with { Shape = CornerShapeRole.Small, TagShape = CornerShapeRole.Small },
        },
    };

    /// <summary>
    /// Warm and calm: ivory paper and warm charcoal, a terracotta accent, bold sans headings with
    /// a serif for display text and long reading, flat hairline-edged components, softly rounded
    /// controls and faint, diffuse shadows.
    /// </summary>
    public static Theme Linen { get; } = new()
    {
        Name = "Linen",
        Colors = new ThemeColors { Light = LinenLight(), Dark = LinenDark() },
        Shape = new ShapeScale
        {
            ExtraSmall = 8, Small = 8, Medium = 12, Large = 16, LargeIncreased = 18, SemiLarge = 20,
            ExtraLarge = 20, ExtraLargeIncreased = 24, ExtraExtraLarge = 32, Control = 8,
        },
        Typography = new TypeScale(new Dictionary<TextType, TextStyle>
        {
            [TextType.DisplayLarge] = Serif(56, 64, FontWeight.Regular, -0.02f),
            [TextType.DisplayMedium] = Serif(44, 52, FontWeight.Regular, -0.02f),
            [TextType.DisplaySmall] = Serif(36, 44, FontWeight.Regular, -0.015f),
            [TextType.HeadlineLarge] = Sans(30, 36, FontWeight.Bold, -0.02f),
            [TextType.HeadlineMedium] = Sans(26, 32, FontWeight.Bold, -0.015f),
            [TextType.HeadlineSmall] = Sans(22, 28, FontWeight.Bold, -0.01f),
            [TextType.HeadlineExtraSmall] = Sans(20, 28, FontWeight.SemiBold, -0.01f),
            [TextType.TitleLarge] = Sans(18, 26, FontWeight.SemiBold, -0.01f),
            [TextType.TitleSemiLarge] = Sans(17, 24, FontWeight.SemiBold, 0f),
            [TextType.TitleMedium] = Sans(16, 24, FontWeight.SemiBold, 0f),
            [TextType.TitleSmall] = Sans(14, 20, FontWeight.SemiBold, 0f),
            [TextType.LabelLarge] = Sans(14, 20, FontWeight.Medium, 0f),
            [TextType.LabelMedium] = Sans(12, 16, FontWeight.Medium, 0f),
            [TextType.LabelSmall] = Sans(11, 16, FontWeight.Medium, 0.01f),
            [TextType.BodyExtraLarge] = Serif(18, 28, FontWeight.Regular, 0f),
            [TextType.BodyLarge] = Sans(16, 24, FontWeight.Regular, 0f),
            [TextType.BodyMedium] = Sans(14, 21, FontWeight.Regular, 0f),
            [TextType.BodySmall] = Sans(12, 17, FontWeight.Regular, 0f),
            [TextType.BodyExtraSmall] = Sans(11, 16, FontWeight.Regular, 0f),
        }),
        Elevation = new ElevationScale
        {
            Levels =
            [
                [],
                [new(1, 2, 0, 0.04f), new(2, 8, 0, 0.04f)],
                [new(4, 20, 0, 0.07f), new(1, 3, 0, 0.06f)],
                [new(12, 32, -4, 0.12f), new(2, 6, 0, 0.05f)],
                [new(20, 48, -8, 0.16f), new(4, 10, 0, 0.05f)],
                [new(28, 64, -12, 0.20f), new(6, 14, 0, 0.05f)],
            ],
        },
        StateLayers = new StateLayerOpacities { Hover = 0.05f, Focus = 0.06f, Pressed = 0.09f, Dragged = 0.12f },
        Motion = new MotionScheme
        {
            ShortDuration = TimeSpan.FromMilliseconds(150),
            MediumDuration = TimeSpan.FromMilliseconds(250),
            LongDuration = TimeSpan.FromMilliseconds(400),
            Standard = new Easing(0.25f, 0.1f, 0.25f, 1f),
            Enter = new Easing(0f, 0f, 0.2f, 1f),
            Exit = new Easing(0.4f, 0f, 1f, 1f),
        },
        Density = -1,
        Components = ComponentStyles.Hairline with
        {
            Interaction = ComponentStyles.Hairline.Interaction with { FocusRingColor = SurfaceName.Info },
        },
    };

    /// <summary>Every preset, in the order a picker should list them.</summary>
    public static IReadOnlyList<Theme> All { get; } = [Tonal, Quartz, Linen];

    /// <summary>The preset with a name (ignoring case), or null.</summary>
    public static Theme? Find(string name)
    {
        foreach (var preset in All)
        {
            if (string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return preset;
            }
        }
        return null;
    }

    // Cool greys, indigo, cyan; red, green, amber and blue for status.
    private static ColorRoles QuartzLight() => new()
    {
        Background = Hex(0xffffff),
        Surface = Hex(0xffffff),
        SurfaceDim = Hex(0xf3f4f6),
        SurfaceBright = Hex(0xffffff),
        SurfaceContainerLowest = Hex(0xffffff),
        SurfaceContainerLow = Hex(0xf9fafb),
        SurfaceContainer = Hex(0xf3f4f6),
        SurfaceContainerHigh = Hex(0xeceef1),
        SurfaceContainerHighest = Hex(0xe5e7eb),
        SurfaceVariant = Hex(0xf3f4f6),
        OnSurface = Hex(0x111827),
        OnSurfaceVariant = Hex(0x5b616e),
        InverseSurface = Hex(0x111827),
        InverseOnSurface = Hex(0xf9fafb),
        InversePrimary = Hex(0xa5b4fc),
        Outline = Hex(0xd1d5db),
        OutlineVariant = Hex(0xe5e7eb),
        Scrim = Hex(0x111827),
        Primary = new(Hex(0x4f46e5), Hex(0xffffff), Hex(0xeef2ff), Hex(0x4338ca)),
        Secondary = new(Hex(0x374151), Hex(0xffffff), Hex(0xf3f4f6), Hex(0x111827)),
        Tertiary = new(Hex(0x0e7490), Hex(0xffffff), Hex(0xecfeff), Hex(0x155e75)),
        Error = new(Hex(0xdc2626), Hex(0xffffff), Hex(0xfef2f2), Hex(0x991b1b)),
        Success = new(Hex(0x15803d), Hex(0xffffff), Hex(0xf0fdf4), Hex(0x166534)),
        Warning = new(Hex(0xf59e0b), Hex(0x451a03), Hex(0xfffbeb), Hex(0x92400e)),
        Info = new(Hex(0x2563eb), Hex(0xffffff), Hex(0xeff6ff), Hex(0x1e40af)),
    };

    private static ColorRoles QuartzDark() => new()
    {
        Background = Hex(0x111827),
        Surface = Hex(0x111827),
        SurfaceDim = Hex(0x0b111e),
        SurfaceBright = Hex(0x1f2937),
        SurfaceContainerLowest = Hex(0x0b111e),
        SurfaceContainerLow = Hex(0x161e2d),
        SurfaceContainer = Hex(0x1f2937),
        SurfaceContainerHigh = Hex(0x283343),
        SurfaceContainerHighest = Hex(0x374151),
        SurfaceVariant = Hex(0x1f2937),
        OnSurface = Hex(0xf9fafb),
        OnSurfaceVariant = Hex(0xaeb4be),
        InverseSurface = Hex(0xf3f4f6),
        InverseOnSurface = Hex(0x111827),
        InversePrimary = Hex(0x4f46e5),
        Outline = Hex(0x4b5563),
        OutlineVariant = Hex(0x2a3342),
        Primary = new(Hex(0x5a54e8), Hex(0xffffff), Hex(0x232857), Hex(0xc7d2fe)),
        Secondary = new(Hex(0xd1d5db), Hex(0x111827), Hex(0x2d3748), Hex(0xf9fafb)),
        Tertiary = new(Hex(0x22d3ee), Hex(0x083344), Hex(0x133245), Hex(0xa5f3fc)),
        Error = new(Hex(0xf87171), Hex(0x450a0a), Hex(0x3a1f29), Hex(0xfca5a5)),
        Success = new(Hex(0x4ade80), Hex(0x052e16), Hex(0x14322f), Hex(0x86efac)),
        Warning = new(Hex(0xfbbf24), Hex(0x451a03), Hex(0x362d20), Hex(0xfcd34d)),
        Info = new(Hex(0x60a5fa), Hex(0x172554), Hex(0x182a4a), Hex(0x93c5fd)),
    };

    // Ivory and warm charcoal, terracotta, olive; warm status colours.
    private static ColorRoles LinenLight() => new()
    {
        Background = Hex(0xfaf9f5),
        Surface = Hex(0xfaf9f5),
        SurfaceDim = Hex(0xf0eee6),
        SurfaceBright = Hex(0xffffff),
        SurfaceContainerLowest = Hex(0xffffff),
        SurfaceContainerLow = Hex(0xf5f4ed),
        SurfaceContainer = Hex(0xf0eee6),
        SurfaceContainerHigh = Hex(0xebe9e1),
        SurfaceContainerHighest = Hex(0xe3e1d8),
        SurfaceVariant = Hex(0xf0eee6),
        OnSurface = Hex(0x141413),
        OnSurfaceVariant = Hex(0x5f5e59),
        InverseSurface = Hex(0x30302e),
        InverseOnSurface = Hex(0xfaf9f5),
        InversePrimary = Hex(0xe8a086),
        Outline = Hex(0xc2c0b6),
        OutlineVariant = Hex(0xe3e1d8),
        Scrim = Hex(0x141413),
        Shadow = Hex(0x1f1e1d),
        Primary = new(Hex(0xb5563a), Hex(0xffffff), Hex(0xf6e4db), Hex(0x7a3219)),
        Secondary = new(Hex(0x3d3d3a), Hex(0xfaf9f5), Hex(0xe8e6dc), Hex(0x141413)),
        Tertiary = new(Hex(0x5f7148), Hex(0xffffff), Hex(0xe6ebdc), Hex(0x384629)),
        Error = new(Hex(0xb3362f), Hex(0xffffff), Hex(0xf7e2de), Hex(0x7a1f1a)),
        Success = new(Hex(0x467a2e), Hex(0xffffff), Hex(0xe4eddb), Hex(0x2c4f1c)),
        Warning = new(Hex(0x9a6512), Hex(0xffffff), Hex(0xf6ead2), Hex(0x6b4508)),
        Info = new(Hex(0x2f6fb5), Hex(0xffffff), Hex(0xe2ecf7), Hex(0x1d4f85)),
    };

    private static ColorRoles LinenDark() => new()
    {
        Background = Hex(0x262624),
        Surface = Hex(0x262624),
        SurfaceDim = Hex(0x1f1e1d),
        SurfaceBright = Hex(0x30302e),
        SurfaceContainerLowest = Hex(0x1f1e1d),
        SurfaceContainerLow = Hex(0x2b2b29),
        SurfaceContainer = Hex(0x30302e),
        SurfaceContainerHigh = Hex(0x393936),
        SurfaceContainerHighest = Hex(0x3f3f3b),
        SurfaceVariant = Hex(0x30302e),
        OnSurface = Hex(0xfaf9f5),
        OnSurfaceVariant = Hex(0xadaba2),
        InverseSurface = Hex(0xf0eee6),
        InverseOnSurface = Hex(0x262624),
        InversePrimary = Hex(0xa44e33),
        Outline = Hex(0x5e5d59),
        OutlineVariant = Hex(0x3e3e3a),
        Primary = new(Hex(0xd97757), Hex(0x1f1e1d), Hex(0x4a352d), Hex(0xf2bca7)),
        Secondary = new(Hex(0xc2c0b6), Hex(0x1f1e1d), Hex(0x3b3b38), Hex(0xfaf9f5)),
        Tertiary = new(Hex(0x9db07e), Hex(0x1f2614), Hex(0x363d2c), Hex(0xd3e0bf)),
        Error = new(Hex(0xea7c74), Hex(0x2b0b08), Hex(0x4a2926), Hex(0xf7c2bc)),
        Success = new(Hex(0x8dbf6f), Hex(0x152409), Hex(0x2f3e27), Hex(0xc9e3b6)),
        Warning = new(Hex(0xe0ae52), Hex(0x2b1d05), Hex(0x463924), Hex(0xf1d8a6)),
        Info = new(Hex(0x6fa8e3), Hex(0x0b2239), Hex(0x283847), Hex(0xbfd9f4)),
    };

    private static Color Hex(uint rgb) => Color.FromArgb(0xFF000000 | rgb);

    // Tracking is given in ems.
    private static TextStyle Sans(float size, float lineHeight, float weight, float tracking) => new()
    {
        FontFamily = FontLibrary.Inter,
        Size = size,
        LineHeight = lineHeight,
        Weight = weight,
        Tracking = tracking,
    };

    private static TextStyle Serif(float size, float lineHeight, float weight, float tracking) =>
        Sans(size, lineHeight, weight, tracking) with { FontFamily = FontLibrary.SourceSerif };
}
