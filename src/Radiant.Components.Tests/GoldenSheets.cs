using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Testing;
using Radiant.Text;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

/// <summary>Helpers for golden sheets: a themed surface to put components on, and a check in light and dark.</summary>
internal static class GoldenSheets
{
    public static GpuCanvas Canvas(int width, int height)
    {
        if (GpuCanvas.TryCreate(width, height, pixelScale: 2f) is { } canvas)
        {
            return canvas;
        }
        Assert.Inconclusive("No GPU available.");
        return null!;
    }

    /// <summary>The content on the theme's surface, in light or dark, and right to left if asked.</summary>
    public static Element Themed(bool dark, Element? content, bool rightToLeft = false)
    {
        Element surface = new Surface
        {
            SurfaceColor = SurfaceName.Surface,
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(16), RowGap = 12, AlignItems = Align.FlexStart },
            Children = [content],
        };
        return new ThemeProvider(
            new ThemeController(new Theme { Colors = new Theme().Colors with { IsDark = dark } }),
            rightToLeft ? new Directionality(TextDirection.RightToLeft, surface) : surface);
    }

    public static Box Row(params Element?[] items) => new()
    {
        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12 },
        Children = items,
    };

    public static Box Column(float gap, params Element?[] items) => new()
    {
        Layout = new LayoutStyle { RowGap = gap, AlignSelf = Align.Stretch },
        Children = items,
    };

    /// <summary>Renders the sheet light and dark and checks each against its golden.</summary>
    public static void Check(string name, int width, int height, Func<Element> sheet, Action<UIRoot>? act = null, int frames = 30, bool rightToLeft = false)
    {
        foreach (var dark in new[] { false, true })
        {
            using var canvas = Canvas(width, height);
            var snapshot = UISnapshot.Render(canvas, Themed(dark, sheet(), rightToLeft), act: act, frames: frames);
            Golden.AssertMatches(snapshot, $"{name}_{(dark ? "dark" : "light")}");
        }
    }

    public static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);
}
