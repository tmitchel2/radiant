using System;
using System.Collections.Generic;
using Radiant.Components;
using Radiant.Theming;

namespace Radiant.Gallery.ThemeLab;

/// <summary>The Shape tab: the corner radii, pill or square controls, density and spacing.</summary>
internal static class ShapeSections
{
    public static IReadOnlyList<PropertySection> For(ThemeFields fields)
    {
        var shape = ThemeLenses.Shape;
        var pill = fields.Theme.Shape.Control >= ShapeScale.FullRadius;
        // Squaring the controls goes back to the preset's control radius, or a gentle one if it has pills.
        var presetControl = ThemeEditing.BaseOf(fields.Theme).Shape.Control;
        var squared = presetControl >= ShapeScale.FullRadius ? 8f : presetControl;
        PropertyItem Radius(string name, Func<ShapeScale, float> get, Func<ShapeScale, float, ShapeScale> set) => fields.Float(name, shape.Then(get, set), 0f, 64f);
        var pills = fields.Bool("Pill controls", shape.Then(s => s.Control >= ShapeScale.FullRadius, (s, on) => s with { Control = on ? ShapeScale.FullRadius : squared }));
        return
        [
            new PropertySection("Radii",
            [
                Radius("Extra small", s => s.ExtraSmall, (s, v) => s with { ExtraSmall = v }),
                Radius("Small", s => s.Small, (s, v) => s with { Small = v }),
                Radius("Medium", s => s.Medium, (s, v) => s with { Medium = v }),
                Radius("Large", s => s.Large, (s, v) => s with { Large = v }),
                Radius("Large increased", s => s.LargeIncreased, (s, v) => s with { LargeIncreased = v }),
                Radius("Semi large", s => s.SemiLarge, (s, v) => s with { SemiLarge = v }),
                Radius("Extra large", s => s.ExtraLarge, (s, v) => s with { ExtraLarge = v }),
                Radius("Extra large increased", s => s.ExtraLargeIncreased, (s, v) => s with { ExtraLargeIncreased = v }),
                Radius("Extra extra large", s => s.ExtraExtraLarge, (s, v) => s with { ExtraExtraLarge = v }),
            ]),
            new PropertySection("Controls", pill ? [pills] : [pills, Radius("Control radius", s => s.Control, (s, v) => s with { Control = v })]),
            new PropertySection("Layout",
            [
                fields.Float("Density", ThemeLenses.Theme.Then(t => (float)t.Density, (t, v) => t with { Density = (int)MathF.Round(v) }), -3f, 2f),
                fields.Float("Spacing unit", ThemeLenses.Theme.Then(t => t.SpacingUnit, (t, v) => t with { SpacingUnit = v }), 2f, 8f),
            ]),
        ];
    }
}
