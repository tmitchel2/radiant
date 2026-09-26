namespace Radiant.Text;

/// <summary>A line chosen by line breaking, before its glyphs are placed.</summary>
/// <param name="Start">The first UTF-16 index.</param>
/// <param name="End">One past the last, trailing spaces and line break included.</param>
/// <param name="Ellipsis">The ellipsis to end the line with, if text was cut after it.</param>
/// <param name="EllipsisStyle">The style the ellipsis is set in.</param>
internal readonly record struct LineRange(int Start, int End, ShapedRun? Ellipsis = null, TextStyle? EllipsisStyle = null);
