using System.Collections.Generic;
using System.Numerics;

namespace Radiant.Text;

/// <summary>
/// A glyph's shape as closed contours of lines and Bézier curves, in font units with y up (the
/// font's own coordinate system; the baseline is y = 0 and the pen origin x = 0).
/// </summary>
public sealed class GlyphOutline
{
    /// <summary>An outline with no contours, for glyphs such as a space.</summary>
    public static GlyphOutline Empty { get; } = new([], []);

    internal GlyphOutline(PathVerb[] verbs, Vector2[] points)
    {
        Verbs = verbs;
        Points = points;
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var p in points)
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        Bounds = points.Length == 0 ? default : (min, max);
    }

    /// <summary>The commands, in order.</summary>
    public IReadOnlyList<PathVerb> Verbs { get; }

    /// <summary>The points the commands consume: one for move and line, two for quad, three for cubic.</summary>
    public IReadOnlyList<Vector2> Points { get; }

    /// <summary>The smallest box around every point (control points included), in font units.</summary>
    public (Vector2 Min, Vector2 Max) Bounds { get; }

    /// <summary>Whether the glyph draws anything.</summary>
    public bool IsEmpty => Verbs.Count == 0;

    /// <summary>The number of contours.</summary>
    public int ContourCount
    {
        get
        {
            var count = 0;
            foreach (var verb in Verbs)
            {
                if (verb == PathVerb.MoveTo)
                {
                    count++;
                }
            }
            return count;
        }
    }

    internal sealed class Builder
    {
        private readonly List<PathVerb> _verbs = [];
        private readonly List<Vector2> _points = [];

        public void MoveTo(float x, float y)
        {
            _verbs.Add(PathVerb.MoveTo);
            _points.Add(new Vector2(x, y));
        }

        public void LineTo(float x, float y)
        {
            _verbs.Add(PathVerb.LineTo);
            _points.Add(new Vector2(x, y));
        }

        public void QuadTo(float cx, float cy, float x, float y)
        {
            _verbs.Add(PathVerb.QuadTo);
            _points.Add(new Vector2(cx, cy));
            _points.Add(new Vector2(x, y));
        }

        public void CubicTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
        {
            _verbs.Add(PathVerb.CubicTo);
            _points.Add(new Vector2(c1x, c1y));
            _points.Add(new Vector2(c2x, c2y));
            _points.Add(new Vector2(x, y));
        }

        public void Close() => _verbs.Add(PathVerb.Close);

        public GlyphOutline Build() => _verbs.Count == 0 ? Empty : new GlyphOutline([.. _verbs], [.. _points]);
    }
}
