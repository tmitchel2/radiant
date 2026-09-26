// Copyright 2022 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System.Collections.Generic;

namespace Radiant.MaterialColor;

/// <summary>A playful theme: the source color's hue does not appear in the theme.</summary>
public sealed class SchemeFruitSalad : DynamicScheme
{
    /// <summary>A FruitSalad scheme from one source color.</summary>
    public SchemeFruitSalad(
        Hct sourceColorHct,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = DefaultSpecVersion,
        Platform platform = DefaultPlatform)
        : base(sourceColorHct, Variant.FruitSalad, isDark, contrastLevel, platform, specVersion)
    {
    }

    /// <summary>A FruitSalad scheme from several source colors; the first is the main one.</summary>
    public SchemeFruitSalad(
        IReadOnlyList<Hct> sourceColorHcts,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = DefaultSpecVersion,
        Platform platform = DefaultPlatform)
        : base(sourceColorHcts, Variant.FruitSalad, isDark, contrastLevel, platform, specVersion)
    {
    }
}
