// Copyright 2022 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

namespace Radiant.ColorSystem;

/// <summary>A theme style: how a dynamic scheme derives its palettes from the source color.</summary>
public enum Variant
{
    /// <summary>Grayscale.</summary>
    Monochrome,

    /// <summary>Near grayscale.</summary>
    Neutral,

    /// <summary>
    /// Low to medium colorfulness, with a tertiary palette whose hue is related to the source color.
    /// </summary>
    TonalSpot,

    /// <summary>Maxes out colorfulness at each position in the primary tonal palette.</summary>
    Vibrant,

    /// <summary>Intentionally detached from the source color.</summary>
    Expressive,

    /// <summary>
    /// Places the source color in the primary container, adjusted for color relativity so it keeps a
    /// constant appearance in light and dark mode; the tertiary container is its complement. As
    /// <see cref="Content"/>, with a different tertiary.
    /// </summary>
    Fidelity,

    /// <summary>
    /// Places the source color in the primary container, adjusted for color relativity so it keeps a
    /// constant appearance in light and dark mode; the tertiary is an analogous color.
    /// </summary>
    Content,

    /// <summary>Playful: the source color's hue does not appear in the theme.</summary>
    Rainbow,

    /// <summary>Playful: the source color's hue does not appear in the theme.</summary>
    FruitSalad,

    /// <summary>Two source colors (spec 2026 only).</summary>
    Cmf,
}
