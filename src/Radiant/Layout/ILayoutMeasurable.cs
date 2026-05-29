using System.Numerics;

namespace Radiant.Layout;

/// <summary>
/// A leaf <see cref="Radiant.UI.UIElement"/> whose intrinsic size comes from its content (e.g. a
/// text label measured against its font). <see cref="YogaLayoutEngine"/> registers a Yoga measure
/// callback that defers to <see cref="MeasureContent"/>, so the widget keeps its font/text private.
/// </summary>
public interface ILayoutMeasurable
{
    /// <summary>
    /// Returns the content's natural size. <paramref name="availableWidth"/> is the width Yoga is
    /// offering (may be <see cref="float.NaN"/> when unconstrained); single-line implementations
    /// may ignore it.
    /// </summary>
    Vector2 MeasureContent(float availableWidth);
}
