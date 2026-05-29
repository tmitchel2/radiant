namespace Radiant.Graphics2D
{
    /// <summary>
    /// Which signed-distance function the SDF-shape pipeline evaluates for an instance. The numeric
    /// values are passed to the shader (as a float in the vertex), so their order is load-bearing.
    /// </summary>
    public enum SdfShapeKind
    {
        /// <summary>Rounded rectangle with per-corner radii.</summary>
        RoundedRect = 0,

        /// <summary>Circle / disc / ring (annulus) — by outer and inner radius.</summary>
        Circle = 1,
    }
}
