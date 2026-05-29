using System.Numerics;
using System.Runtime.InteropServices;

namespace Radiant.Graphics2D
{
    /// <summary>
    /// Vertex for the SDF rounded-rectangle pipeline. Carries the fill and border colours plus the
    /// per-instance box description (half-size, corner radius, border width) flat across all six quad
    /// vertices, and the fragment's offset from the box centre (<see cref="LocalPos"/>) which the
    /// fragment shader feeds into a rounded-box signed-distance function for crisp, scale-independent
    /// corners and 1px analytic anti-aliasing.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RoundedRectVertex2D
    {
        /// <summary>Quad corner position in screen space (expanded by an AA pad beyond the box).</summary>
        public Vector2 Position;

        /// <summary>Fill colour.</summary>
        public Vector4 Color;

        /// <summary>Border colour.</summary>
        public Vector4 BorderColor;

        /// <summary>This corner's offset from the box centre, in pixels (interpolated to fragments).</summary>
        public Vector2 LocalPos;

        /// <summary>Box parameters: (halfWidth, halfHeight, cornerRadius, borderWidth).</summary>
        public Vector4 Params;

        public RoundedRectVertex2D(Vector2 position, Vector4 color, Vector4 borderColor, Vector2 localPos, Vector4 @params)
        {
            Position = position;
            Color = color;
            BorderColor = borderColor;
            LocalPos = localPos;
            Params = @params;
        }
    }
}
