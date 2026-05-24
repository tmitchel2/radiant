using System.Numerics;
using SixLabors.Fonts;

namespace Radiant.MsdfBaker
{
    /// <summary>
    /// IGlyphRenderer implementation that captures a single glyph's outline
    /// into a Shape, in font-units (Y-up after we flip from SixLabors' Y-down
    /// path callbacks).
    /// </summary>
    public sealed class GlyphShapeBuilder : IGlyphRenderer
    {
        private Shape? _shape;
        private Contour? _current;
        private Vector2 _start;
        private Vector2 _last;
        private FontRectangle _bounds;

        public Shape Result => _shape ?? new Shape();
        public FontRectangle Bounds => _bounds;

        public void BeginText(in FontRectangle bounds)
        {
        }

        public void EndText()
        {
        }

        public bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters parameters)
        {
            _shape = new Shape { InverseYAxis = false };
            _bounds = bounds;
            return true;
        }

        public void EndGlyph()
        {
            _current = null;
        }

        public void BeginFigure()
        {
            _current = new Contour();
            _shape!.Contours.Add(_current);
        }

        public void MoveTo(Vector2 point)
        {
            _start = point;
            _last = point;
        }

        public void LineTo(Vector2 point)
        {
            _current!.Edges.Add(new LinearSegment(_last, point));
            _last = point;
        }

        public void QuadraticBezierTo(Vector2 secondControlPoint, Vector2 point)
        {
            _current!.Edges.Add(new QuadraticSegment(_last, secondControlPoint, point));
            _last = point;
        }

        public void CubicBezierTo(Vector2 secondControlPoint, Vector2 thirdControlPoint, Vector2 point)
        {
            _current!.Edges.Add(new CubicSegment(_last, secondControlPoint, thirdControlPoint, point));
            _last = point;
        }

        public void EndFigure()
        {
            if (_current is null) return;
            if (_last != _start && _current.Edges.Count > 0)
            {
                _current.Edges.Add(new LinearSegment(_last, _start));
                _last = _start;
            }
        }

        public TextDecorations EnabledDecorations() => TextDecorations.None;

        public void SetDecoration(TextDecorations textDecorations, Vector2 start, Vector2 end, float thickness)
        {
        }
    }
}
