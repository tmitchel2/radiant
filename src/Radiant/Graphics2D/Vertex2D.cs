using System.Numerics;
using System.Runtime.InteropServices;

namespace Radiant.Graphics2D
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex2D
    {
        public Vector2 Position;  // 8 bytes
        public Vector4 Color;     // 16 bytes

        public Vertex2D(Vector2 position, Vector4 color)
        {
            Position = position;
            Color = color;
        }

        public Vertex2D(float x, float y, float r, float g, float b, float a = 1.0f)
            : this(new Vector2(x, y), new Vector4(r, g, b, a))
        {
        }
    }
}
