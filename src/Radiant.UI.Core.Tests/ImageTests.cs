using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Tests.Graphics2D;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class ImageTests
{
    // A 2×1 picture: red on the left, blue on the right (BGRA).
    private static ImageSource RedBlue() => ImageSource.FromBgra(2, 1, [0, 0, 255, 255, 255, 0, 0, 255]);

    [TestMethod]
    public void AnImageTakesItsPicturesSizeOrKeepsItsProportions()
    {
        using var root = new UIRoot(new Box
        {
            Layout = new LayoutStyle { AlignItems = Align.FlexStart },
            Children =
            [
                new Image(RedBlue()),
                new Image(RedBlue()) { Layout = new LayoutStyle { Width = 100 } },
            ],
        });
        root.Update(new Vector2(400, 300));

        var images = root.RootRenderNode.Children[0].Children;
        Assert.AreEqual(new Vector2(2, 1), images[0].Size);
        Assert.AreEqual(new Vector2(100, 50), images[1].Size);
    }

    [TestMethod]
    public void AnImageWithAltTextIsAPictureToAssistiveTechnology()
    {
        using var root = new UIRoot(new Box { Children = [new Image(RedBlue()) { AltText = "Flag" }, new Image(RedBlue())] });
        root.Update(new Vector2(400, 300));

        var nodes = root.GetSemantics().Children;

        Assert.AreEqual(1, nodes.Count, "the decorative one is skipped");
        Assert.AreEqual((SemanticsRole.Image, "Flag"), (nodes[0].Role, nodes[0].Label));
    }

    [TestMethod]
    [TestCategory(GpuFrame.Category)]
    public void CoverCropsTheOverflowAndFillStretches()
    {
        using var frame = GpuFrame.CreateOrSkip(64, 32);
        using var root = new UIRoot(new Box
        {
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row },
            Children =
            [
                // A 2:1 picture covering a square: the middle shows, so red meets blue in the centre.
                new Image(RedBlue()) { Fit = ImageFit.Cover, Layout = new LayoutStyle { Width = 32, Height = 32 } },
                new Image(RedBlue()) { Fit = ImageFit.Fill, Layout = new LayoutStyle { Width = 32, Height = 32 } },
            ],
        });
        root.Update(new Vector2(64, 32));

        var pixels = frame.Render(Vector4.One, root.Paint);

        // Two texels filtered in linear light: each side is mostly its own colour.
        Assert.IsTrue(frame.PixelAt(pixels, 4, 16).R > frame.PixelAt(pixels, 4, 16).B * 2, "red at the cover's left");
        Assert.IsTrue(frame.PixelAt(pixels, 28, 16).B > frame.PixelAt(pixels, 28, 16).R * 2, "blue at its right");
        Assert.IsTrue(frame.PixelAt(pixels, 34, 16).R > 200, "the filled one starts red");
        Assert.IsTrue(frame.PixelAt(pixels, 62, 16).B > 200, "and ends blue");
    }
}
