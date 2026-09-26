// The pipeline follows msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8: main.cpp's shape
// preparation and core/msdfgen.cpp's generateMSDF with its default configuration.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>
/// Generates multi-channel signed distance fields of glyphs at runtime, with a C# port of
/// msdfgen's core. One field, generated once at a moderate size, draws a glyph sharply at any
/// size, rotation or zoom, which is what text that turns or scales needs.
/// <para>
/// The steps are msdfgen's defaults, with one addition: contours that cross (each other or
/// themselves), as variable fonts' do, are first replaced by the outline of the area they fill
/// (<see cref="OverlapResolver"/>; msdfgen does this with Skia).
/// Then the outline is normalized, turned outward if it is inside out, and its edges coloured so
/// that the edges at every corner share at most one channel (msdfgen's angle threshold of 3,
/// which makes any join turning by more than about 8° a corner). Each texel gets a perpendicular
/// distance per channel, combined across contours so that overlapping contours leave no seams,
/// and finally the texels that would cause interpolation artifacts are made single-channel,
/// protecting corners and edges.
/// </para>
/// </summary>
public static class MsdfGenerator
{
    /// <summary>Generates a glyph's field.</summary>
    /// <param name="outline">The outline, in font units with y up.</param>
    /// <param name="scale">Pixels per font unit: the size in pixels over the font's units per em.</param>
    /// <param name="range">
    /// The distance range in pixels: distances out to half this on either side of the outline are
    /// stored, and the field has that much margin around the glyph. Magnified or minified, the
    /// field must still span at least a pixel or two on screen, so 4 to 6 is typical.
    /// </param>
    public static MsdfBitmap Generate(GlyphOutline outline, float scale, float range)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(range);
        if (outline.IsEmpty)
        {
            return MsdfBitmap.Empty;
        }
        var shape = PrepareShape(outline);
        if (shape.Contours.Count == 0)
        {
            return MsdfBitmap.Empty;
        }

        // The field covers the shape and half the range around it, on whole pixels from the pen.
        var (l, b, r, t) = shape.GetBounds();
        var pad = range / 2.0;
        var left = (int)Math.Floor(l * scale - pad);
        var right = (int)Math.Ceiling(r * scale + pad);
        var top = (int)Math.Floor(-t * scale - pad);
        var bottom = (int)Math.Ceiling(-b * scale + pad);
        var width = right - left;
        var height = bottom - top;
        if (width <= 0 || height <= 0)
        {
            return MsdfBitmap.Empty;
        }

        // Texel (x, y), with row 0 at the bottom as msdfgen has it, is at pixel (left + x, bottom - 1 - y).
        var transformation = SdfTransformation.Symmetrical(
            new Vector2d(scale, scale), new Vector2d(-left / (double)scale, bottom / (double)scale), range / (double)scale);
        var field = GenerateField(shape, width, height, transformation);

        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var row = (height - 1 - y) * width * 4;
            for (var x = 0; x < width; x++)
            {
                var texel = (y * width + x) * 3;
                var pixel = row + x * 4;
                pixels[pixel] = ToByte(field[texel]);
                pixels[pixel + 1] = ToByte(field[texel + 1]);
                pixels[pixel + 2] = ToByte(field[texel + 2]);
                pixels[pixel + 3] = 255;
            }
        }
        return new MsdfBitmap(left, top, width, height, range, pixels);
    }

    /// <summary>
    /// Builds and prepares the shape as msdfgen does before generating: closed, normalized,
    /// oriented so distances are positive inside, and coloured. First, unless
    /// <paramref name="resolveOverlaps"/> is false (to compare with msdfgen built without Skia,
    /// which doesn't), contours that cross are replaced by the outline of what they fill.
    /// </summary>
    internal static Shape PrepareShape(GlyphOutline outline, bool resolveOverlaps = true)
    {
        var shape = Shape.FromOutline(outline);
        if (resolveOverlaps)
        {
            OverlapResolver.Resolve(shape);
        }
        shape.Normalize();
        shape.OrientOutward();
        EdgeColoring.Simple(shape, EdgeColoring.DefaultAngleThreshold);
        return shape;
    }

    /// <summary>
    /// msdfgen's <c>generateMSDF</c> with overlap support and the default error correction: three
    /// floats a texel, rows from the bottom, where ½ is the outline.
    /// </summary>
    internal static float[] GenerateField(Shape shape, int width, int height, SdfTransformation transformation)
    {
        var field = new float[width * height * 3];
        var distanceFinder = new ShapeDistanceFinder<MultiDistanceSelector, MultiDistance>(shape);
        var xDirection = 1;
        for (var y = 0; y < height; ++y)
        {
            var x = xDirection < 0 ? width - 1 : 0;
            for (var col = 0; col < width; ++col)
            {
                var p = transformation.Unproject(new Vector2d(x + .5, y + .5));
                var distance = distanceFinder.Distance(p);
                var texel = (y * width + x) * 3;
                field[texel] = (float)transformation.MapDistance(distance.R);
                field[texel + 1] = (float)transformation.MapDistance(distance.G);
                field[texel + 2] = (float)transformation.MapDistance(distance.B);
                x += xDirection;
            }
            xDirection = -xDirection;
        }
        MsdfErrorCorrection.Apply(field, width, height, shape, transformation);
        return field;
    }

    /// <summary>msdfgen's <c>pixelFloatToByte</c>.</summary>
    private static byte ToByte(float x) => (byte)(255 - (int)(255.5f - 255f * Math.Clamp(x, 0f, 1f)));
}
