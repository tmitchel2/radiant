using System;
using System.Collections.Generic;
using Radiant.Text;
using Radiant.Theming;

namespace Radiant.Gallery.ThemeLab;

/// <summary>
/// Type edits: an overall size, and a font and weight per <see cref="TypeGroup"/>. Every edit is
/// worked out afresh from the preset's own type scale, so each role keeps its place in the scale
/// (a weight is moved, not replaced), and putting everything back gives the preset's scale itself.
/// </summary>
internal static class TypographyEditing
{
    /// <summary>The fonts that can be chosen: those the font library embeds.</summary>
    public static IReadOnlyList<string> Families { get; } = [FontLibrary.Inter, FontLibrary.SourceSerif, FontLibrary.JetBrainsMono];

    public static TypeGroup GroupOf(TextType type) => type switch
    {
        TextType.DisplayLarge or TextType.DisplayMedium or TextType.DisplaySmall => TypeGroup.Display,
        TextType.HeadlineLarge or TextType.HeadlineMedium or TextType.HeadlineSmall or TextType.HeadlineExtraSmall => TypeGroup.Headline,
        TextType.TitleLarge or TextType.TitleSemiLarge or TextType.TitleMedium or TextType.TitleSmall => TypeGroup.Title,
        TextType.BodyExtraLarge or TextType.BodyLarge or TextType.BodyMedium or TextType.BodySmall or TextType.BodyExtraSmall => TypeGroup.Body,
        TextType.Code => TypeGroup.Code,
        _ => TypeGroup.Label,
    };

    /// <summary>The role whose font and weight stand for its group's.</summary>
    public static TextType Representative(TypeGroup group) => group switch
    {
        TypeGroup.Display => TextType.DisplaySmall,
        TypeGroup.Headline => TextType.HeadlineSmall,
        TypeGroup.Title => TextType.TitleMedium,
        TypeGroup.Body => TextType.BodyMedium,
        TypeGroup.Label => TextType.LabelLarge,
        _ => TextType.Code,
    };

    /// <summary>The size of the type against the preset's, 1 for the same.</summary>
    public static float Scale(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var preset = ThemeEditing.BaseOf(theme).Typography[TextType.BodyMedium].Size;
        return MathF.Round(theme.Typography[TextType.BodyMedium].Size / preset, 2);
    }

    public static string Family(Theme theme, TypeGroup group) => theme.Typography[Representative(group)].FontFamily;

    public static float Weight(Theme theme, TypeGroup group) => theme.Typography[Representative(group)].Weight;

    public static Theme WithScale(Theme theme, float scale) => Rebuild(theme, scale, null, null, null);

    public static Theme WithFamily(Theme theme, TypeGroup group, string family) => Rebuild(theme, Scale(theme), group, family, null);

    public static Theme WithWeight(Theme theme, TypeGroup group, float weight) => Rebuild(theme, Scale(theme), group, null, weight);

    private static Theme Rebuild(Theme theme, float scale, TypeGroup? changed, string? family, float? weight)
    {
        var preset = ThemeEditing.BaseOf(theme).Typography;
        var styles = new Dictionary<TextType, TextStyle>();
        var unchanged = true;
        foreach (var type in Enum.GetValues<TextType>())
        {
            var group = GroupOf(type);
            var style = preset[type];
            var groupWeight = group == changed && weight is { } w ? w : Weight(theme, group);
            var next = style with
            {
                Size = style.Size * scale,
                LineHeight = style.LineHeight * scale,
                FontFamily = group == changed && family is not null ? family : Family(theme, group),
                Weight = Math.Clamp(style.Weight + groupWeight - preset[Representative(group)].Weight, FontWeight.Thin, FontWeight.Black),
            };
            unchanged &= next == style;
            styles[type] = next;
        }
        // The preset's own scale when nothing differs: type scales compare by reference.
        return theme with { Typography = unchanged ? preset : new TypeScale(styles) };
    }
}
