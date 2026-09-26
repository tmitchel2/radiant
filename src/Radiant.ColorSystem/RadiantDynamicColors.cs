// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System.Collections.Generic;
using System.Linq;

namespace Radiant.ColorSystem;

/// <summary>
/// The dynamic colors for the roles of the Radiant color system: primary, onPrimary,
/// surfaceContainerHigh and the rest. Resolve one with <see cref="DynamicScheme.GetArgb"/>, or read
/// the scheme's role properties. (Upstream's are instance methods on an object every scheme holds;
/// with nothing per instance, here they are static.)
/// <para>
/// Defined by the latest spec (2026); each role behaves as the spec version of the scheme it is
/// resolved against defines it. Upstream builds a new <see cref="DynamicColor"/> on every call; they
/// are immutable functions of the scheme, so here each is built once and shared, which also lets a
/// scheme's resolved-color cache hit.
/// </para>
/// </summary>
public static class RadiantDynamicColors
{
    /// <summary>How far, in tone, content-based accents sit from their containers.</summary>
    public const double ContentAccentToneDelta = 15.0;

    private static readonly ColorSpec2026 s_spec = new();

    private static readonly DynamicColor s_primaryPaletteKeyColor = s_spec.PrimaryPaletteKeyColor();
    private static readonly DynamicColor s_secondaryPaletteKeyColor = s_spec.SecondaryPaletteKeyColor();
    private static readonly DynamicColor s_tertiaryPaletteKeyColor = s_spec.TertiaryPaletteKeyColor();
    private static readonly DynamicColor s_neutralPaletteKeyColor = s_spec.NeutralPaletteKeyColor();
    private static readonly DynamicColor s_neutralVariantPaletteKeyColor = s_spec.NeutralVariantPaletteKeyColor();
    private static readonly DynamicColor s_errorPaletteKeyColor = s_spec.ErrorPaletteKeyColor();
    private static readonly DynamicColor s_background = s_spec.Background();
    private static readonly DynamicColor s_onBackground = s_spec.OnBackground();
    private static readonly DynamicColor s_surface = s_spec.Surface();
    private static readonly DynamicColor s_surfaceDim = s_spec.SurfaceDim();
    private static readonly DynamicColor s_surfaceBright = s_spec.SurfaceBright();
    private static readonly DynamicColor s_surfaceContainerLowest = s_spec.SurfaceContainerLowest();
    private static readonly DynamicColor s_surfaceContainerLow = s_spec.SurfaceContainerLow();
    private static readonly DynamicColor s_surfaceContainer = s_spec.SurfaceContainer();
    private static readonly DynamicColor s_surfaceContainerHigh = s_spec.SurfaceContainerHigh();
    private static readonly DynamicColor s_surfaceContainerHighest = s_spec.SurfaceContainerHighest();
    private static readonly DynamicColor s_onSurface = s_spec.OnSurface();
    private static readonly DynamicColor s_surfaceVariant = s_spec.SurfaceVariant();
    private static readonly DynamicColor s_onSurfaceVariant = s_spec.OnSurfaceVariant();
    private static readonly DynamicColor s_inverseSurface = s_spec.InverseSurface();
    private static readonly DynamicColor s_inverseOnSurface = s_spec.InverseOnSurface();
    private static readonly DynamicColor s_outline = s_spec.Outline();
    private static readonly DynamicColor s_outlineVariant = s_spec.OutlineVariant();
    private static readonly DynamicColor s_shadow = s_spec.Shadow();
    private static readonly DynamicColor s_scrim = s_spec.Scrim();
    private static readonly DynamicColor s_surfaceTint = s_spec.SurfaceTint();
    private static readonly DynamicColor s_primary = s_spec.Primary();
    private static readonly DynamicColor? s_primaryDim = s_spec.PrimaryDim();
    private static readonly DynamicColor s_onPrimary = s_spec.OnPrimary();
    private static readonly DynamicColor s_primaryContainer = s_spec.PrimaryContainer();
    private static readonly DynamicColor s_onPrimaryContainer = s_spec.OnPrimaryContainer();
    private static readonly DynamicColor s_inversePrimary = s_spec.InversePrimary();
    private static readonly DynamicColor s_secondary = s_spec.Secondary();
    private static readonly DynamicColor? s_secondaryDim = s_spec.SecondaryDim();
    private static readonly DynamicColor s_onSecondary = s_spec.OnSecondary();
    private static readonly DynamicColor s_secondaryContainer = s_spec.SecondaryContainer();
    private static readonly DynamicColor s_onSecondaryContainer = s_spec.OnSecondaryContainer();
    private static readonly DynamicColor s_tertiary = s_spec.Tertiary();
    private static readonly DynamicColor? s_tertiaryDim = s_spec.TertiaryDim();
    private static readonly DynamicColor s_onTertiary = s_spec.OnTertiary();
    private static readonly DynamicColor s_tertiaryContainer = s_spec.TertiaryContainer();
    private static readonly DynamicColor s_onTertiaryContainer = s_spec.OnTertiaryContainer();
    private static readonly DynamicColor s_error = s_spec.Error();
    private static readonly DynamicColor? s_errorDim = s_spec.ErrorDim();
    private static readonly DynamicColor s_onError = s_spec.OnError();
    private static readonly DynamicColor s_errorContainer = s_spec.ErrorContainer();
    private static readonly DynamicColor s_onErrorContainer = s_spec.OnErrorContainer();
    private static readonly DynamicColor s_primaryFixed = s_spec.PrimaryFixed();
    private static readonly DynamicColor s_primaryFixedDim = s_spec.PrimaryFixedDim();
    private static readonly DynamicColor s_onPrimaryFixed = s_spec.OnPrimaryFixed();
    private static readonly DynamicColor s_onPrimaryFixedVariant = s_spec.OnPrimaryFixedVariant();
    private static readonly DynamicColor s_secondaryFixed = s_spec.SecondaryFixed();
    private static readonly DynamicColor s_secondaryFixedDim = s_spec.SecondaryFixedDim();
    private static readonly DynamicColor s_onSecondaryFixed = s_spec.OnSecondaryFixed();
    private static readonly DynamicColor s_onSecondaryFixedVariant = s_spec.OnSecondaryFixedVariant();
    private static readonly DynamicColor s_tertiaryFixed = s_spec.TertiaryFixed();
    private static readonly DynamicColor s_tertiaryFixedDim = s_spec.TertiaryFixedDim();
    private static readonly DynamicColor s_onTertiaryFixed = s_spec.OnTertiaryFixed();
    private static readonly DynamicColor s_onTertiaryFixedVariant = s_spec.OnTertiaryFixedVariant();

    private static readonly IReadOnlyList<DynamicColor> s_allColors = new List<DynamicColor?>
    {
            s_background,
            s_onBackground,
            s_surface,
            s_surfaceDim,
            s_surfaceBright,
            s_surfaceContainerLowest,
            s_surfaceContainerLow,
            s_surfaceContainer,
            s_surfaceContainerHigh,
            s_surfaceContainerHighest,
            s_onSurface,
            s_onSurfaceVariant,
            s_outline,
            s_outlineVariant,
            s_inverseSurface,
            s_inverseOnSurface,
            s_primary,
            s_primaryDim,
            s_onPrimary,
            s_primaryContainer,
            s_onPrimaryContainer,
            s_primaryFixed,
            s_primaryFixedDim,
            s_onPrimaryFixed,
            s_onPrimaryFixedVariant,
            s_inversePrimary,
            s_secondary,
            s_secondaryDim,
            s_onSecondary,
            s_secondaryContainer,
            s_onSecondaryContainer,
            s_secondaryFixed,
            s_secondaryFixedDim,
            s_onSecondaryFixed,
            s_onSecondaryFixedVariant,
            s_tertiary,
            s_tertiaryDim,
            s_onTertiary,
            s_tertiaryContainer,
            s_onTertiaryContainer,
            s_tertiaryFixed,
            s_tertiaryFixedDim,
            s_onTertiaryFixed,
            s_onTertiaryFixedVariant,
            s_error,
            s_errorDim,
            s_onError,
            s_errorContainer,
            s_onErrorContainer,
    }.OfType<DynamicColor>().ToList().AsReadOnly();

    /// <summary>
    /// Every role a theme needs, in upstream's order (the palette key colors are not included).
    /// </summary>
    public static IReadOnlyList<DynamicColor> AllColors => s_allColors;

    /// <summary>The highest surface for a scheme: the one foregrounds must contrast with.</summary>
    public static DynamicColor HighestSurface(DynamicScheme s) => s_spec.HighestSurface(s);

    /// <summary>The <c>primaryPaletteKeyColor</c> role.</summary>
    public static DynamicColor PrimaryPaletteKeyColor() => s_primaryPaletteKeyColor;

    /// <summary>The <c>secondaryPaletteKeyColor</c> role.</summary>
    public static DynamicColor SecondaryPaletteKeyColor() => s_secondaryPaletteKeyColor;

    /// <summary>The <c>tertiaryPaletteKeyColor</c> role.</summary>
    public static DynamicColor TertiaryPaletteKeyColor() => s_tertiaryPaletteKeyColor;

    /// <summary>The <c>neutralPaletteKeyColor</c> role.</summary>
    public static DynamicColor NeutralPaletteKeyColor() => s_neutralPaletteKeyColor;

    /// <summary>The <c>neutralVariantPaletteKeyColor</c> role.</summary>
    public static DynamicColor NeutralVariantPaletteKeyColor() => s_neutralVariantPaletteKeyColor;

    /// <summary>The <c>errorPaletteKeyColor</c> role.</summary>
    public static DynamicColor ErrorPaletteKeyColor() => s_errorPaletteKeyColor;

    /// <summary>The <c>background</c> role.</summary>
    public static DynamicColor Background() => s_background;

    /// <summary>The <c>onBackground</c> role.</summary>
    public static DynamicColor OnBackground() => s_onBackground;

    /// <summary>The <c>surface</c> role.</summary>
    public static DynamicColor Surface() => s_surface;

    /// <summary>The <c>surfaceDim</c> role.</summary>
    public static DynamicColor SurfaceDim() => s_surfaceDim;

    /// <summary>The <c>surfaceBright</c> role.</summary>
    public static DynamicColor SurfaceBright() => s_surfaceBright;

    /// <summary>The <c>surfaceContainerLowest</c> role.</summary>
    public static DynamicColor SurfaceContainerLowest() => s_surfaceContainerLowest;

    /// <summary>The <c>surfaceContainerLow</c> role.</summary>
    public static DynamicColor SurfaceContainerLow() => s_surfaceContainerLow;

    /// <summary>The <c>surfaceContainer</c> role.</summary>
    public static DynamicColor SurfaceContainer() => s_surfaceContainer;

    /// <summary>The <c>surfaceContainerHigh</c> role.</summary>
    public static DynamicColor SurfaceContainerHigh() => s_surfaceContainerHigh;

    /// <summary>The <c>surfaceContainerHighest</c> role.</summary>
    public static DynamicColor SurfaceContainerHighest() => s_surfaceContainerHighest;

    /// <summary>The <c>onSurface</c> role.</summary>
    public static DynamicColor OnSurface() => s_onSurface;

    /// <summary>The <c>surfaceVariant</c> role.</summary>
    public static DynamicColor SurfaceVariant() => s_surfaceVariant;

    /// <summary>The <c>onSurfaceVariant</c> role.</summary>
    public static DynamicColor OnSurfaceVariant() => s_onSurfaceVariant;

    /// <summary>The <c>inverseSurface</c> role.</summary>
    public static DynamicColor InverseSurface() => s_inverseSurface;

    /// <summary>The <c>inverseOnSurface</c> role.</summary>
    public static DynamicColor InverseOnSurface() => s_inverseOnSurface;

    /// <summary>The <c>outline</c> role.</summary>
    public static DynamicColor Outline() => s_outline;

    /// <summary>The <c>outlineVariant</c> role.</summary>
    public static DynamicColor OutlineVariant() => s_outlineVariant;

    /// <summary>The <c>shadow</c> role.</summary>
    public static DynamicColor Shadow() => s_shadow;

    /// <summary>The <c>scrim</c> role.</summary>
    public static DynamicColor Scrim() => s_scrim;

    /// <summary>The <c>surfaceTint</c> role.</summary>
    public static DynamicColor SurfaceTint() => s_surfaceTint;

    /// <summary>The <c>primary</c> role.</summary>
    public static DynamicColor Primary() => s_primary;

    /// <summary>The <c>primaryDim</c> role; null before the 2025 spec.</summary>
    public static DynamicColor? PrimaryDim() => s_primaryDim;

    /// <summary>The <c>onPrimary</c> role.</summary>
    public static DynamicColor OnPrimary() => s_onPrimary;

    /// <summary>The <c>primaryContainer</c> role.</summary>
    public static DynamicColor PrimaryContainer() => s_primaryContainer;

    /// <summary>The <c>onPrimaryContainer</c> role.</summary>
    public static DynamicColor OnPrimaryContainer() => s_onPrimaryContainer;

    /// <summary>The <c>inversePrimary</c> role.</summary>
    public static DynamicColor InversePrimary() => s_inversePrimary;

    /// <summary>The <c>secondary</c> role.</summary>
    public static DynamicColor Secondary() => s_secondary;

    /// <summary>The <c>secondaryDim</c> role; null before the 2025 spec.</summary>
    public static DynamicColor? SecondaryDim() => s_secondaryDim;

    /// <summary>The <c>onSecondary</c> role.</summary>
    public static DynamicColor OnSecondary() => s_onSecondary;

    /// <summary>The <c>secondaryContainer</c> role.</summary>
    public static DynamicColor SecondaryContainer() => s_secondaryContainer;

    /// <summary>The <c>onSecondaryContainer</c> role.</summary>
    public static DynamicColor OnSecondaryContainer() => s_onSecondaryContainer;

    /// <summary>The <c>tertiary</c> role.</summary>
    public static DynamicColor Tertiary() => s_tertiary;

    /// <summary>The <c>tertiaryDim</c> role; null before the 2025 spec.</summary>
    public static DynamicColor? TertiaryDim() => s_tertiaryDim;

    /// <summary>The <c>onTertiary</c> role.</summary>
    public static DynamicColor OnTertiary() => s_onTertiary;

    /// <summary>The <c>tertiaryContainer</c> role.</summary>
    public static DynamicColor TertiaryContainer() => s_tertiaryContainer;

    /// <summary>The <c>onTertiaryContainer</c> role.</summary>
    public static DynamicColor OnTertiaryContainer() => s_onTertiaryContainer;

    /// <summary>The <c>error</c> role.</summary>
    public static DynamicColor Error() => s_error;

    /// <summary>The <c>errorDim</c> role; null before the 2025 spec.</summary>
    public static DynamicColor? ErrorDim() => s_errorDim;

    /// <summary>The <c>onError</c> role.</summary>
    public static DynamicColor OnError() => s_onError;

    /// <summary>The <c>errorContainer</c> role.</summary>
    public static DynamicColor ErrorContainer() => s_errorContainer;

    /// <summary>The <c>onErrorContainer</c> role.</summary>
    public static DynamicColor OnErrorContainer() => s_onErrorContainer;

    /// <summary>The <c>primaryFixed</c> role.</summary>
    public static DynamicColor PrimaryFixed() => s_primaryFixed;

    /// <summary>The <c>primaryFixedDim</c> role.</summary>
    public static DynamicColor PrimaryFixedDim() => s_primaryFixedDim;

    /// <summary>The <c>onPrimaryFixed</c> role.</summary>
    public static DynamicColor OnPrimaryFixed() => s_onPrimaryFixed;

    /// <summary>The <c>onPrimaryFixedVariant</c> role.</summary>
    public static DynamicColor OnPrimaryFixedVariant() => s_onPrimaryFixedVariant;

    /// <summary>The <c>secondaryFixed</c> role.</summary>
    public static DynamicColor SecondaryFixed() => s_secondaryFixed;

    /// <summary>The <c>secondaryFixedDim</c> role.</summary>
    public static DynamicColor SecondaryFixedDim() => s_secondaryFixedDim;

    /// <summary>The <c>onSecondaryFixed</c> role.</summary>
    public static DynamicColor OnSecondaryFixed() => s_onSecondaryFixed;

    /// <summary>The <c>onSecondaryFixedVariant</c> role.</summary>
    public static DynamicColor OnSecondaryFixedVariant() => s_onSecondaryFixedVariant;

    /// <summary>The <c>tertiaryFixed</c> role.</summary>
    public static DynamicColor TertiaryFixed() => s_tertiaryFixed;

    /// <summary>The <c>tertiaryFixedDim</c> role.</summary>
    public static DynamicColor TertiaryFixedDim() => s_tertiaryFixedDim;

    /// <summary>The <c>onTertiaryFixed</c> role.</summary>
    public static DynamicColor OnTertiaryFixed() => s_onTertiaryFixed;

    /// <summary>The <c>onTertiaryFixedVariant</c> role.</summary>
    public static DynamicColor OnTertiaryFixedVariant() => s_onTertiaryFixedVariant;
}
