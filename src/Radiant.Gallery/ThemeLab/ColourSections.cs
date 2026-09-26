using System.Collections.Generic;
using Radiant.Components;
using Radiant.Theming;
using Color = Radiant.Graphics2D.Color;

namespace Radiant.Gallery.ThemeLab;

/// <summary>
/// The Colour tab. A seeded theme is edited through its seed and scheme; a hand-picked palette
/// colour by colour, in whichever of light or dark is showing.
/// </summary>
internal static class ColourSections
{
    public static IReadOnlyList<PropertySection> For(ThemeFields fields)
    {
        var colors = fields.Theme.Colors;
        var seeded = colors.Roles is null;
        var scheme = new List<PropertyItem>
        {
            fields.Float("Contrast", ThemeLenses.Colors.Then(c => (float)c.ContrastLevel, (c, v) => c with { ContrastLevel = v }), -1f, 1f, 0.1f, "0.0"),
        };
        if (!seeded)
        {
            scheme.Add(fields.Bool("Accent from seed", ThemeLenses.Colors.Then(c => c.AccentFromSeed, (c, v) => c with { AccentFromSeed = v })));
        }
        if (seeded || colors.AccentFromSeed)
        {
            scheme.Add(fields.Color("Seed", ThemeLenses.Colors.Then(c => c.Seed, (c, v) => c with { Seed = v })));
            scheme.Add(fields.Enum("Variant", ThemeLenses.Colors.Then(c => c.Variant, (c, v) => c with { Variant = v })));
            scheme.Add(fields.Enum("Spec", ThemeLenses.Colors.Then(c => c.SpecVersion, (c, v) => c with { SpecVersion = v })));
        }
        if (seeded)
        {
            scheme.Add(fields.Color("Success", ThemeLenses.Colors.Then(c => c.Success, (c, v) => c with { Success = v })));
            scheme.Add(fields.Color("Warning", ThemeLenses.Colors.Then(c => c.Warning, (c, v) => c with { Warning = v })));
            scheme.Add(fields.Color("Info", ThemeLenses.Colors.Then(c => c.Info, (c, v) => c with { Info = v })));
            scheme.Add(fields.Bool("Harmonize", ThemeLenses.Colors.Then(c => c.HarmonizeCustomColors, (c, v) => c with { HarmonizeCustomColors = v })));
            return [new PropertySection("Scheme", scheme)];
        }

        var roles = ThemeLenses.Roles;
        PropertyItem Role(string name, System.Func<ColorRoles, Color> get, System.Func<ColorRoles, Color, ColorRoles> set) => fields.Color(name, roles.Then(get, set));
        PropertySection Family(string name, ThemeLens<ColorFamily> family) => new(name,
        [
            fields.Color("Colour", family.Then(f => f.Color, (f, v) => f with { Color = v })),
            fields.Color("On colour", family.Then(f => f.On, (f, v) => f with { On = v })),
            fields.Color("Container", family.Then(f => f.Container, (f, v) => f with { Container = v })),
            fields.Color("On container", family.Then(f => f.OnContainer, (f, v) => f with { OnContainer = v })),
        ]) { InitiallyOpen = name == "Primary" };
        return
        [
            new PropertySection("Scheme", scheme),
            new PropertySection(colors.IsDark ? "Dark surfaces" : "Light surfaces",
            [
                Role("Background", r => r.Background, (r, v) => r with { Background = v }),
                Role("Surface", r => r.Surface, (r, v) => r with { Surface = v }),
                Role("Surface dim", r => r.SurfaceDim, (r, v) => r with { SurfaceDim = v }),
                Role("Surface bright", r => r.SurfaceBright, (r, v) => r with { SurfaceBright = v }),
                Role("Lowest container", r => r.SurfaceContainerLowest, (r, v) => r with { SurfaceContainerLowest = v }),
                Role("Low container", r => r.SurfaceContainerLow, (r, v) => r with { SurfaceContainerLow = v }),
                Role("Container", r => r.SurfaceContainer, (r, v) => r with { SurfaceContainer = v }),
                Role("High container", r => r.SurfaceContainerHigh, (r, v) => r with { SurfaceContainerHigh = v }),
                Role("Highest container", r => r.SurfaceContainerHighest, (r, v) => r with { SurfaceContainerHighest = v }),
                Role("Surface variant", r => r.SurfaceVariant, (r, v) => r with { SurfaceVariant = v }),
                Role("On surface", r => r.OnSurface, (r, v) => r with { OnSurface = v }),
                Role("On surface variant", r => r.OnSurfaceVariant, (r, v) => r with { OnSurfaceVariant = v }),
                Role("Inverse surface", r => r.InverseSurface, (r, v) => r with { InverseSurface = v }),
                Role("Inverse on surface", r => r.InverseOnSurface, (r, v) => r with { InverseOnSurface = v }),
                Role("Inverse primary", r => r.InversePrimary, (r, v) => r with { InversePrimary = v }),
                Role("Outline", r => r.Outline, (r, v) => r with { Outline = v }),
                Role("Outline variant", r => r.OutlineVariant, (r, v) => r with { OutlineVariant = v }),
                Role("Scrim", r => r.Scrim, (r, v) => r with { Scrim = v }),
                Role("Shadow", r => r.Shadow, (r, v) => r with { Shadow = v }),
            ]) { InitiallyOpen = false },
            Family("Primary", roles.Then(r => r.Primary, (r, v) => r with { Primary = v })),
            Family("Secondary", roles.Then(r => r.Secondary, (r, v) => r with { Secondary = v })),
            Family("Tertiary", roles.Then(r => r.Tertiary, (r, v) => r with { Tertiary = v })),
            Family("Error", roles.Then(r => r.Error, (r, v) => r with { Error = v })),
            Family("Success", roles.Then(r => r.Success, (r, v) => r with { Success = v })),
            Family("Warning", roles.Then(r => r.Warning, (r, v) => r with { Warning = v })),
            Family("Info", roles.Then(r => r.Info, (r, v) => r with { Info = v })),
        ];
    }
}
