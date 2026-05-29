using System.Numerics;
using System.Runtime.InteropServices;

namespace Radiant.Graphics2D
{
    /// <summary>
    /// Vertex for the batched SDF-shape pipeline. A single pipeline draws every analytic 2D shape
    /// (rounded rectangle, disc, ring) by carrying a shape kind plus its parameters flat across all
    /// six quad vertices; the fragment shader dispatches on <see cref="Misc"/>.w and evaluates the
    /// matching signed-distance function for crisp, scale-independent edges with 1px analytic AA.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SdfShapeVertex2D
    {
        /// <summary>Quad corner position in screen space (expanded by an AA pad beyond the shape).</summary>
        public Vector2 Position;

        /// <summary>This corner's offset from the shape centre, in pixels (interpolated to fragments).</summary>
        public Vector2 LocalPos;

        /// <summary>Fill colour.</summary>
        public Vector4 Color;

        /// <summary>Border colour.</summary>
        public Vector4 BorderColor;

        /// <summary>(halfWidth, halfHeight, borderWidth, shapeKind) — <see cref="SdfShapeKind"/> as a float.</summary>
        public Vector4 Misc;

        /// <summary>
        /// Shape parameters. Rounded rect: per-corner radii (TopLeft, TopRight, BottomRight, BottomLeft).
        /// Circle/ring: (outerRadius, innerRadius, _, _) — innerRadius 0 ⇒ filled disc.
        /// </summary>
        public Vector4 Params;

        public SdfShapeVertex2D(Vector2 position, Vector2 localPos, Vector4 color, Vector4 borderColor, Vector4 misc, Vector4 @params)
        {
            Position = position;
            LocalPos = localPos;
            Color = color;
            BorderColor = borderColor;
            Misc = misc;
            Params = @params;
        }
    }
}
