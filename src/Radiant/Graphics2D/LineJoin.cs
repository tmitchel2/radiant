namespace Radiant.Graphics2D
{
    /// <summary>How a stroked path fills the outside of a corner.</summary>
    /// <remarks>
    /// Without one, a chain of quads leaves a wedge missing on the outside of every bend, because
    /// each quad ends square at the shared point and the two are not the same rectangle.
    /// </remarks>
    public enum LineJoin
    {
        /// <summary>Extend both outer edges until they meet. Falls back to a bevel past the limit.</summary>
        Miter,

        /// <summary>An arc of the stroke's own radius. Cannot spike, whatever the angle.</summary>
        Round,

        /// <summary>A single triangle across the gap: the flat cut of a miter.</summary>
        Bevel,
    }
}
