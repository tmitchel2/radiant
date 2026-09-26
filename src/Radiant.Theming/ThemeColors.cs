using Radiant.ColorSystem;
using Radiant.Graphics2D;

namespace Radiant.Theming;

/// <summary>
/// Where a theme's colours come from: a seed colour turned into a full scheme by Radiant's colour
/// system (the Material 3 method), in one of its variants, light or dark, at a contrast level; or
/// hand-picked <see cref="ColorRoles"/> for light and dark.
/// </summary>
public sealed record ThemeColors
{
    /// <summary>The colour the scheme is built from.</summary>
    public Color Seed { get; init; } = Color.FromArgb(0xFF6750A4);

    /// <summary>How the seed becomes palettes: tonal spot is calm, vibrant and expressive louder, fidelity faithful to the seed.</summary>
    public Variant Variant { get; init; } = Variant.TonalSpot;

    /// <summary>Whether the scheme is dark.</summary>
    public bool IsDark { get; init; }

    /// <summary>Contrast, from -1 (reduced) through 0 (standard) to 1 (high).</summary>
    public double ContrastLevel { get; init; }

    /// <summary>Which version of the colour specification to follow.</summary>
    public SpecVersion SpecVersion { get; init; } = SpecVersion.Spec2021;

    /// <summary>The success colour, before harmonising.</summary>
    public Color Success { get; init; } = Color.FromArgb(0xFF2E7D32);

    /// <summary>The warning colour, before harmonising.</summary>
    public Color Warning { get; init; } = Color.FromArgb(0xFFB26A00);

    /// <summary>The information colour, before harmonising.</summary>
    public Color Info { get; init; } = Color.FromArgb(0xFF0061A4);

    /// <summary>Whether success, warning and info are nudged towards the seed's hue, so they sit well with it.</summary>
    public bool HarmonizeCustomColors { get; init; } = true;

    /// <summary>
    /// Hand-picked colours for the light appearance, used instead of the seed's scheme when not
    /// <see cref="IsDark"/>. A fixed palette has one contrast level: <see cref="ContrastLevel"/>,
    /// <see cref="Variant"/> and the seed don't change it.
    /// </summary>
    public ColorRoles? Light { get; init; }

    /// <summary>Hand-picked colours for the dark appearance, used instead of the seed's scheme when <see cref="IsDark"/>.</summary>
    public ColorRoles? Dark { get; init; }

    /// <summary>The hand-picked colours in force for the appearance, or null if they come from the seed.</summary>
    public ColorRoles? Roles => IsDark ? Dark : Light;
}
