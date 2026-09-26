using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Radiant.Tests.Graphics2D.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Radiant.Graphics2D.Color;

namespace Radiant.Tests.Graphics2D;

/// <summary>
/// One scene using every renderer feature, compared with a golden image, so a change anywhere in
/// the renderer that alters what is drawn is caught even where no targeted test looks.
/// Regenerate with UPDATE_GOLDEN_IMAGES=true after an intended change, and review the image.
/// </summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class ReferenceSceneGpuTests
{
    [TestMethod]
    public void TheReferenceSceneLooksAsItDid()
    {
        using var frame = GpuFrame.CreateOrSkip(320, 240);
        var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);
        var ink = Color.Parse("#1c1b1f");

        var pixels = frame.Render(Color.Parse("#f3edf7"), r =>
        {
            // Elevated cards: two-layer shadows under rounded surfaces.
            for (var i = 0; i < 3; i++)
            {
                var x = 16 + i * 64;
                r.DrawShadow(x, 16, 52, 60, 10f, 4f + i * 6f, new Vector4(0, 0, 0, 0.15f), new Vector2(0, 1 + i * 2), spread: 1);
                r.DrawShadow(x, 16, 52, 60, 10f, 2f + i, new Vector4(0, 0, 0, 0.3f), new Vector2(0, 1));
                r.DrawRoundedRectFilled(x, 16, 52, 60, 10f, Color.Parse("#fffbfe"));
            }

            // A gradient header in OKLab.
            r.DrawRoundedRectFilled(212, 16, 92, 60, CornerRadii.Top(16f),
                Gradient.Linear(new Vector2(212, 16), new Vector2(304, 76), Color.Parse("#6750a4"), Color.Parse("#e8def8"), GradientInterpolation.Oklab));

            // A rounded, scrolled list: text and shapes cut to the corners.
            r.DrawRoundedRectFilled(16, 92, 140, 132, 16f, Color.Parse("#ece6f0"));
            r.PushClip(16, 92, 140, 132, 16f);
            r.PushScrollOffset(new Vector2(0, -10));
            for (var row = 0; row < 7; row++)
            {
                var y = 94 + row * 22;
                r.DrawDisc(new Vector2(30, y + 10), 7f, Color.Parse("#6750a4"));
                r.DrawText(font, $"Row {row + 1}", 44, y + 3, 13f, ink);
            }
            r.PopScrollOffset();
            r.PopClip();

            // A translucent layer of overlapping shapes, over text.
            r.DrawText(font, "Under the layer", 172, 100, 13f, ink);
            r.PushLayer(0.6f);
            r.DrawRoundedRectFilled(172, 92, 70, 50, 12f, Color.Parse("#b3261e"));
            r.DrawDisc(new Vector2(250, 120), 28f, Color.Parse("#386a20"));
            r.PopLayer();

            // A rotated card with a shadow and a border.
            r.PushTransform(Matrix3x2.CreateRotation(-0.25f, new Vector2(250, 190)));
            r.DrawShadow(210, 165, 80, 50, 12f, 12f, new Vector4(0, 0, 0, 0.3f), new Vector2(0, 4));
            r.DrawRoundedRect(210, 165, 80, 50, 12f, 2f, Color.Parse("#ffd8e4"), Color.Parse("#7d5260"));
            r.DrawText(font, "Rotated", 222, 180, 16f, ink);
            r.PopTransform();
        });

        using var image = Image.LoadPixelData<Bgra32>(pixels, frame.Width, frame.Height);
        using var rgba = image.CloneAs<Rgba32>();
        GoldenImageHelper.AssertMatchesGolden(rgba, "Gpu_ReferenceScene", tolerance: 3);
    }
}
