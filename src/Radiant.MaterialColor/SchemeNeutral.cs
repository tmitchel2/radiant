// Copyright 2022 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System.Collections.Generic;

namespace Radiant.MaterialColor;

/// <summary>A Dynamic Color theme that is near grayscale.</summary>
public sealed class SchemeNeutral : DynamicScheme
{
    /// <summary>A Neutral scheme from one source color.</summary>
    public SchemeNeutral(
        Hct sourceColorHct,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = DefaultSpecVersion,
        Platform platform = DefaultPlatform)
        : base(sourceColorHct, Variant.Neutral, isDark, contrastLevel, platform, specVersion)
    {
    }

    /// <summary>A Neutral scheme from several source colors; the first is the main one.</summary>
    public SchemeNeutral(
        IReadOnlyList<Hct> sourceColorHcts,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = DefaultSpecVersion,
        Platform platform = DefaultPlatform)
        : base(sourceColorHcts, Variant.Neutral, isDark, contrastLevel, platform, specVersion)
    {
    }
}
