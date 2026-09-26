// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

namespace Radiant.ColorSystem;

/// <summary>The palette rules for each spec version.</summary>
internal static class SchemePalettes
{
    private static readonly SchemePalettes2021 s_spec2021 = new();
    private static readonly SchemePalettes2025 s_spec2025 = new();

    /// <summary>
    /// The rules for a spec version: 2025's for 2025, and 2021's otherwise. A 2026 scheme reaches
    /// here only for <see cref="Variant.Cmf"/> (the others fall back to 2025), which supplies its
    /// own palettes.
    /// </summary>
    public static SchemePalettes2021 For(SpecVersion specVersion) =>
        specVersion == SpecVersion.Spec2025 ? s_spec2025 : s_spec2021;
}
