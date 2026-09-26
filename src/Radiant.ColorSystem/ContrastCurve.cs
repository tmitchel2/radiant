// Copyright 2023 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

namespace Radiant.ColorSystem;

/// <summary>
/// A value that changes with the contrast level: usually the contrast ratio a dynamic color needs
/// against its background. The four values are for contrast levels -1.0, 0.0, 0.5 and 1.0, and it
/// interpolates linearly between them.
/// </summary>
/// <param name="Low">Value for contrast level -1.0.</param>
/// <param name="Normal">Value for contrast level 0.0.</param>
/// <param name="Medium">Value for contrast level 0.5.</param>
/// <param name="High">Value for contrast level 1.0.</param>
public sealed record ContrastCurve(double Low, double Normal, double Medium, double High)
{
    /// <summary>
    /// The value at a contrast level: 0.0 is the default, -1.0 the lowest, 1.0 the highest. For
    /// contrast ratios, a number between 1.0 and 21.0.
    /// </summary>
    public double Get(double contrastLevel)
    {
        if (contrastLevel <= -1.0)
        {
            return Low;
        }
        else if (contrastLevel < 0.0)
        {
            return MathUtils.Lerp(Low, Normal, (contrastLevel - -1) / 1);
        }
        else if (contrastLevel < 0.5)
        {
            return MathUtils.Lerp(Normal, Medium, (contrastLevel - 0) / 0.5);
        }
        else if (contrastLevel < 1.0)
        {
            return MathUtils.Lerp(Medium, High, (contrastLevel - 0.5) / 0.5);
        }
        else
        {
            return High;
        }
    }
}
