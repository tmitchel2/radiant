using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Templates;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Gallery;

/// <summary>The gallery's sample pages, one per template family.</summary>
internal static class Pages
{
    public static Element Dashboard() => new Box
    {
        Layout = new LayoutStyle { RowGap = 24 },
        Children =
        [
            new PageHeading("Dashboard")
            {
                Description = "How the last 30 days went",
                Actions = [new SurfaceButton("Export", ButtonVariant.Outlined) { Icon = "download" }, new SurfaceButton("New report") { Icon = "add" }],
            },
            new StatsGrid(
            [
                new Stat("Revenue", "$48,210") { Change = 0.124, Icon = "payments" },
                new Stat("Orders", "1,284") { Change = 0.052, Icon = "shopping_bag" },
                new Stat("Refunds", "37") { Change = -0.18, Icon = "receipt_long" },
                new Stat("Visitors", "92.4k") { Change = 0.31, Icon = "groups" },
            ]),
            new Card(new LineChart(
                ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"],
                [
                    new ChartSeries("This year", [31, 34, 33, 39, 42, 41, 46, 45, 48, 52, 55, 61]),
                    new ChartSeries("Last year", [24, 26, 29, 28, 31, 33, 32, 35, 37, 36, 40, 44]),
                ])
            {
                Title = "Revenue",
                Area = true,
                Format = v => $"${v:0}k",
            })
            {
                Variant = CardVariant.Outlined,
                Layout = new LayoutStyle { Padding = Edges.All(20) },
            },
            new Grid
            {
                MinColumnWidth = 360,
                ColumnGap = 16,
                RowGap = 16,
                Children =
                [
                    new Card(new BarChart(["North", "South", "East", "West"],
                        [
                            new ChartSeries("Online", [420, 310, 380, 290]),
                            new ChartSeries("In store", [180, 240, 150, 210]),
                        ]) { Title = "Orders by region", Stacked = true, Height = 200 })
                    {
                        Variant = CardVariant.Outlined,
                        Layout = new LayoutStyle { Padding = Edges.All(20) },
                    },
                    new Card(new DonutChart([("Search", 48), ("Direct", 27), ("Social", 15), ("Email", 10)]) { Title = "Visitors by source", Caption = "92.4k" })
                    {
                        Variant = CardVariant.Outlined,
                        Layout = new LayoutStyle { Padding = Edges.All(20) },
                    },
                ],
            },
            new StackedList(
            [
                new ListEntry("Ada Lovelace", "Ordered the Analytical Engine kit") { Meta = "2m ago", Status = "Paid" },
                new ListEntry("Grace Hopper", "Returned a compiler, unused") { Meta = "1h ago", Status = "Refunded" },
                new ListEntry("Alan Turing", "Subscribed to the monthly plan") { Meta = "3h ago" },
                new ListEntry("Katherine Johnson", "Upgraded to Team") { Meta = "Yesterday", Status = "Paid" },
            ]) { Title = "Recent activity" },
        ],
    };

    public static Element Settings(ThemeController themes) => new SettingsPage(themes);

    public static Element SignIn() => new Box
    {
        Layout = new LayoutStyle { AlignItems = Align.Center, Padding = Edges.Symmetric(0, 24) },
        Children = [new SignInForm((_, _, _) => { })],
    };

    public static Element Marketing() => new Box
    {
        Layout = new LayoutStyle { RowGap = 40 },
        Children =
        [
            new Hero("Desktop apps that feel native, built in C#")
            {
                Eyebrow = "Radiant 1.0",
                Text = "A declarative UI, a Material-inspired theme system, sharp text and GPU rendering, in one platform.",
                Actions = [new SurfaceButton("Get started") { Icon = "rocket_launch" }, new SurfaceButton("Read the docs", ButtonVariant.Text)],
                Picture = SamplePictures.Gradient(250),
            },
            new FeatureGrid("Everything a desktop app needs",
            [
                new Feature("palette", "Themes from one colour", "Pick a seed and every colour, in light and dark, is worked out and readable."),
                new Feature("text_fields", "Text done properly", "Shaping, bidi, line breaking and three ways to draw glyphs."),
                new Feature("bolt", "Fast by design", "Only what changed is rebuilt, laid out and drawn."),
                new Feature("widgets", "A full component set", "From buttons to data tables, all keyboard and screen-reader ready."),
            ]) { Subtitle = "Radiant's layers, from pixels to page templates." },
            new PricingTiers(
            [
                new PricingTier("Hobby", "$0", ["One app", "Community support"]) { Description = "For trying it out", Period = "" },
                new PricingTier("Pro", "$12", ["Unlimited apps", "Priority support", "All templates"]) { Description = "For professionals", Featured = true },
                new PricingTier("Team", "$49", ["Everything in Pro", "Shared themes", "Single sign-on"]) { Description = "For teams" },
            ], null),
            new Testimonials("Loved by teams",
            [
                new Testimonial("We shipped our desktop app in half the time, and it looks native everywhere.", "Ada Lovelace", "CTO, Analytical"),
                new Testimonial("The theming alone saved us weeks. Dark mode just worked.", "Grace Hopper", "Lead engineer, Cobol & Co"),
                new Testimonial("Accessible out of the box, which our users noticed straight away.", "Alan Turing", "Founder, Enigma"),
            ]),
            new Faq("Frequently asked questions",
            [
                new FaqEntry("Which platforms does it run on?", "macOS today; Windows and Linux are planned, behind the same platform interfaces."),
                new FaqEntry("Can I publish with Native AOT?", "Yes: the gallery and its self-test publish and run as native binaries."),
                new FaqEntry("Is it accessible?", "Components expose roles, names and states, and VoiceOver can read and press them."),
            ]) { Subtitle = "Can't find what you need? Ask on the forum." },
            new CallToAction("Ready to build something great?")
            {
                Text = "Start free, and upgrade when your team grows.",
                Actions = [new SurfaceButton("Get started"), new SurfaceButton("Talk to sales", ButtonVariant.Text)],
            },
            new Newsletter("Stay up to date", null) { Text = "News and releases, once a month. No spam." },
            new SiteFooter("Radiant",
            [
                new FooterColumn("Product", ["Features", "Pricing", "Changelog"]),
                new FooterColumn("Company", ["About", "Careers", "Contact"]),
                new FooterColumn("Legal", ["Privacy", "Terms"]),
            ])
            {
                Tagline = "A desktop app platform for .NET.",
                Copyright = "© 2026 Radiant. All rights reserved.",
            },
        ],
    };

    public static Element Store() => new StorePage();

    public static Element Empty() => new EmptyState("inbox", "No messages yet")
    {
        Description = "When someone writes to you, their messages will appear here.",
        Action = new SurfaceButton("Compose") { Icon = "edit" },
    };

    private sealed record SettingsPage(ThemeController Themes) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var notifications = context.UseState(true);
            var digest = context.UseState(false);
            var density = context.UseState(0);
            var themes = Themes;
            var theme = context.UseTheme();
            return new Box
            {
                Layout = new LayoutStyle { RowGap = 32 },
                Children =
                [
                    new PageHeading("Settings") { Description = "Manage your account and preferences" },
                    new SettingsSection("Appearance",
                    [
                        new SettingsRow("Dark theme", new Switch(theme.Theme.Colors.IsDark, dark =>
                            themes.Set(themes.Theme with { Colors = themes.Theme.Colors with { IsDark = dark } }, System.TimeSpan.FromMilliseconds(300))))
                        {
                            Description = "Use dark colours everywhere",
                        },
                        new SettingsRow("Density", new SelectField("Density", ["Comfortable", "Compact"], density.Value, i =>
                        {
                            density.Set(i);
                            themes.Set(themes.Theme with { Density = -i });
                        }) { Layout = new LayoutStyle { Width = 200 } }),
                    ]) { Description = "How Radiant looks on this device." },
                    new SettingsSection("Theme colour",
                    [
                        new Box
                        {
                            Layout = new LayoutStyle { Padding = Edges.All(16) },
                            Children =
                            [
                                new ColorPicker(unchecked((int)theme.Theme.Colors.Seed.ToArgb()), argb =>
                                    themes.Set(themes.Theme with { Colors = themes.Theme.Colors with { Seed = Radiant.Graphics2D.Color.FromArgb(argb) } }))
                                {
                                    Label = "Theme colour",
                                },
                            ],
                        },
                    ]) { Description = "Every colour in the app is worked out from this one, in light and dark." },
                    new SettingsSection("Notifications",
                    [
                        new SettingsRow("Push notifications", new Switch(notifications.Value, notifications.Set)) { Description = "Alerts for mentions and replies" },
                        new SettingsRow("Weekly digest", new Switch(digest.Value, digest.Set)) { Description = "A summary every Monday" },
                    ]) { Description = "What we tell you about, and how." },
                ],
            };
        }
    }

    private sealed record StorePage : Component
    {
        private static readonly int[] s_startingCart = [1, 0, 2, 0];

        private static readonly Product[] s_products =
        [
            new("Aurora lamp", "$89", SamplePictures.Gradient(30)) { Detail = "Warm white", Rating = 4.8, Badge = "New" },
            new("Tide mug", "$24", SamplePictures.Gradient(190)) { Detail = "Stoneware, 350 ml", Rating = 4.6 },
            new("Meadow throw", "$120", SamplePictures.Gradient(110)) { Detail = "Wool blend", Rating = 4.9 },
            new("Dusk print", "$45", SamplePictures.Gradient(290)) { Detail = "A3, framed", Rating = 4.4, Badge = "Sale" },
        ];

        public override Element? Build(BuildContext context)
        {
            var quantities = context.UseState(() => s_startingCart);
            var lines = new List<CartLine>();
            for (var i = 0; i < s_products.Length; i++)
            {
                if (quantities.Value[i] > 0)
                {
                    lines.Add(new CartLine(s_products[i], quantities.Value[i], decimal.Parse(s_products[i].Price.TrimStart('$'), System.Globalization.CultureInfo.InvariantCulture)));
                }
            }
            var snackbars = context.UseSnackbars();
            return new Box
            {
                Layout = new LayoutStyle { RowGap = 24 },
                Children =
                [
                    new PageHeading("Shop") { Description = "Things for a calmer desk" },
                    new ProductGrid(s_products, i =>
                    {
                        var next = (int[])quantities.Value.Clone();
                        next[i]++;
                        quantities.Set(next);
                        snackbars.Show($"Added {s_products[i].Name} to your cart");
                    }),
                    new CartSummary(lines) { Shipping = 0 },
                    new Reviews(
                    [
                        new Review("Ada Lovelace", 5, "A calmer desk indeed", "The walnut tray keeps everything in one place.") { Date = "12 March" },
                        new Review("Grace Hopper", 4, "Lovely lamp", "Warm light, though the switch is a little stiff.") { Date = "2 March" },
                    ]) { OnWrite = () => snackbars.Show("Thanks! Reviews open once your order arrives.") },
                    new SurfaceText("Your orders") { TextType = TextType.HeadlineSmall, HeadingLevel = 2 },
                    new OrderHistory(
                    [
                        new Order("WU88191111", "12 March 2026", "$96.00", "On its way", [new OrderLine("Walnut tray", 1, "$48.00"), new OrderLine("Linen notebook", 2, "$48.00")]),
                        new Order("WU88191009", "2 January 2026", "$64.00", "Delivered", [new OrderLine("Desk lamp", 1, "$64.00")]) { Complete = true },
                    ]) { OnBuyAgain = order => snackbars.Show($"Added order {order.Number} to your cart") },
                ],
            };
        }
    }
}
