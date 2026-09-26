// Copyright 2022 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System.Collections.Generic;

namespace Radiant.ColorSystem;

/// <summary>A Dynamic Color theme with low to medium colorfulness and a tertiary tonal palette with a hue related to the source color. The default variant.</summary>
public sealed class SchemeTonalSpot : DynamicScheme
{
    /// <summary>A TonalSpot scheme from one source color.</summary>
    public SchemeTonalSpot(
        Hct sourceColorHct,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = DefaultSpecVersion,
        Platform platform = DefaultPlatform)
        : base(sourceColorHct, Variant.TonalSpot, isDark, contrastLevel, platform, specVersion)
    {
    }

    /// <summary>A TonalSpot scheme from several source colors; the first is the main one.</summary>
    public SchemeTonalSpot(
        IReadOnlyList<Hct> sourceColorHcts,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = DefaultSpecVersion,
        Platform platform = DefaultPlatform)
        : base(sourceColorHcts, Variant.TonalSpot, isDark, contrastLevel, platform, specVersion)
    {
    }
}
