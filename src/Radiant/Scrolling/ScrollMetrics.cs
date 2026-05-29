using System.Numerics;

namespace Radiant.Scrolling;

/// <summary>
/// Scroll position/extent snapshot delivered with scroll lifecycle callbacks,
/// mirroring React Native's <c>NativeScrollEvent</c> payload.
/// </summary>
public readonly record struct ScrollMetrics(
    Vector2 ContentOffset,
    Vector2 ContentSize,
    Vector2 LayoutMeasurement,
    Vector2 Velocity);
