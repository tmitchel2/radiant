using System;
using System.Collections.Generic;
using Radiant.Components;

namespace Radiant.Gallery.ThemeLab;

/// <summary>The Type tab: the size of all the type, and a font and weight for each group of roles.</summary>
internal static class TypeSections
{
    public static IReadOnlyList<PropertySection> For(ThemeFields fields)
    {
        var sections = new List<PropertySection>
        {
            new("Scale", [fields.Float("Size", new ThemeLens<float>(TypographyEditing.Scale, TypographyEditing.WithScale), 0.75f, 1.5f, 0.05f, "0.00×")]),
        };
        var families = TypographyEditing.Families;
        foreach (var group in Enum.GetValues<TypeGroup>())
        {
            var family = new ThemeLens<int>(
                t => IndexOf(families, TypographyEditing.Family(t, group)),
                (t, i) => TypographyEditing.WithFamily(t, group, families[i]));
            var weight = new ThemeLens<float>(t => TypographyEditing.Weight(t, group), (t, w) => TypographyEditing.WithWeight(t, group, w));
            sections.Add(new PropertySection(group.ToString(), [fields.Choice("Font", families, family), fields.Float("Weight", weight, 100f, 900f, 50f)]));
        }
        return sections;
    }

    private static int IndexOf(IReadOnlyList<string> list, string item)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == item)
            {
                return i;
            }
        }
        return -1;
    }
}
