namespace Radiant.Text;

/// <summary>A drawing command in a <see cref="GlyphOutline"/>.</summary>
public enum PathVerb : byte
{
    /// <summary>Starts a contour at one point.</summary>
    MoveTo,

    /// <summary>A straight line to one point.</summary>
    LineTo,

    /// <summary>A quadratic Bézier: a control point, then the end point.</summary>
    QuadTo,

    /// <summary>A cubic Bézier: two control points, then the end point.</summary>
    CubicTo,

    /// <summary>Closes the contour back to its start.</summary>
    Close,
}
