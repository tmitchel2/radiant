using System;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Radiant.Text;

namespace Radiant.Tests.Graphics2D;

/// <summary>
/// Laid-out text drawn with MSDF glyphs generated at runtime, and the hybrid of coverage for small
/// upright text and MSDF for the rest.
/// </summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class RendererMsdfTextGpuTests
{
    private static readonly Vector4 White = Vector4.One;
    private static readonly TextStyle Black = new() { Size = 16, Color = new Vector4(0, 0, 0, 1) };
    private static readonly float[] Sizes = [20f, 37.5f, 64f, 120f];

    private static int Ink(GpuFrame frame, byte[] pixels, int x0 = 0, int y0 = 0, int x1 = int.MaxValue, int y1 = int.MaxValue)
    {
        // Darkness summed over a region of a white frame: how much text is there.
        var ink = 0;
        for (var y = Math.Max(0, y0); y < Math.Min(frame.Height, y1); y++)
        {
            for (var x = Math.Max(0, x0); x < Math.Min(frame.Width, x1); x++)
            {
                ink += 255 - frame.PixelAt(pixels, x, y).G;
            }
        }
        return ink;
    }

    private static int SolidPixels(byte[] pixels)
    {
        var solid = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            solid += pixels[i + 1] == 0 ? 1 : 0;
        }
        return solid;
    }

    [TestMethod]
    public void MsdfTextDrawsInsideItsParagraphBox()
    {
        using var frame = GpuFrame.CreateOrSkip(200, 64);
        frame.Renderer.TextRendering = TextRendering.Msdf;
        var paragraph = Paragraph.Layout("Hello", Black with { Size = 32 });

        var pixels = frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(10, 10)));

        var inside = Ink(frame, pixels, 10, 10, 10 + (int)MathF.Ceiling(paragraph.Width), 10 + (int)MathF.Ceiling(paragraph.Height));
        Assert.IsTrue(inside > 255 * 100, $"ink {inside}");
        Assert.AreEqual(inside, Ink(frame, pixels), "no ink outside the paragraph");
        Assert.IsTrue(frame.Renderer.MsdfVertices.Count > 0, "drawn through the MSDF pipeline");
        Assert.AreEqual(0, frame.Renderer.CoverageVertices.Count);
    }

    [TestMethod]
    [DataRow(64f)]
    [DataRow(96f)]
    public void AtLargeSizesMsdfInkMatchesCoverageInk(float size)
    {
        using var frame = GpuFrame.CreateOrSkip(420, 140);
        var paragraph = Paragraph.Layout("Radiant&", Black with { Size = size });
        // Coverage blends its edges as if in gamma 1.8 by default; compare the shapes, in linear light.
        frame.Renderer.TextGamma = 1f;

        frame.Renderer.TextRendering = TextRendering.Coverage;
        var coverage = frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(8, 8)));
        frame.Renderer.TextRendering = TextRendering.Msdf;
        var msdf = frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(8, 8)));

        long difference = 0;
        for (var i = 0; i < coverage.Length; i += 4)
        {
            difference += Math.Abs(coverage[i + 1] - msdf[i + 1]);
        }
        var inkCoverage = Ink(frame, coverage);
        var inkMsdf = Ink(frame, msdf);
        // Where they differ at all is along the edges; over the ink, that is a few percent.
        Assert.IsTrue(difference < 0.06 * inkCoverage, $"differ by {difference} over {inkCoverage} of ink");
        Assert.AreEqual(1f, inkMsdf / (float)inkCoverage, 0.03f, $"ink {inkMsdf} vs {inkCoverage}");
    }

    [TestMethod]
    public void RotatedMsdfTextKeepsSolidStems()
    {
        using var frame = GpuFrame.CreateOrSkip(128, 128);
        var paragraph = Paragraph.Layout("lll", Black with { Size = 64 });
        void Draw(Renderer2D r)
        {
            r.PushTransform(Matrix3x2.CreateRotation(MathF.PI / 7f) * Matrix3x2.CreateTranslation(40, 10));
            r.DrawParagraph(paragraph, Vector2.Zero);
            r.PopTransform();
        }

        frame.Renderer.TextRendering = TextRendering.Msdf;
        var msdf = frame.Render(White, Draw);
        frame.Renderer.TextRendering = TextRendering.Coverage;
        frame.Renderer.TextGamma = 1f; // compare the shapes, in linear light
        var coverage = frame.Render(White, Draw);

        // Three stems about 6 pixels wide and 45 long, turned: well over a hundred fully covered
        // pixels each, as many as the coverage atlas's glyphs (rasterized for this size) have.
        var solid = SolidPixels(msdf);
        Assert.IsTrue(solid > 400, $"{solid} fully covered pixels");
        Assert.IsTrue(solid >= 0.95f * SolidPixels(coverage), $"{solid} vs coverage's {SolidPixels(coverage)}");
        Assert.AreEqual(1f, Ink(frame, msdf) / (float)Ink(frame, coverage), 0.05f, "the same glyphs, turned");
    }

    [TestMethod]
    public void HybridDrawsSmallUprightTextFromCoverageAndTheRestWithMsdf()
    {
        using var frame = GpuFrame.CreateOrSkip(200, 100);
        var renderer = frame.Renderer;
        renderer.TextRendering = TextRendering.Hybrid;
        var small = Paragraph.Layout("Hybrid", Black);
        var large = Paragraph.Layout("Hybrid", Black with { Size = 48 });

        (int Coverage, int Msdf) KindsFor(Action<Renderer2D> draw)
        {
            frame.Render(White, draw);
            return (renderer.CoverageVertices.Count, renderer.MsdfVertices.Count);
        }

        var smallKinds = KindsFor(r => r.DrawParagraph(small, new Vector2(4, 4)));
        Assert.IsTrue(smallKinds.Coverage > 0 && smallKinds.Msdf == 0, $"16 px upright: {smallKinds}");

        var largeKinds = KindsFor(r => r.DrawParagraph(large, new Vector2(4, 4)));
        Assert.IsTrue(largeKinds.Coverage == 0 && largeKinds.Msdf > 0, $"48 px: {largeKinds}");

        var rotatedKinds = KindsFor(r =>
        {
            r.PushTransform(Matrix3x2.CreateRotation(0.1f));
            r.DrawParagraph(small, new Vector2(4, 4));
            r.PopTransform();
        });
        Assert.IsTrue(rotatedKinds.Coverage == 0 && rotatedKinds.Msdf > 0, $"16 px rotated: {rotatedKinds}");

        // The threshold is in device pixels: 16 px scaled up two times is 32 on screen.
        var scaledKinds = KindsFor(r =>
        {
            r.PushTransform(Matrix3x2.CreateScale(2f));
            r.DrawParagraph(small, new Vector2(2, 2));
            r.PopTransform();
        });
        Assert.IsTrue(scaledKinds.Coverage == 0 && scaledKinds.Msdf > 0, $"16 px scaled 2x: {scaledKinds}");
    }

    [TestMethod]
    public void HybridCountsTheRetinaScaleTowardsTheThreshold()
    {
        using var frame = GpuFrame.CreateOrSkip(200, 100, pixelScale: 2f);
        frame.Renderer.TextRendering = TextRendering.Hybrid;
        var paragraph = Paragraph.Layout("Hybrid", Black);

        // 16 logical pixels are 32 device pixels, over the 24 px threshold.
        frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(4, 4)));

        Assert.AreEqual(0, frame.Renderer.CoverageVertices.Count);
        Assert.IsTrue(frame.Renderer.MsdfVertices.Count > 0);
    }

    [TestMethod]
    public void EachGlyphIsGeneratedOnceForEverySize()
    {
        using var frame = GpuFrame.CreateOrSkip(300, 200);
        frame.Renderer.TextRendering = TextRendering.Msdf;
        var paragraphs = Sizes.Select(size => Paragraph.Layout("aba", Black with { Size = size })).ToArray();

        frame.Render(White, r =>
        {
            foreach (var paragraph in paragraphs)
            {
                r.DrawParagraph(paragraph, Vector2.Zero);
            }
        });

        // Inter picks its optical size by text size up to 32 px, so 20 px text is another instance.
        var atlas = frame.Renderer.MsdfGlyphAtlas!;
        Assert.AreEqual(4, atlas.GlyphCount, "a and b at 20 px, and a and b at every size from 32 px up");
    }

    [TestMethod]
    public void TextTakesItsTintInLinearStraightAlpha()
    {
        using var frame = GpuFrame.CreateOrSkip(96, 96);
        frame.Renderer.TextRendering = TextRendering.Msdf;
        var paragraph = Paragraph.Layout("l", Black with { Size = 72 });

        var red = frame.Render(Vector4.Zero, r => r.DrawParagraph(paragraph, new Vector2(30, 0), Color.Parse("#ff0000")));
        var halfBlue = frame.Render(Vector4.Zero, r => r.DrawParagraph(paragraph, new Vector2(30, 0), Color.Parse("#0000ff80")));

        var (x, y) = (-1, -1);
        for (var i = 0; i < red.Length && x < 0; i += 4)
        {
            if (red[i + 3] == 255)
            {
                (x, y) = (i / 4 % frame.Width, i / 4 / frame.Width);
            }
        }
        Assert.IsTrue(x >= 0, "a fully covered pixel");
        Assert.AreEqual(((byte)255, (byte)0, (byte)0, (byte)255), frame.PixelAt(red, x, y));
        // Half-transparent blue over a transparent target: premultiplied, so the stored colour is
        // blue at half coverage.
        // blue at half coverage: 0.5 in linear light, which the sRGB target stores as 188.
        var (r, g, b, a) = frame.PixelAt(halfBlue, x, y);
        Assert.AreEqual((0, 0), (r, g));
        Assert.AreEqual(128, a, 1);
        Assert.AreEqual(188, b, 2);
    }

    [TestMethod]
    public void AFullAtlasIsEmptiedBetweenFramesAndTextStillDraws()
    {
        using var frame = GpuFrame.CreateOrSkip(256, 64);
        frame.Renderer.TextRendering = TextRendering.Msdf;
        // Pages of 128 texels hold a few glyphs each, so a few dozen fill the atlas.
        frame.Renderer.MsdfAtlasPageSize = 128;
        const string text = "ABCDEFGH";
        var weight = 100f;
        MsdfGlyphAtlas? atlas = null;
        // Every weight is another font instance, so another set of fields.
        while (atlas == null || atlas.PageCount <= MsdfGlyphAtlas.MaxPages)
        {
            var paragraph = Paragraph.Layout(text, Black with { Size = 40, Weight = weight });
            weight += 50f;
            frame.Render(White, r => r.DrawParagraph(paragraph, Vector2.Zero));
            atlas = frame.Renderer.MsdfGlyphAtlas!;
            Assert.IsTrue(weight <= 900f, "the atlas never filled");
        }
        var small = Paragraph.Layout("Radiant", Black with { Size = 32 });

        var pixels = frame.Render(White, r => r.DrawParagraph(small, new Vector2(4, 4)));

        Assert.AreEqual(1, atlas.PageCount, "emptied back to its first page");
        Assert.AreEqual(6, atlas.GlyphCount, "only what the frame drew: R, a, d, i, n, t");
        Assert.IsTrue(Ink(frame, pixels) > 255 * 60, "and the text still draws");
    }
}
