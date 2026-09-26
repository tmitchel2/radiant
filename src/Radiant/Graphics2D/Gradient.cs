using System;
using System.Collections.Generic;
using System.Numerics;

namespace Radiant.Graphics2D;

/// <summary>
/// A linear or radial gradient fill, with two to four color stops. Points are in the coordinates the
/// shape is drawn in, and the gradient transforms with its shape.
/// </summary>
public sealed record Gradient
{
    /// <summary>The most stops a gradient can have (they travel with the shape's vertices).</summary>
    public const int MaxStops = 4;

    private Gradient(GradientKind kind, Vector2 start, Vector2 end, float radius, IReadOnlyList<GradientStop> stops, GradientInterpolation interpolation)
    {
        if (stops.Count is < 2 or > MaxStops)
        {
            throw new ArgumentException($"A gradient takes 2 to {MaxStops} stops; got {stops.Count}.", nameof(stops));
        }
        for (var i = 1; i < stops.Count; i++)
        {
            if (stops[i].Offset < stops[i - 1].Offset)
            {
                throw new ArgumentException("Gradient stops must be in increasing offset order.", nameof(stops));
            }
        }
        Kind = kind;
        Start = start;
        End = end;
        Radius = radius;
        Stops = stops;
        Interpolation = interpolation;
    }

    /// <summary>Linear or radial.</summary>
    public GradientKind Kind { get; }

    /// <summary>Where offset 0 is: the start of a linear gradient, or the center of a radial one.</summary>
    public Vector2 Start { get; }

    /// <summary>Where offset 1 is, for a linear gradient.</summary>
    public Vector2 End { get; }

    /// <summary>The distance from the center at which a radial gradient reaches offset 1.</summary>
    public float Radius { get; }

    /// <summary>The stops, in increasing offset order. Before the first and after the last, the end colors hold.</summary>
    public IReadOnlyList<GradientStop> Stops { get; }

    /// <summary>The color space the stops are blended in.</summary>
    public GradientInterpolation Interpolation { get; }

    /// <summary>A gradient along the line from <paramref name="start"/> to <paramref name="end"/>.</summary>
    public static Gradient Linear(Vector2 start, Vector2 end, IReadOnlyList<GradientStop> stops,
        GradientInterpolation interpolation = GradientInterpolation.Srgb)
    {
        ArgumentNullException.ThrowIfNull(stops);
        return new Gradient(GradientKind.Linear, start, end, 0f, stops, interpolation);
    }

    /// <summary>A two-color gradient along the line from <paramref name="start"/> to <paramref name="end"/>.</summary>
    public static Gradient Linear(Vector2 start, Vector2 end, Vector4 from, Vector4 to,
        GradientInterpolation interpolation = GradientInterpolation.Srgb) =>
        Linear(start, end, [new GradientStop(0f, from), new GradientStop(1f, to)], interpolation);

    /// <summary>A gradient by distance from <paramref name="center"/>, reaching offset 1 at <paramref name="radius"/>.</summary>
    public static Gradient Radial(Vector2 center, float radius, IReadOnlyList<GradientStop> stops,
        GradientInterpolation interpolation = GradientInterpolation.Srgb)
    {
        ArgumentNullException.ThrowIfNull(stops);
        return new Gradient(GradientKind.Radial, center, center, radius, stops, interpolation);
    }

    /// <summary>A two-color gradient by distance from <paramref name="center"/>.</summary>
    public static Gradient Radial(Vector2 center, float radius, Vector4 from, Vector4 to,
        GradientInterpolation interpolation = GradientInterpolation.Srgb) =>
        Radial(center, radius, [new GradientStop(0f, from), new GradientStop(1f, to)], interpolation);
}
