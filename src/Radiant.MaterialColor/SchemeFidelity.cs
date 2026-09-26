// Copyright 2023 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System.Collections.Generic;

namespace Radiant.MaterialColor;

/// <summary>A scheme that places the source color in the primary container. It is adjusted for color relativity, keeping a constant appearance in light and dark mode (about 5 tone added in light mode, subtracted in dark); the tertiary container is the complement to the source color, from <see cref="TemperatureCache"/>, likewise constant in appearance.</summary>
public sealed class SchemeFidelity : DynamicScheme
{
    /// <summary>A Fidelity scheme from one source color.</summary>
    public SchemeFidelity(
        Hct sourceColorHct,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = DefaultSpecVersion,
        Platform platform = DefaultPlatform)
        : base(sourceColorHct, Variant.Fidelity, isDark, contrastLevel, platform, specVersion)
    {
    }

    /// <summary>A Fidelity scheme from several source colors; the first is the main one.</summary>
    public SchemeFidelity(
        IReadOnlyList<Hct> sourceColorHcts,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = DefaultSpecVersion,
        Platform platform = DefaultPlatform)
        : base(sourceColorHcts, Variant.Fidelity, isDark, contrastLevel, platform, specVersion)
    {
    }
}
