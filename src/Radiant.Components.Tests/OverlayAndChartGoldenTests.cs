using System;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.ColorSystem;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;
using static Radiant.Components.Tests.GoldenSheets;

namespace Radiant.Components.Tests;

/// <summary>Goldens for pickers, sheets, snackbars, the palette and charts, and the theme's variants.</summary>
[TestClass]
[TestCategory("Gpu")]
public class OverlayAndChartGoldenTests
{
    private static readonly DateOnly Day = new(2026, 3, 18);

    [TestMethod]
    public void Calendar() => Check("Calendar", 360, 400, () => new Calendar(Day, _ => { })
    {
        Today = Day.AddDays(-2),
        InitialMonth = Day,
        IsDisabled = d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
        Culture = CultureInfo.GetCultureInfo("en-GB"),
    });

    [TestMethod]
    public void ColorPicker() => Check("ColorPicker", 340, 330, () => new ColorPicker(unchecked((int)0xFF6750A4), _ => { }));

    [TestMethod]
    public void OpenSheet() => Check("Sheet", 640, 400, () => new Sheet(true, () => { }, new SurfaceText("Filters for the list go here."))
    {
        Title = "Filters",
        Size = 300,
        Actions = [new SurfaceButton("Reset", ButtonVariant.Text), new SurfaceButton("Apply")],
    });

    [TestMethod]
    public void Snackbar() => Check("Snackbar", 520, 200, () => new SnackbarHost(new Host(context =>
    {
        var snackbars = context.UseSnackbars();
        context.UseEffect(() =>
        {
            snackbars.Show("Photo deleted", "Undo", () => { });
            return null;
        }, default(ValueTuple));
        return null;
    })), frames: 40);

    [TestMethod]
    public void CommandPalette() => Check("CommandPalette", 640, 420, () => new CommandPalette(true, () => { },
    [
        new Command("new", "New file") { Group = "File", Icon = "add", Shortcut = KeyChord.Command(KeyCode.N) },
        new Command("open", "Open…") { Group = "File", Icon = "folder_open", Shortcut = KeyChord.Command(KeyCode.O) },
        new Command("dark", "Dark theme") { Group = "View", Icon = "palette" },
        new Command("zoom", "Zoom in") { Group = "View", Icon = "add", Shortcut = KeyChord.Command(KeyCode.Equal) },
    ]));

    [TestMethod]
    public void Charts()
    {
        string[] months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun"];
        Check("Charts", 640, 560, () => Column(16,
            new LineChart(months, [new ChartSeries("This year", [12, 18, 15, 24, 29, 35]), new ChartSeries("Last year", [10, 11, 14, 13, 18, 21])])
            {
                Title = "Revenue",
                Area = true,
                Height = 180,
            },
            Row(
                new Box
                {
                    Layout = new LayoutStyle { Width = 360 },
                    Children = [new BarChart(["North", "South", "East"], [new ChartSeries("Online", [40, 25, 32]), new ChartSeries("Stores", [20, 30, 12])]) { Stacked = true, Height = 160 }],
                },
                new DonutChart([("Search", 48), ("Direct", 27), ("Social", 15), ("Email", 10)]) { Caption = "Visits", Size = 150 })));
    }

    /// <summary>The button sheet in the theme's other variants and at high contrast, where colours are chosen differently.</summary>
    [TestMethod]
    public void ThemeVariants()
    {
        (string Name, ThemeColors Colors)[] variants =
        [
            ("HighContrast_light", new ThemeColors { ContrastLevel = 1 }),
            ("HighContrast_dark", new ThemeColors { ContrastLevel = 1, IsDark = true }),
            ("Vibrant_light", new ThemeColors { Variant = Variant.Vibrant, Seed = Radiant.Graphics2D.Color.FromArgb(0xFF0B6E4F) }),
            ("Monochrome_dark", new ThemeColors { Variant = Variant.Monochrome, IsDark = true }),
        ];
        foreach (var (name, colors) in variants)
        {
            CheckIn(new Theme { Colors = colors }, $"Buttons_{name}", 480, 190, () => Column(12,
                Row(new SurfaceButton("Filled"), new SurfaceButton("Tonal", ButtonVariant.Tonal), new SurfaceButton("Outlined", ButtonVariant.Outlined), new SurfaceButton("Text", ButtonVariant.Text)),
                Row(new Chip("Filter") { Selected = true }, new Switch(true, _ => { }) { Label = "On" }, new Checkbox(true, _ => { }) { Label = "Checked" }),
                new Alert("Saved") { Kind = AlertKind.Success, Layout = new LayoutStyle { Width = 300 } }));
        }
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
