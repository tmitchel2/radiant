// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

namespace Radiant.MaterialColor;

/// <summary>
/// The version of the Material design spec a scheme follows. Later versions extend earlier ones, and
/// compare greater (a color defined "from 2025" applies to 2025 and 2026 schemes).
/// </summary>
public enum SpecVersion
{
    /// <summary>The 2021 spec: Material You as launched.</summary>
    Spec2021,

    /// <summary>The 2025 spec: Material 3 Expressive (dim roles, platform-aware surfaces).</summary>
    Spec2025,

    /// <summary>The 2026 spec.</summary>
    Spec2026,
}
