// Copyright 2022 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System.Collections.Generic;

namespace Radiant.ColorSystem;

/// <summary>A Dynamic Color theme that maxes out colorfulness at each position in the primary tonal palette.</summary>
public sealed class SchemeVibrant : DynamicScheme
{
    /// <summary>A Vibrant scheme from one source color.</summary>
    public SchemeVibrant(
        Hct sourceColorHct,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = DefaultSpecVersion,
        Platform platform = DefaultPlatform)
        : base(sourceColorHct, Variant.Vibrant, isDark, contrastLevel, platform, specVersion)
    {
    }

    /// <summary>A Vibrant scheme from several source colors; the first is the main one.</summary>
    public SchemeVibrant(
        IReadOnlyList<Hct> sourceColorHcts,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = DefaultSpecVersion,
        Platform platform = DefaultPlatform)
        : base(sourceColorHcts, Variant.Vibrant, isDark, contrastLevel, platform, specVersion)
    {
    }
}
