// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

namespace Radiant.MaterialColor;

/// <summary>How a <see cref="ToneDeltaPair"/>'s tone distance is enforced.</summary>
public enum DeltaConstraint
{
    /// <summary>Exactly the delta apart.</summary>
    Exact,

    /// <summary>At most the delta apart.</summary>
    Nearer,

    /// <summary>At least the delta apart.</summary>
    Farther,
}
