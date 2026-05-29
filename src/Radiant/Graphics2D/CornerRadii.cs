namespace Radiant.Graphics2D
{
    /// <summary>
    /// Per-corner rounding radii for a rounded rectangle (CSS <c>border-radius</c> order:
    /// top-left, top-right, bottom-right, bottom-left). Use <see cref="All"/> for a uniform radius.
    /// </summary>
    /// <param name="TopLeft">Top-left corner radius.</param>
    /// <param name="TopRight">Top-right corner radius.</param>
    /// <param name="BottomRight">Bottom-right corner radius.</param>
    /// <param name="BottomLeft">Bottom-left corner radius.</param>
    public readonly record struct CornerRadii(float TopLeft, float TopRight, float BottomRight, float BottomLeft)
    {
        /// <summary>The same radius on all four corners.</summary>
        public static CornerRadii All(float radius) => new(radius, radius, radius, radius);

        /// <summary>Round only the top two corners (e.g. tabs / card headers).</summary>
        public static CornerRadii Top(float radius) => new(radius, radius, 0f, 0f);

        /// <summary>Round only the bottom two corners.</summary>
        public static CornerRadii Bottom(float radius) => new(0f, 0f, radius, radius);
    }
}
