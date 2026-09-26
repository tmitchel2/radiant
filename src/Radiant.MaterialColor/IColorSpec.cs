// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

namespace Radiant.MaterialColor;

/// <summary>
/// The Material color roles as one spec version defines them. Upstream's <c>ColorSpecDelegate</c>;
/// <see cref="MaterialDynamicColors"/> exposes the latest one, whose roles fall back to earlier
/// definitions for schemes of earlier spec versions.
/// </summary>
internal interface IColorSpec
{
    /// <summary>The <c>primaryPaletteKeyColor</c> role.</summary>
    DynamicColor PrimaryPaletteKeyColor();

    /// <summary>The <c>secondaryPaletteKeyColor</c> role.</summary>
    DynamicColor SecondaryPaletteKeyColor();

    /// <summary>The <c>tertiaryPaletteKeyColor</c> role.</summary>
    DynamicColor TertiaryPaletteKeyColor();

    /// <summary>The <c>neutralPaletteKeyColor</c> role.</summary>
    DynamicColor NeutralPaletteKeyColor();

    /// <summary>The <c>neutralVariantPaletteKeyColor</c> role.</summary>
    DynamicColor NeutralVariantPaletteKeyColor();

    /// <summary>The <c>errorPaletteKeyColor</c> role.</summary>
    DynamicColor ErrorPaletteKeyColor();

    /// <summary>The <c>background</c> role.</summary>
    DynamicColor Background();

    /// <summary>The <c>onBackground</c> role.</summary>
    DynamicColor OnBackground();

    /// <summary>The <c>surface</c> role.</summary>
    DynamicColor Surface();

    /// <summary>The <c>surfaceDim</c> role.</summary>
    DynamicColor SurfaceDim();

    /// <summary>The <c>surfaceBright</c> role.</summary>
    DynamicColor SurfaceBright();

    /// <summary>The <c>surfaceContainerLowest</c> role.</summary>
    DynamicColor SurfaceContainerLowest();

    /// <summary>The <c>surfaceContainerLow</c> role.</summary>
    DynamicColor SurfaceContainerLow();

    /// <summary>The <c>surfaceContainer</c> role.</summary>
    DynamicColor SurfaceContainer();

    /// <summary>The <c>surfaceContainerHigh</c> role.</summary>
    DynamicColor SurfaceContainerHigh();

    /// <summary>The <c>surfaceContainerHighest</c> role.</summary>
    DynamicColor SurfaceContainerHighest();

    /// <summary>The <c>onSurface</c> role.</summary>
    DynamicColor OnSurface();

    /// <summary>The <c>surfaceVariant</c> role.</summary>
    DynamicColor SurfaceVariant();

    /// <summary>The <c>onSurfaceVariant</c> role.</summary>
    DynamicColor OnSurfaceVariant();

    /// <summary>The <c>inverseSurface</c> role.</summary>
    DynamicColor InverseSurface();

    /// <summary>The <c>inverseOnSurface</c> role.</summary>
    DynamicColor InverseOnSurface();

    /// <summary>The <c>outline</c> role.</summary>
    DynamicColor Outline();

    /// <summary>The <c>outlineVariant</c> role.</summary>
    DynamicColor OutlineVariant();

    /// <summary>The <c>shadow</c> role.</summary>
    DynamicColor Shadow();

    /// <summary>The <c>scrim</c> role.</summary>
    DynamicColor Scrim();

    /// <summary>The <c>surfaceTint</c> role.</summary>
    DynamicColor SurfaceTint();

    /// <summary>The <c>primary</c> role.</summary>
    DynamicColor Primary();

    /// <summary>The <c>primaryDim</c> role; null before the 2025 spec.</summary>
    DynamicColor? PrimaryDim();

    /// <summary>The <c>onPrimary</c> role.</summary>
    DynamicColor OnPrimary();

    /// <summary>The <c>primaryContainer</c> role.</summary>
    DynamicColor PrimaryContainer();

    /// <summary>The <c>onPrimaryContainer</c> role.</summary>
    DynamicColor OnPrimaryContainer();

    /// <summary>The <c>inversePrimary</c> role.</summary>
    DynamicColor InversePrimary();

    /// <summary>The <c>secondary</c> role.</summary>
    DynamicColor Secondary();

    /// <summary>The <c>secondaryDim</c> role; null before the 2025 spec.</summary>
    DynamicColor? SecondaryDim();

    /// <summary>The <c>onSecondary</c> role.</summary>
    DynamicColor OnSecondary();

    /// <summary>The <c>secondaryContainer</c> role.</summary>
    DynamicColor SecondaryContainer();

    /// <summary>The <c>onSecondaryContainer</c> role.</summary>
    DynamicColor OnSecondaryContainer();

    /// <summary>The <c>tertiary</c> role.</summary>
    DynamicColor Tertiary();

    /// <summary>The <c>tertiaryDim</c> role; null before the 2025 spec.</summary>
    DynamicColor? TertiaryDim();

    /// <summary>The <c>onTertiary</c> role.</summary>
    DynamicColor OnTertiary();

    /// <summary>The <c>tertiaryContainer</c> role.</summary>
    DynamicColor TertiaryContainer();

    /// <summary>The <c>onTertiaryContainer</c> role.</summary>
    DynamicColor OnTertiaryContainer();

    /// <summary>The <c>error</c> role.</summary>
    DynamicColor Error();

    /// <summary>The <c>errorDim</c> role; null before the 2025 spec.</summary>
    DynamicColor? ErrorDim();

    /// <summary>The <c>onError</c> role.</summary>
    DynamicColor OnError();

    /// <summary>The <c>errorContainer</c> role.</summary>
    DynamicColor ErrorContainer();

    /// <summary>The <c>onErrorContainer</c> role.</summary>
    DynamicColor OnErrorContainer();

    /// <summary>The <c>primaryFixed</c> role.</summary>
    DynamicColor PrimaryFixed();

    /// <summary>The <c>primaryFixedDim</c> role.</summary>
    DynamicColor PrimaryFixedDim();

    /// <summary>The <c>onPrimaryFixed</c> role.</summary>
    DynamicColor OnPrimaryFixed();

    /// <summary>The <c>onPrimaryFixedVariant</c> role.</summary>
    DynamicColor OnPrimaryFixedVariant();

    /// <summary>The <c>secondaryFixed</c> role.</summary>
    DynamicColor SecondaryFixed();

    /// <summary>The <c>secondaryFixedDim</c> role.</summary>
    DynamicColor SecondaryFixedDim();

    /// <summary>The <c>onSecondaryFixed</c> role.</summary>
    DynamicColor OnSecondaryFixed();

    /// <summary>The <c>onSecondaryFixedVariant</c> role.</summary>
    DynamicColor OnSecondaryFixedVariant();

    /// <summary>The <c>tertiaryFixed</c> role.</summary>
    DynamicColor TertiaryFixed();

    /// <summary>The <c>tertiaryFixedDim</c> role.</summary>
    DynamicColor TertiaryFixedDim();

    /// <summary>The <c>onTertiaryFixed</c> role.</summary>
    DynamicColor OnTertiaryFixed();

    /// <summary>The <c>onTertiaryFixedVariant</c> role.</summary>
    DynamicColor OnTertiaryFixedVariant();

    /// <summary>The highest surface for a scheme: the one foregrounds must contrast with.</summary>
    DynamicColor HighestSurface(DynamicScheme s);
}
