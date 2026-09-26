// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

namespace Radiant.MaterialColor;

/// <summary>
/// How role A's tone relates to role B's in a <see cref="ToneDeltaPair"/>.
/// <see cref="Nearer"/> and <see cref="Farther"/> are deprecated upstream in favour of
/// <see cref="DeltaConstraint"/>.
/// </summary>
public enum TonePolarity
{
    /// <summary>A is darker than B.</summary>
    Darker,

    /// <summary>A is lighter than B.</summary>
    Lighter,

    /// <summary>A is nearer the surface tone than B.</summary>
    Nearer,

    /// <summary>A is farther from the surface tone than B.</summary>
    Farther,

    /// <summary>A is darker than B in light mode and lighter in dark mode (relative to the surface trend).</summary>
    RelativeDarker,

    /// <summary>A is lighter than B in light mode and darker in dark mode (relative to the surface trend).</summary>
    RelativeLighter,
}
