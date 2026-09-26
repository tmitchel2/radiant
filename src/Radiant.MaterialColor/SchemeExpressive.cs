// Copyright 2022 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System.Collections.Generic;

namespace Radiant.MaterialColor;

/// <summary>A Dynamic Color theme that is intentionally detached from the source color.</summary>
public sealed class SchemeExpressive : DynamicScheme
{
    /// <summary>A Expressive scheme from one source color.</summary>
    public SchemeExpressive(
        Hct sourceColorHct,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = DefaultSpecVersion,
        Platform platform = DefaultPlatform)
        : base(sourceColorHct, Variant.Expressive, isDark, contrastLevel, platform, specVersion)
    {
    }

    /// <summary>A Expressive scheme from several source colors; the first is the main one.</summary>
    public SchemeExpressive(
        IReadOnlyList<Hct> sourceColorHcts,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = DefaultSpecVersion,
        Platform platform = DefaultPlatform)
        : base(sourceColorHcts, Variant.Expressive, isDark, contrastLevel, platform, specVersion)
    {
    }
}
