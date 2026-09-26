// Copyright 2021 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;

namespace Radiant.MaterialColor;

/// <summary>Utility methods for mathematical operations.</summary>
public static class MathUtils
{
    /// <summary>The signum function: 1 if <paramref name="num"/> &gt; 0, -1 if &lt; 0, 0 if 0.</summary>
    public static int Signum(double num)
    {
        if (num < 0)
        {
            return -1;
        }
        else if (num == 0)
        {
            return 0;
        }
        else
        {
            return 1;
        }
    }

    /// <summary>Linear interpolation: <paramref name="start"/> at 0, <paramref name="stop"/> at 1.</summary>
    public static double Lerp(double start, double stop, double amount) =>
        (1.0 - amount) * start + amount * stop;

    /// <summary>Clamps an integer between two integers.</summary>
    public static int ClampInt(int min, int max, int input)
    {
        if (input < min)
        {
            return min;
        }
        else if (input > max)
        {
            return max;
        }

        return input;
    }

    /// <summary>Clamps a floating-point number between two floating-point numbers.</summary>
    public static double ClampDouble(double min, double max, double input)
    {
        if (input < min)
        {
            return min;
        }
        else if (input > max)
        {
            return max;
        }

        return input;
    }

    /// <summary>A degree measure between 0 (inclusive) and 360 (exclusive).</summary>
    public static int SanitizeDegreesInt(int degrees)
    {
        degrees %= 360;
        if (degrees < 0)
        {
            degrees += 360;
        }
        return degrees;
    }

    /// <summary>A degree measure between 0.0 (inclusive) and 360.0 (exclusive).</summary>
    public static double SanitizeDegreesDouble(double degrees)
    {
        degrees %= 360.0;
        if (degrees < 0)
        {
            degrees += 360.0;
        }
        return degrees;
    }

    /// <summary>
    /// Sign of direction change needed to travel from one angle to another: -1 if decreasing
    /// <paramref name="from"/> is the shortest way to <paramref name="to"/>, 1 if increasing is (or if
    /// the angles are 180 degrees apart, when both are equally short).
    /// </summary>
    public static double RotationDirection(double from, double to)
    {
        var increasingDifference = SanitizeDegreesDouble(to - from);
        return increasingDifference <= 180.0 ? 1.0 : -1.0;
    }

    /// <summary>Distance of two points on a circle, represented using degrees.</summary>
    public static double DifferenceDegrees(double a, double b) =>
        180.0 - Math.Abs(Math.Abs(a - b) - 180.0);

    /// <summary>Multiplies a 1x3 row vector with a 3x3 matrix.</summary>
    public static double[] MatrixMultiply(double[] row, double[][] matrix)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(matrix);
        var a = row[0] * matrix[0][0] + row[1] * matrix[0][1] + row[2] * matrix[0][2];
        var b = row[0] * matrix[1][0] + row[1] * matrix[1][1] + row[2] * matrix[1][2];
        var c = row[0] * matrix[2][0] + row[1] * matrix[2][1] + row[2] * matrix[2][2];
        return [a, b, c];
    }

    /// <summary>
    /// Rounds to the nearest integer, halves towards positive infinity: JavaScript's and Java's
    /// <c>Math.round</c>. <see cref="Math.Round(double)"/> rounds halves to even, and
    /// <see cref="MidpointRounding.AwayFromZero"/> rounds -2.5 to -3, so neither reproduces upstream's
    /// results; every rounding in this port goes through here.
    /// </summary>
    public static double Round(double value)
    {
        var floor = Math.Floor(value);
        // Comparing the fraction rather than computing Floor(value + 0.5) keeps 0.49999999999999994
        // at 0: adding 0.5 to it rounds up to 1.0 before the floor is taken.
        return value - floor >= 0.5 ? floor + 1.0 : floor;
    }
}
