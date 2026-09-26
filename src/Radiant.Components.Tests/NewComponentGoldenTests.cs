using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;
using static Radiant.Components.Tests.GoldenSheets;

namespace Radiant.Components.Tests;

/// <summary>Goldens for the banner, button group, collapsible, time picker, aspect ratio, fieldset and property grid.</summary>
[TestClass]
[TestCategory("Gpu")]
public class NewComponentGoldenTests
{
    [TestMethod]
    public void Assorted() => Check("Assorted", 600, 560, () => Column(16,
        new Banner("You're offline. Changes will sync when you reconnect.")
        {
            Icon = "wifi_off",
            Actions = [new SurfaceButton("Dismiss", ButtonVariant.Text), new SurfaceButton("Retry", ButtonVariant.Text)],
        },
        Row(
            new ButtonGroup(
            [
                new GroupButton("Bold", null) { Icon = "format_bold", IconOnly = true },
                new GroupButton("Italic", null) { Icon = "format_italic", IconOnly = true },
                new GroupButton("Underline", null) { Icon = "format_underlined", IconOnly = true },
            ]) { Label = "Style" },
            new ButtonGroup(
            [
                new GroupButton("Zoom out", null) { Icon = "zoom_out" },
                new GroupButton("Fit", null) { Icon = "fit_screen" },
                new GroupButton("Zoom in", null) { Icon = "zoom_in", Disabled = true },
            ])),
        Row(
            new TimePicker("Start", new TimeOnly(9, 41), _ => { }) { Use24Hour = true, Culture = System.Globalization.CultureInfo.GetCultureInfo("en-US") },
            new TimePicker("Alarm", new TimeOnly(18, 5), _ => { }) { Use24Hour = false, Culture = System.Globalization.CultureInfo.GetCultureInfo("en-US") },
            new TimePicker("End", null, _ => { }) { Use24Hour = true, Culture = System.Globalization.CultureInfo.GetCultureInfo("en-US") }),
        new Box
        {
            Layout = new LayoutStyle { AlignSelf = Align.Stretch },
            Children =
            [
                new Collapsible("Advanced options", new SurfaceText("Retries, timeouts and logging.")) { InitiallyOpen = true, Icon = "tune" },
                new Collapsible("Experimental", new SurfaceText("Hidden")),
            ],
        },
        new Box
        {
            Layout = new LayoutStyle { Width = 240 },
            Children = [new AspectRatio(16f / 9f, new Surface { SurfaceColor = SurfaceName.Tertiary, SurfaceContainerToggle = true, Layout = new LayoutStyle { FlexGrow = 1, AlignItems = Align.Center, JustifyContent = Justify.Center }, Children = [new SurfaceText("16 : 9")] })],
        }));

    [TestMethod]
    public void Forms() => Check("Inspector", 600, 520, () => Row(
        new Box
        {
            Layout = new LayoutStyle { Width = 250, AlignSelf = Align.FlexStart },
            Children =
            [
                new Fieldset("Notifications",
                [
                    new Checkbox(true, _ => { }) { Label = "Email" },
                    new Checkbox(false, _ => { }) { Label = "Push" },
                    new Switch(true, _ => { }) { Label = "Weekly digest" },
                ]) { Description = "How we reach you." },
            ],
        },
        new Box
        {
            Layout = new LayoutStyle { Width = 300, AlignSelf = Align.FlexStart },
            Children =
            [
                new PropertyGrid(
                [
                    new PropertySection("Layout",
                    [
                        new PropertyItem("Width", new NumberField("Width", 240, _ => { }) { Layout = new LayoutStyle { Width = 150 } }),
                        new PropertyItem("Visible", new Switch(true, _ => { }) { AccessibleLabel = "Visible" }),
                    ]),
                    new PropertySection("Appearance", [new PropertyItem("Corners", new SurfaceText("Medium"))]) { InitiallyOpen = false },
                ]) { Filterable = true, NameWidth = 90 },
            ],
        }));
}
