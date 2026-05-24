using System.Numerics;
using System.Runtime.InteropServices;

namespace Radiant.Graphics2D
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MsdfVertex2D
    {
        public Vector2 Position;
        public Vector4 Color;
        public Vector2 TexCoord;

        public MsdfVertex2D(Vector2 position, Vector4 color, Vector2 texCoord)
        {
            Position = position;
            Color = color;
            TexCoord = texCoord;
        }
    }
}
