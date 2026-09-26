using System.Numerics;

namespace Radiant.Graphics2D;

/// <summary>
/// Turns the straight-alpha background colour a caller passes into a render-pass clear value.
/// Attachments hold premultiplied alpha (see <see cref="Renderer2D"/>'s blend state), so a
/// translucent clear is premultiplied; an opaque one comes out unchanged.
/// </summary>
internal static class ClearColor
{
    public static Silk.NET.WebGPU.Color FromStraightAlpha(Vector4 color)
    {
        var premultiplied = Color.FromVector4(color).ToPremultiplied();
        return new Silk.NET.WebGPU.Color
        {
            R = premultiplied.X,
            G = premultiplied.Y,
            B = premultiplied.Z,
            A = premultiplied.W,
        };
    }
}
