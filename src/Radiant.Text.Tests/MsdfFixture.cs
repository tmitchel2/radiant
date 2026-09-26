using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Radiant.Text.Tests;

/// <summary>
/// A shape and the MSDF upstream msdfgen generates for it (MsdfFixtures, made by
/// tools/msdf-fixtures/generate.sh). A .shape file is the scale and range, then one path command
/// a line; the .msdf.gz is the frame (width, height, left, top, scale, range), then the field's
/// rows from the bottom, three floats a texel.
/// </summary>
internal sealed class MsdfFixture
{
    private MsdfFixture(GlyphOutline outline, int width, int height, int left, int top, double scale, double range, float[] field)
    {
        Outline = outline;
        Width = width;
        Height = height;
        Left = left;
        Top = top;
        Scale = scale;
        Range = range;
        Field = field;
    }

    public GlyphOutline Outline { get; }

    public int Width { get; }

    public int Height { get; }

    public int Left { get; }

    public int Top { get; }

    public double Scale { get; }

    public double Range { get; }

    /// <summary>msdfgen's field: three floats a texel, rows from the bottom.</summary>
    public float[] Field { get; }

    public static MsdfFixture Load(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "MsdfFixtures");
        var outline = ReadShape(Path.Combine(directory, name + ".shape"));

        using var file = File.OpenRead(Path.Combine(directory, name + ".msdf.gz"));
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.ASCII);
        var header = reader.ReadLine()!.Split(' ');
        int width = Int(header[0]), height = Int(header[1]);
        var field = new float[width * height * 3];
        var i = 0;
        while (reader.ReadLine() is { } line)
        {
            foreach (var value in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                // A channel no edge is coloured with is infinitely far away: printf writes "-inf".
                field[i++] = value switch
                {
                    "-inf" => float.NegativeInfinity,
                    "inf" => float.PositiveInfinity,
                    _ => float.Parse(value, CultureInfo.InvariantCulture),
                };
            }
        }
        if (i != field.Length)
        {
            throw new InvalidDataException($"{name}: {i} values for {field.Length}");
        }
        return new MsdfFixture(outline, width, height, Int(header[2]), Int(header[3]), Double(header[4]), Double(header[5]), field);
    }

    /// <summary>A shape file's path as an outline.</summary>
    public static GlyphOutline ReadShape(string path)
    {
        var builder = new GlyphOutline.Builder();
        var sawHeader = false;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }
            if (!sawHeader)
            {
                sawHeader = true;
                continue;
            }
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            float F(int index) => (float)Double(parts[index]);
            switch (parts[0])
            {
                case "M":
                    builder.MoveTo(F(1), F(2));
                    break;
                case "L":
                    builder.LineTo(F(1), F(2));
                    break;
                case "Q":
                    builder.QuadTo(F(1), F(2), F(3), F(4));
                    break;
                case "C":
                    builder.CubicTo(F(1), F(2), F(3), F(4), F(5), F(6));
                    break;
                case "Z":
                    builder.Close();
                    break;
            }
        }
        return builder.Build();
    }

    private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    private static double Double(string s) => double.Parse(s, CultureInfo.InvariantCulture);
}
