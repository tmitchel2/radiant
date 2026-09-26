using System;
using System.Collections.Generic;
using Radiant.Text;

namespace Radiant.Theming;

/// <summary>
/// The text style of each <see cref="TextType"/>. The default is Material 3's type scale in Inter,
/// with Destash's extra steps and JetBrains Mono for code. Colour comes from the surface, not
/// from here.
/// </summary>
public sealed record TypeScale
{
    private readonly IReadOnlyDictionary<TextType, TextStyle> _styles;

    /// <summary>A scale from explicit styles; missing types fall back to the default scale.</summary>
    public TypeScale(IReadOnlyDictionary<TextType, TextStyle> styles) => _styles = styles;

    /// <summary>Material 3's type scale in Inter (and JetBrains Mono for code).</summary>
    public static TypeScale Default { get; } = new(new Dictionary<TextType, TextStyle>
    {
        [TextType.DisplayLarge] = Style(57, 64, FontWeight.Regular, -0.25f),
        [TextType.DisplayMedium] = Style(45, 52, FontWeight.Regular, 0f),
        [TextType.DisplaySmall] = Style(36, 44, FontWeight.Regular, 0f),
        [TextType.HeadlineLarge] = Style(32, 40, FontWeight.Regular, 0f),
        [TextType.HeadlineMedium] = Style(28, 36, FontWeight.Regular, 0f),
        [TextType.HeadlineSmall] = Style(24, 32, FontWeight.Regular, 0f),
        [TextType.HeadlineExtraSmall] = Style(22, 28, FontWeight.Regular, 0f),
        [TextType.TitleLarge] = Style(22, 28, FontWeight.Regular, 0f),
        [TextType.TitleSemiLarge] = Style(18, 26, FontWeight.Medium, 0f),
        [TextType.TitleMedium] = Style(16, 24, FontWeight.Medium, 0.15f),
        [TextType.TitleSmall] = Style(14, 20, FontWeight.Medium, 0.1f),
        [TextType.LabelLarge] = Style(14, 20, FontWeight.Medium, 0.1f),
        [TextType.LabelMedium] = Style(12, 16, FontWeight.Medium, 0.5f),
        [TextType.LabelSmall] = Style(11, 16, FontWeight.Medium, 0.5f),
        [TextType.BodyExtraLarge] = Style(18, 24, FontWeight.Regular, 0f),
        [TextType.BodyLarge] = Style(16, 24, FontWeight.Regular, 0.5f),
        [TextType.BodyMedium] = Style(14, 20, FontWeight.Regular, 0.25f),
        [TextType.BodySmall] = Style(12, 16, FontWeight.Regular, 0.4f),
        [TextType.BodyExtraSmall] = Style(11, 16, FontWeight.Regular, 0f),
        [TextType.Code] = Style(13, 20, FontWeight.Regular, 0f) with { FontFamily = FontLibrary.JetBrainsMono },
        [TextType.Overline] = Style(11, 16, FontWeight.SemiBold, 0.88f),
    });

    /// <summary>A type's style.</summary>
    public TextStyle this[TextType type] =>
        _styles.TryGetValue(type, out var style) ? style : Default._styles[type];

    /// <summary>A scale with every size and line height multiplied by <paramref name="factor"/>.</summary>
    public TypeScale Scaled(float factor)
    {
        var styles = new Dictionary<TextType, TextStyle>();
        foreach (var type in Enum.GetValues<TextType>())
        {
            var style = this[type];
            styles[type] = style with { Size = style.Size * factor, LineHeight = style.LineHeight * factor };
        }
        return new TypeScale(styles);
    }

    // Material gives tracking in pixels; Radiant.Text takes ems.
    private static TextStyle Style(float size, float lineHeight, float weight, float trackingPx) => new()
    {
        FontFamily = FontLibrary.Inter,
        Size = size,
        LineHeight = lineHeight,
        Weight = weight,
        Tracking = trackingPx / size,
    };
}
