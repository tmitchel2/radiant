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

    // The showcase blocks in each preset but the default, wide, light and dark: heroes, features,
    // the featured plan, calls to action and empty states take their looks from the theme.
    [TestMethod]
    [DataRow("Quartz")]
    [DataRow("Linen")]
    public void ShowcaseInPresets(string preset)
    {
        foreach (var dark in new[] { false, true })
        {
            var theme = ThemePresets.Find(preset)!;
            theme = theme with { Colors = theme.Colors with { IsDark = dark } };
            CheckIn(theme, $"Showcase_{preset}_{(dark ? "dark" : "light")}", Wide, 1500, () => Fill(new Box
            {
                Layout = new LayoutStyle { RowGap = 32 },
                Children =
                [
                    new Hero("Desktop apps, built in C#")
                    {
                        Eyebrow = "Radiant 1.0",
                        Text = "A declarative UI, themes you can swap live, sharp text and GPU rendering.",
                        Actions = [new SurfaceButton("Get started"), new SurfaceButton("Read the docs", ButtonVariant.Text)],
                    },
                    new FeatureGrid("Everything a desktop app needs",
                    [
                        new Feature("palette", "Themes", "Swap the whole look live."),
                        new Feature("text_fields", "Text", "Shaping, bidi and sharp glyphs."),
                        new Feature("bolt", "Fast", "Only what changed is rebuilt."),
                    ]),
                    new PricingTiers(
                    [
                        new PricingTier("Hobby", "$0", ["1 project"]) { Description = "For trying it out." },
                        new PricingTier("Pro", "$24", ["Unlimited projects", "Email support"]) { Description = "For professionals.", Featured = true },
                        new PricingTier("Team", "$96", ["Single sign-on"]) { Description = "For organisations." },
                    ], _ => { }),
                    new CallToAction("Ready to build something great?") { Text = "Start free.", Actions = [new SurfaceButton("Get started")] },
                    new EmptyState("inbox", "No messages") { Description = "When someone writes to you, it'll show up here." },
                ],
            }));
        }
    }

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

    [TestMethod]
    public void TestimonialQuotes() => CheckBlock("Testimonials", 900, 330, () => Fill(new Testimonials("Loved by teams",
    [
        new Testimonial("We shipped our desktop app in half the time, and it looks native everywhere.", "Ada Lovelace", "CTO, Analytical"),
        new Testimonial("The theming alone saved us weeks. Dark mode just worked.", "Grace Hopper", "Lead engineer, Cobol & Co"),
        new Testimonial("Accessible out of the box, which our users noticed straight away.", "Alan Turing", "Founder, Enigma"),
    ])));

    [TestMethod]
    public void FrequentQuestions() => CheckBlock("Faq", 480, 420, () => Fill(new Faq("Frequently asked questions",
    [
        new FaqEntry("Is there a free plan?", "Yes: the Hobby plan is free for personal projects, with no time limit."),
        new FaqEntry("Can I cancel any time?", "You can, from Settings. You keep access until the end of the billing period."),
        new FaqEntry("Do you offer refunds?", "Within 30 days of purchase, no questions asked."),
    ]) { Subtitle = "Can't find what you need? Contact support." }));

    [TestMethod]
    public void ClosingPitch() => CheckBlock("CallToAction", 360, 300, () => Fill(new CallToAction("Ready to build something great?")
    {
        Text = "Start free, and upgrade when your team grows.",
        Actions = [new SurfaceButton("Get started"), new SurfaceButton("Talk to sales", ButtonVariant.Text)],
    }));

    [TestMethod]
    public void NewsletterSignUp() => CheckBlock("Newsletter", 300, 200, () => Fill(new Newsletter("Stay up to date", null)
    {
        Text = "News and releases, once a month. No spam.",
    }));

    [TestMethod]
    public void Footer() => CheckBlock("Footer", 560, 330, () => Fill(new SiteFooter("Radiant",
    [
        new FooterColumn("Product", ["Features", "Pricing", "Changelog"]),
        new FooterColumn("Company", ["About", "Careers", "Contact"]),
        new FooterColumn("Legal", ["Privacy", "Terms"]),
    ])
    {
        Tagline = "A desktop app platform for .NET.",
        Copyright = "© 2026 Radiant. All rights reserved.",
    }));

    [TestMethod]
    public void PageNotFound() => CheckBlock("NotFound", 380, 380, () => Fill(new NotFound
    {
        Actions = [new SurfaceButton("Go home") { Icon = "arrow_back" }, new SurfaceButton("Contact support", ButtonVariant.Text)],
    }));

    [TestMethod]
    public void ProductReviews() => CheckBlock("Reviews", 1000, 600, () => Fill(new Reviews(
    [
        new Review("Ada Lovelace", 5, "Perfect fit", "Exactly as described, and the linen softens after a wash.") { Date = "12 March" },
        new Review("Grace Hopper", 4, "Good shirt", "Runs slightly large; I'd size down.") { Date = "2 March" },
        new Review("Alan Turing", 4, "Would buy again", "Arrived quickly and well packed.") { Date = "20 February" },
    ]) { OnWrite = () => { } }));

    [TestMethod]
    public void PastOrders() => CheckBlock("OrderHistory", 760, 560, () => Fill(new OrderHistory(
    [
        new Order("WU88191111", "12 March 2026", "$96.00", "On its way", [new OrderLine("Linen shirt", 1, "$48.00"), new OrderLine("Canvas tote", 2, "$48.00")]),
        new Order("WU88191009", "2 January 2026", "$18.00", "Delivered", [new OrderLine("Wool beanie", 1, "$18.00")]) { Complete = true },
    ]) { OnView = _ => { }, OnBuyAgain = _ => { } }));

    [TestMethod]
    public void Checkout() => CheckBlock("Checkout", 780, 700, () => Fill(new CheckoutForm(MoreTemplateTests.Countries, MoreTemplateTests.Delivery, _ => { }) { PlaceOrderLabel = "Pay $101.00" }));

    [TestMethod]
    public void Notifications() => CheckBlock("Notifications", 420, 420, () => Fill(new NotificationFeed(
    [
        new NotificationEntry("chat", "Ada commented on your design", "5 min ago") { Unread = true },
        new NotificationEntry("person_add", "Grace joined your team", "1 hour ago") { Unread = true },
        new NotificationEntry("check_circle", "Your export finished", "Yesterday"),
    ]) { OnMarkAllRead = () => { } }));

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
