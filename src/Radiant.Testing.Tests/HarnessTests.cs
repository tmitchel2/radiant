using System;
using System.IO;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.UI.Core;

namespace Radiant.Testing.Tests;

[TestClass]
public class HarnessTests
{
    private static Snapshot Solid(int width, int height, byte r, byte g, byte b)
    {
        var rgba = new byte[width * height * 4];
        for (var i = 0; i < rgba.Length; i += 4)
        {
            (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = (r, g, b, 255);
        }
        return new Snapshot(width, height, rgba);
    }

    private static Snapshot WithPixel(Snapshot image, int x, int y, byte r, byte g, byte b)
    {
        var rgba = (byte[])image.Rgba.Clone();
        var i = (y * image.Width + x) * 4;
        (rgba[i], rgba[i + 1], rgba[i + 2]) = (r, g, b);
        return new Snapshot(image.Width, image.Height, rgba);
    }

    private static string TempDirectory() => Directory.CreateTempSubdirectory("radiant-golden-").FullName;

    [TestMethod]
    public void NearlyTheSameColourMatchesButAChangedOneDoesNot()
    {
        var grey = Solid(4, 4, 128, 128, 128);

        Assert.AreEqual(0, ImageDiff.Compare(WithPixel(grey, 1, 1, 130, 128, 127), grey).DifferentPixels);
        var changed = ImageDiff.Compare(WithPixel(grey, 1, 1, 200, 60, 60), grey);
        Assert.AreEqual(1, changed.DifferentPixels);
        Assert.AreEqual((255, 0, 0, 255), changed.Diff.PixelAt(1, 1), "the differing pixel is red in the diff");
    }

    [TestMethod]
    public void AMissingGoldenIsWrittenThenChecked()
    {
        var directory = TempDirectory();
        var options = new GoldenOptions { Directory = directory, ResultsDirectory = Path.Combine(directory, "results") };
        var image = Solid(8, 8, 10, 120, 200);

        Golden.AssertMatches(image, "blue", options);
        Assert.IsTrue(File.Exists(Path.Combine(directory, "blue.png")));
        Golden.AssertMatches(image, "blue", options);

        var changed = WithPixel(image, 3, 3, 250, 250, 0);
        var failure = Assert.ThrowsException<GoldenMismatchException>(() => Golden.AssertMatches(changed, "blue", options));
        StringAssert.Contains(failure.Message, "1 pixels differ");
        Assert.IsTrue(File.Exists(Path.Combine(directory, "results", "blue.diff.png")));
        Assert.IsTrue(File.Exists(Path.Combine(directory, "results", "blue.actual.png")));
        Golden.AssertMatches(changed, "blue", options with { MaxDifferentPixels = 1 });
    }

    [TestMethod]
    public void ADifferentSizeDoesNotMatch()
    {
        var directory = TempDirectory();
        var options = new GoldenOptions { Directory = directory, ResultsDirectory = Path.Combine(directory, "results") };
        Golden.AssertMatches(Solid(8, 8, 0, 0, 0), "size", options);

        Assert.ThrowsException<GoldenMismatchException>(() => Golden.AssertMatches(Solid(8, 9, 0, 0, 0), "size", options));
    }

    [TestMethod]
    public void ImagesJoinInRowsAndColumns()
    {
        var row = Snapshot.Row([Solid(2, 3, 255, 0, 0), Solid(4, 1, 0, 0, 255)], gap: 1);
        Assert.AreEqual((7, 3), (row.Width, row.Height));
        Assert.AreEqual((0, 0, 255, 255), row.PixelAt(3, 0));
        Assert.AreEqual((255, 255, 255, 255), row.PixelAt(2, 0), "the gap is the background");
        Assert.AreEqual((255, 255, 255, 255), row.PixelAt(4, 2), "below a shorter image too");

        var column = Snapshot.Column([Solid(2, 2, 0, 0, 0), Solid(3, 1, 0, 0, 0)]);
        Assert.AreEqual((3, 3), (column.Width, column.Height));
    }

    [TestMethod]
    public void PngsRoundTrip()
    {
        var path = Path.Combine(TempDirectory(), "image.png");
        var image = WithPixel(Solid(5, 3, 1, 2, 3), 4, 2, 200, 100, 50);

        image.SavePng(path);

        CollectionAssert.AreEqual(image.Rgba, Snapshot.LoadPng(path).Rgba);
    }

    [TestMethod]
    [TestCategory("Gpu")]
    public void AUIRendersOnAFixedClock()
    {
        using var canvas = GpuCanvas.TryCreate(40, 20, pixelScale: 2f);
        if (canvas is null)
        {
            Assert.Inconclusive("No GPU available.");
            return;
        }
        var red = new Vector4(1, 0, 0, 1);
        Element Ui() => new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, FlexDirection = FlexDirection.Row },
            Children = [new Box { Layout = new LayoutStyle { Width = 20 }, Background = red }],
        };

        var first = UISnapshot.Render(canvas, Ui());
        var second = UISnapshot.Render(canvas, Ui());

        Assert.AreEqual((80, 40), (first.Width, first.Height), "pixels at the canvas's scale");
        Assert.AreEqual((255, 0, 0, 255), first.PixelAt(10, 10));
        Assert.AreEqual((255, 255, 255, 255), first.PixelAt(70, 10), "on the default white");
        CollectionAssert.AreEqual(first.Rgba, second.Rgba, "the same UI renders the same every time");
    }
}
