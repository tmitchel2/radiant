using System.Numerics;

namespace Radiant.UI.Core;

/// <summary>A shadow cast by a <see cref="Box"/>, as CSS's <c>box-shadow</c>: a blurred copy of its shape.</summary>
/// <param name="Offset">How far the shadow is moved, typically down.</param>
/// <param name="Blur">How soft it is, in pixels.</param>
/// <param name="Spread">How much larger than the box it is, on every side; may be negative.</param>
/// <param name="Color">Its colour at full strength, linear and straight alpha.</param>
public readonly record struct BoxShadow(Vector2 Offset, float Blur, float Spread, Vector4 Color);
