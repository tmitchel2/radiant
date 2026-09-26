using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;
using static Radiant.Components.Tests.GoldenSheets;

namespace Radiant.Templates.Tests;

/// <summary>
/// Template blocks as goldens: narrow and wide in light, wide in dark, and wide at compact density,
/// so a change to how a block reflows or spaces itself shows up.
/// </summary>
[TestClass]
[TestCategory("Gpu")]
public class TemplateGoldenTests
{
    private const int Narrow = 380;
    private const int Wide = 900;

    private static void CheckBlock(string name, int narrowHeight, int wideHeight, Func<Element> block)
    {
        var light = new Theme();
        CheckIn(light, $"{name}_narrow", Narrow, narrowHeight, block);
        CheckIn(light, $"{name}_wide", Wide, wideHeight, block);
        CheckIn(light with { Colors = light.Colors with { IsDark = true } }, $"{name}_wide_dark", Wide, wideHeight, block);
        CheckIn(light with { Density = -2 }, $"{name}_wide_compact", Wide, wideHeight, block);
    }

    // A block fills the width it's given, as it would in a page.
    private static Element Fill(Element block) => new Box { Layout = new LayoutStyle { AlignSelf = Align.Stretch }, Children = [block] };

    [TestMethod]
    public void Stats() => CheckBlock("Stats", 780, 200, () => Fill(new StatsGrid(
    [
        new Stat("Visitors", "92.4k") { Change = 0.31, Icon = "group" },
        new Stat("Refunds", "37") { Change = -0.18, Icon = "receipt_long" },
        new Stat("Orders", "1,284") { Change = 0.052, Icon = "shopping_bag" },
        new Stat("Revenue", "$48,210") { Change = 0.124, Icon = "payments" },
    ])));

    [TestMethod]
    public void Pricing() => CheckBlock("Pricing", 1150, 520, () => Fill(new PricingTiers(
    [
        new PricingTier("Hobby", "$0", ["1 project", "Community support"]) { Description = "For trying it out." },
        new PricingTier("Pro", "$24", ["Unlimited projects", "Email support", "Custom domains"]) { Description = "For professionals.", Featured = true },
        new PricingTier("Team", "$96", ["Everything in Pro", "Single sign-on", "Audit log"]) { Description = "For organisations." },
    ], _ => { })));

    [TestMethod]
    public void SignIn() => CheckBlock("SignIn", 460, 460, () => new SignInForm((_, _, _) => { }) { Error = "That password didn't match." });

    [TestMethod]
    public void Empty() => CheckBlock("EmptyState", 320, 280, () => Fill(new EmptyState("inbox", "No messages")
    {
        Description = "When someone writes to you, it'll show up here.",
        Action = new SurfaceButton("Compose") { Icon = "edit" },
    }));

    [TestMethod]
    public void Products() => CheckBlock("Products", 1300, 420, () => Fill(new ProductGrid(
    [
        new Product("Linen shirt", "$48", Picture(0xFF7A9E7E)) { Rating = 4.5, Badge = "New" },
        new Product("Canvas tote", "$24", Picture(0xFFC98B5A)) { Detail = "Natural" },
        new Product("Wool beanie", "$18", Picture(0xFF5A6FC9)) { Rating = 4 },
    ], _ => { })));

    // A plain picture: a vertical gradient of one colour, so products differ without image files.
    private static ImageSource Picture(uint argb)
    {
        const int size = 32;
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            var shade = 0.75 + 0.25 * y / (size - 1);
            for (var x = 0; x < size; x++)
            {
                var i = (y * size + x) * 4;
                pixels[i] = (byte)((argb & 0xFF) * shade);
                pixels[i + 1] = (byte)((argb >> 8 & 0xFF) * shade);
                pixels[i + 2] = (byte)((argb >> 16 & 0xFF) * shade);
                pixels[i + 3] = 255;
            }
        }
        return ImageSource.FromBgra(size, size, pixels);
    }
}
