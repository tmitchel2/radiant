// Copyright 2022 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System.Collections.Generic;

namespace Radiant.ColorSystem;

/// <summary>A Dynamic Color theme that is grayscale.</summary>
public sealed class SchemeMonochrome : DynamicScheme
{
    /// <summary>A Monochrome scheme from one source color.</summary>
    public SchemeMonochrome(
        Hct sourceColorHct,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = DefaultSpecVersion,
        Platform platform = DefaultPlatform)
        : base(sourceColorHct, Variant.Monochrome, isDark, contrastLevel, platform, specVersion)
    {
    }

    /// <summary>A Monochrome scheme from several source colors; the first is the main one.</summary>
    public SchemeMonochrome(
        IReadOnlyList<Hct> sourceColorHcts,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = DefaultSpecVersion,
        Platform platform = DefaultPlatform)
        : base(sourceColorHcts, Variant.Monochrome, isDark, contrastLevel, platform, specVersion)
    {
    }
}
