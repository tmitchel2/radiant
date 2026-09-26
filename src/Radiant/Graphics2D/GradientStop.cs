using System.Numerics;

namespace Radiant.Graphics2D;

/// <summary>A color at a position along a gradient.</summary>
/// <param name="Offset">Where along the gradient, 0 (start) to 1 (end).</param>
/// <param name="Color">The straight-alpha, linear-light color there.</param>
public readonly record struct GradientStop(float Offset, Vector4 Color);
