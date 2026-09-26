using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Radiant.Layout;
using Radiant.Tests.Graphics2D;
using Radiant.Text;
using Color = Radiant.Graphics2D.Color;

namespace Radiant.UI.Core.Tests;

/// <summary>What a UI tree looks like once drawn.</summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class PaintGpuTests
{
    private static readonly Vector4 White = Vector4.One;
    private static readonly Color Red = Color.Parse("#ff0000");
    private static readonly Color Blue = Color.Parse("#0000ff");

    private static byte[] Draw(GpuFrame frame, Element element)
    {
        using var root = new UIRoot(element);
        root.Update(new Vector2(frame.Width / frame.PixelScale, frame.Height / frame.PixelScale));
        return frame.Render(White, root.Paint);
    }

    private static LayoutStyle At(float x, float y, float w, float h) => new()
    {
        Position = PositionType.Absolute,
        Inset = new Edges(x, y, Dimension.Undefined, Dimension.Undefined),
        Width = w,
        Height = h,
    };

    [TestMethod]
    public void BoxesDrawTheirBackgroundsWhereTheyAreLaidOut()
    {
        using var frame = GpuFrame.CreateOrSkip(64, 64);

        var pixels = Draw(frame, new Box
        {
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, Padding = Edges.All(8) },
            Children =
            [
                new Box { Layout = new LayoutStyle { Width = 20, Height = 20 }, Background = Red },
                new Box { Layout = new LayoutStyle { Width = 20, Height = 20 }, Background = Blue },
            ],
        });

        Assert.AreEqual((255, 0, 0), Rgb(frame.PixelAt(pixels, 18, 18)));
        Assert.AreEqual((0, 0, 255), Rgb(frame.PixelAt(pixels, 38, 18)));
        Assert.AreEqual((255, 255, 255), Rgb(frame.PixelAt(pixels, 18, 40)));
    }

    [TestMethod]
    public void ClippedChildrenAreCutToTheirParent()
    {
        using var frame = GpuFrame.CreateOrSkip(64, 64);

        var pixels = Draw(frame, new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children =
            [
                new Box
                {
                    Layout = At(8, 8, 20, 20),
                    ClipContent = true,
                    Children = [new Box { Layout = At(0, 0, 40, 40), Background = Red }],
                },
            ],
        });

        Assert.AreEqual((255, 0, 0), Rgb(frame.PixelAt(pixels, 20, 20)));
        Assert.AreEqual((255, 255, 255), Rgb(frame.PixelAt(pixels, 35, 35)));
    }

    [TestMethod]
    public void OpacityFadesABoxAndItsChildrenAsOne()
    {
        using var frame = GpuFrame.CreateOrSkip(64, 64);

        var pixels = Draw(frame, new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children =
            [
                new Box
                {
                    Layout = At(8, 8, 40, 40),
                    Opacity = 0.5f,
                    Background = Red,
                    Children = [new Box { Layout = At(10, 10, 10, 10), Background = Red }],
                },
            ],
        });

        // Where the child overlaps its parent, the group is still half-transparent red: one layer.
        Assert.AreEqual(frame.PixelAt(pixels, 12, 12), frame.PixelAt(pixels, 22, 22));
        Assert.IsTrue(frame.PixelAt(pixels, 22, 22).G is > 100 and < 255);
    }

    [TestMethod]
    public void TransformsMoveBoxesAndTheirChildren()
    {
        using var frame = GpuFrame.CreateOrSkip(64, 64);

        var pixels = Draw(frame, new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children =
            [
                new Box
                {
                    Layout = At(0, 0, 20, 20),
                    Transform = Matrix3x2.CreateTranslation(30, 30),
                    Children = [new Box { Layout = At(0, 0, 20, 20), Background = Blue }],
                },
            ],
        });

        Assert.AreEqual((0, 0, 255), Rgb(frame.PixelAt(pixels, 40, 40)));
        Assert.AreEqual((255, 255, 255), Rgb(frame.PixelAt(pixels, 10, 10)));
    }

    [TestMethod]
    public void TextIsDrawnInsideItsBlock()
    {
        using var frame = GpuFrame.CreateOrSkip(128, 48);

        var pixels = Draw(frame, new Box
        {
            Layout = new LayoutStyle { Padding = Edges.All(10), AlignItems = Align.FlexStart },
            Children = [new TextBlock("Radiant") { Style = new TextStyle { Size = 16, Color = new Vector4(0, 0, 0, 1) } }],
        });

        var ink = 0;
        var outside = 0;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var dark = 255 - frame.PixelAt(pixels, x, y).G;
                if (x >= 10 && y >= 10 && y < 10 + 20)
                {
                    ink += dark;
                }
                else
                {
                    outside += dark;
                }
            }
        }
        Assert.IsTrue(ink > 255 * 20, $"ink {ink}");
        Assert.AreEqual(0, outside);
    }

    private static (byte R, byte G, byte B) Rgb((byte R, byte G, byte B, byte A) pixel) => (pixel.R, pixel.G, pixel.B);
}
