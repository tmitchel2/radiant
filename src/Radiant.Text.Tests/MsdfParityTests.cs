using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Text.Msdf;

namespace Radiant.Text.Tests;

/// <summary>
/// The port against upstream msdfgen: fields generated for the same shapes by msdfgen at the
/// pinned commit (tools/msdf-fixtures) and by <see cref="MsdfGenerator"/> agree texel by texel.
/// Upstream is built without Skia, so it doesn't resolve crossing contours; the port is compared
/// with that step off, and with it on only for shapes it leaves alone.
/// </summary>
[TestClass]
public class MsdfParityTests
{
    [TestMethod]
    [DataRow("square")]
    [DataRow("overlap")]
    [DataRow("ring-quadratic")]
    [DataRow("circle-cubic")]
    [DataRow("teardrop")]
    [DataRow("cusp")]
    [DataRow("inter-A-900")]
    [DataRow("inter-g-400")]
    [DataRow("inter-dollar-900")]
    [DataRow("inter-at-400")]
    [DataRow("inter-t-800")]
    public void TheFieldMatchesMsdfgen(string name)
    {
        var fixture = MsdfFixture.Load(name);
        var shape = MsdfGenerator.PrepareShape(fixture.Outline, resolveOverlaps: false);
        var transformation = SdfTransformation.Symmetrical(
            new Vector2d(fixture.Scale, fixture.Scale),
            new Vector2d(-fixture.Left / fixture.Scale, (fixture.Top + fixture.Height) / fixture.Scale),
            fixture.Range / fixture.Scale);

        var field = MsdfGenerator.GenerateField(shape, fixture.Width, fixture.Height, transformation);

        var worst = 0f;
        var worstAt = -1;
        for (var i = 0; i < field.Length; i++)
        {
            var difference = field[i] == fixture.Field[i] ? 0f : MathF.Abs(field[i] - fixture.Field[i]);
            if (difference > worst)
            {
                worst = difference;
                worstAt = i;
            }
        }
        // The same double-precision operations in the same order: on macOS, where .NET and the C++
        // build share a maths library, the fields are bit-identical. The tolerance allows for other
        // platforms' acos, cos and pow rounding differently in the last place.
        Assert.IsTrue(worst < 1e-5f, $"{name}: largest difference {worst} at texel {worstAt / 3}, channel {worstAt % 3}");
    }

    [TestMethod]
    [DataRow("square")]
    [DataRow("ring-quadratic")]
    [DataRow("circle-cubic")]
    [DataRow("teardrop")]
    [DataRow("cusp")]
    [DataRow("inter-g-400")]
    [DataRow("inter-at-400")]
    public void TheBitmapOfAShapeWithoutCrossingsIsMsdfgensFieldInBytesTopDown(string name)
    {
        // Contours that don't cross are left as they are, so the public API gives msdfgen's
        // field, as bytes, top row first (to within a byte, as some scales are rounded to float).
        var fixture = MsdfFixture.Load(name);

        var bitmap = MsdfGenerator.Generate(fixture.Outline, (float)fixture.Scale, (float)fixture.Range);

        Assert.AreEqual((fixture.Left, fixture.Top, fixture.Width, fixture.Height), (bitmap.Left, bitmap.Top, bitmap.Width, bitmap.Height));
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var texel = ((bitmap.Height - 1 - y) * bitmap.Width + x) * 3;
                var pixel = (y * bitmap.Width + x) * 4;
                for (var c = 0; c < 3; c++)
                {
                    var expected = (int)MathF.Round(255f * Math.Clamp(fixture.Field[texel + c], 0f, 1f));
                    Assert.AreEqual(expected, bitmap.Pixels[pixel + c], 1, $"{name} ({x}, {y}) channel {c}");
                }
                Assert.AreEqual(255, bitmap.Pixels[pixel + 3]);
            }
        }
    }
}
